using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace ModbusIEC104
{
    /// <summary>
    /// Quản lý socket TCP cho kết nối IEC104
    /// </summary>
    public class IEC104Socket : IDisposable
    {
        #region FIELDS
        private TcpClient tcpClient;
        private NetworkStream networkStream;
        private readonly object lockObject = new object();
        private bool isDisposed = false;
        private Timer keepAliveTimer;
        #endregion

        #region PROPERTIES
        /// <summary>Địa chỉ IP của server</summary>
        public string IpAddress { get; private set; }

        /// <summary>Cổng kết nối</summary>
        public int Port { get; private set; }

        /// <summary>Timeout cho kết nối (ms)</summary>
        public int ConnectionTimeout { get; set; } = IEC104Constants.DEFAULT_CONNECTION_TIMEOUT;

        /// <summary>Timeout cho đọc dữ liệu (ms)</summary>
        public int ReadTimeout { get; set; } = IEC104Constants.DEFAULT_READ_TIMEOUT;

        /// <summary>Timeout cho ghi dữ liệu (ms)</summary>
        public int WriteTimeout { get; set; } = IEC104Constants.DEFAULT_READ_TIMEOUT;

        /// <summary>Kiểm tra trạng thái kết nối</summary>
        public bool Connected
        {
            get
            {
                lock (lockObject)
                {
                    return tcpClient?.Connected == true && networkStream != null;
                }
            }
        }

        /// <summary>Số bytes có sẵn để đọc</summary>
        public int Available
        {
            get
            {
                lock (lockObject)
                {
                    return tcpClient?.Available ?? 0;
                }
            }
        }

        /// <summary>Endpoint local của kết nối</summary>
        public EndPoint LocalEndPoint
        {
            get
            {
                lock (lockObject)
                {
                    return tcpClient?.Client?.LocalEndPoint;
                }
            }
        }

        /// <summary>Endpoint remote của kết nối</summary>
        public EndPoint RemoteEndPoint
        {
            get
            {
                lock (lockObject)
                {
                    return tcpClient?.Client?.RemoteEndPoint;
                }
            }
        }

        /// <summary>Thời gian kết nối cuối</summary>
        public DateTime LastConnectTime { get; private set; }

        /// <summary>Thời gian gửi dữ liệu cuối</summary>
        public DateTime LastSendTime { get; private set; }

        /// <summary>Thời gian nhận dữ liệu cuối</summary>
        public DateTime LastReceiveTime { get; private set; }
        #endregion

        #region CONSTRUCTORS
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="ipAddress">Địa chỉ IP</param>
        /// <param name="port">Cổng kết nối</param>
        public IEC104Socket(string ipAddress, int port)
        {
            IpAddress = ipAddress ?? throw new ArgumentNullException(nameof(ipAddress));
            Port = port;

            if (port <= 0 || port > 65535)
                throw new ArgumentOutOfRangeException(nameof(port), "Port must be between 1 and 65535");
        }
        #endregion

        #region CONNECTION METHODS
        /// <summary>
        /// Kết nối đến server
        /// </summary>
        /// <returns>True nếu kết nối thành công</returns>
        public bool Connect()
        {
            try
            {
                lock (lockObject)
                {
                    if (Connected)
                        return true;

                    // Đóng kết nối cũ nếu có
                    CloseInternal();

                    // Tạo kết nối mới
                    tcpClient = new TcpClient();

                    // Thiết lập timeout
                    tcpClient.ReceiveTimeout = ReadTimeout;
                    tcpClient.SendTimeout = WriteTimeout;

                    // Thiết lập socket options
                    tcpClient.NoDelay = true; // Disable Nagle algorithm for real-time communication
                    tcpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);

                    // Kết nối với timeout
                    var result = tcpClient.BeginConnect(IpAddress, Port, null, null);
                    var success = result.AsyncWaitHandle.WaitOne(ConnectionTimeout);

                    if (!success)
                    {
                        tcpClient.Close();
                        tcpClient = null;
                        return false;
                    }

                    tcpClient.EndConnect(result);

                    // Lấy network stream
                    networkStream = tcpClient.GetStream();
                    networkStream.ReadTimeout = ReadTimeout;
                    networkStream.WriteTimeout = WriteTimeout;

                    LastConnectTime = DateTime.Now;

                    // Khởi động keep-alive timer
                    StartKeepAliveTimer();

                    return true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"IEC104Socket.Connect error: {ex.Message}");
                CloseInternal();
                return false;
            }
        }

        /// <summary>
        /// Đóng kết nối
        /// </summary>
        public void Close()
        {
            lock (lockObject)
            {
                CloseInternal();
            }
        }

        /// <summary>
        /// Đóng kết nối (internal method không lock)
        /// </summary>
        private void CloseInternal()
        {
            try
            {
                // Dừng keep-alive timer
                keepAliveTimer?.Dispose();
                keepAliveTimer = null;

                // Đóng network stream
                networkStream?.Close();
                networkStream?.Dispose();
                networkStream = null;

                // Đóng TCP client
                tcpClient?.Close();
                tcpClient?.Dispose();
                tcpClient = null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"IEC104Socket.CloseInternal error: {ex.Message}");
            }
        }
        #endregion

        #region SEND/RECEIVE METHODS
        /// <summary>
        /// Gửi dữ liệu
        /// </summary>
        /// <param name="data">Dữ liệu cần gửi</param>
        /// <returns>Số bytes đã gửi</returns>
        public int Send(byte[] data)
        {
            if (data == null || data.Length == 0)
                return 0;

            try
            {
                lock (lockObject)
                {
                    if (!Connected)
                        throw new InvalidOperationException("Socket is not connected");

                    networkStream.Write(data, 0, data.Length);
                    networkStream.Flush();

                    LastSendTime = DateTime.Now;
                    return data.Length;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"IEC104Socket.Send error: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Nhận dữ liệu
        /// </summary>
        /// <param name="buffer">Buffer để chứa dữ liệu</param>
        /// <param name="offset">Vị trí bắt đầu trong buffer</param>
        /// <param name="size">Số bytes tối đa cần đọc</param>
        /// <returns>Số bytes đã đọc</returns>
        public int Receive(byte[] buffer, int offset, int size)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));

            if (offset < 0 || size <= 0 || offset + size > buffer.Length)
                throw new ArgumentOutOfRangeException("Invalid offset or size");

            try
            {
                lock (lockObject)
                {
                    if (!Connected)
                        throw new InvalidOperationException("Socket is not connected");

                    var bytesRead = networkStream.Read(buffer, offset, size);

                    if (bytesRead > 0)
                        LastReceiveTime = DateTime.Now;

                    return bytesRead;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"IEC104Socket.Receive error: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Nhận đúng số bytes yêu cầu
        /// </summary>
        /// <param name="buffer">Buffer để chứa dữ liệu</param>
        /// <param name="offset">Vị trí bắt đầu trong buffer</param>
        /// <param name="size">Số bytes cần đọc</param>
        /// <param name="timeout">Timeout (ms)</param>
        /// <returns>True nếu đọc đủ số bytes</returns>
        public bool ReceiveExact(byte[] buffer, int offset, int size, int timeout = 0)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));

            if (offset < 0 || size <= 0 || offset + size > buffer.Length)
                throw new ArgumentOutOfRangeException("Invalid offset or size");

            var timeoutToUse = timeout > 0 ? timeout : ReadTimeout;
            var startTime = DateTime.Now;
            var totalBytesRead = 0;

            try
            {
                while (totalBytesRead < size)
                {
                    // Kiểm tra timeout
                    if ((DateTime.Now - startTime).TotalMilliseconds > timeoutToUse)
                        return false;

                    lock (lockObject)
                    {
                        if (!Connected)
                            return false;

                        // Kiểm tra dữ liệu có sẵn
                        if (networkStream.DataAvailable || totalBytesRead == 0)
                        {
                            var bytesRead = networkStream.Read(buffer, offset + totalBytesRead, size - totalBytesRead);

                            if (bytesRead == 0)
                                return false; // Connection closed

                            totalBytesRead += bytesRead;

                            if (totalBytesRead > 0)
                                LastReceiveTime = DateTime.Now;
                        }
                        else
                        {
                            // Chờ một chút trước khi thử lại
                            Thread.Sleep(1);
                        }
                    }
                }

                return totalBytesRead == size;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"IEC104Socket.ReceiveExact error: {ex.Message}");
                return false;
            }
        }
        #endregion

        #region KEEP-ALIVE METHODS
        /// <summary>
        /// Khởi động keep-alive timer
        /// </summary>
        private void StartKeepAliveTimer()
        {
            try
            {
                // Timer kiểm tra kết nối mỗi 30 giây
                keepAliveTimer = new Timer(KeepAliveCallback, null, 30000, 30000);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"IEC104Socket.StartKeepAliveTimer error: {ex.Message}");
            }
        }

        /// <summary>
        /// Callback cho keep-alive timer
        /// </summary>
        /// <param name="state">Timer state</param>
        private void KeepAliveCallback(object state)
        {
            try
            {
                lock (lockObject)
                {
                    if (!Connected)
                        return;

                    // Kiểm tra xem có activity gần đây không
                    var now = DateTime.Now;
                    var timeSinceLastActivity = now - Math.Max(LastSendTime, LastReceiveTime);

                    // Nếu không có activity trong 60 giây, thử ping socket
                    if (timeSinceLastActivity.TotalSeconds > 60)
                    {
                        if (!IsSocketConnected())
                        {
                            CloseInternal();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"IEC104Socket.KeepAliveCallback error: {ex.Message}");
            }
        }

        /// <summary>
        /// Kiểm tra socket có thực sự kết nối không
        /// </summary>
        /// <returns>True nếu socket còn kết nối</returns>
        private bool IsSocketConnected()
        {
            try
            {
                if (tcpClient?.Client == null)
                    return false;

                var socket = tcpClient.Client;

                // Sử dụng Poll để kiểm tra trạng thái
                return !(socket.Poll(1, SelectMode.SelectRead) && socket.Available == 0);
            }
            catch
            {
                return false;
            }
        }
        #endregion

        #region STATUS METHODS
        /// <summary>
        /// Lấy thông tin trạng thái socket
        /// </summary>
        /// <returns>Thông tin trạng thái</returns>
        public string GetStatusInfo()
        {
            lock (lockObject)
            {
                if (!Connected)
                    return "Disconnected";

                return $"Connected to {RemoteEndPoint} | " +
                       $"Last Send: {LastSendTime:HH:mm:ss} | " +
                       $"Last Receive: {LastReceiveTime:HH:mm:ss} | " +
                       $"Available: {Available} bytes";
            }
        }

        /// <summary>
        /// Lấy thống kê kết nối
        /// </summary>
        /// <returns>Thống kê kết nối</returns>
        public SocketStatistics GetStatistics()
        {
            lock (lockObject)
            {
                return new SocketStatistics
                {
                    IsConnected = Connected,
                    LocalEndPoint = LocalEndPoint?.ToString(),
                    RemoteEndPoint = RemoteEndPoint?.ToString(),
                    LastConnectTime = LastConnectTime,
                    LastSendTime = LastSendTime,
                    LastReceiveTime = LastReceiveTime,
                    AvailableBytes = Available,
                    ConnectionTimeout = ConnectionTimeout,
                    ReadTimeout = ReadTimeout,
                    WriteTimeout = WriteTimeout
                };
            }
        }
        #endregion

        #region DISPOSE
        /// <summary>
        /// Dispose socket
        /// </summary>
        public void Dispose()
        {
            if (!isDisposed)
            {
                Close();
                isDisposed = true;
            }
        }
        #endregion
    }

    #region SUPPORTING CLASSES
    /// <summary>
    /// Thống kê socket
    /// </summary>
    public class SocketStatistics
    {
        public bool IsConnected { get; set; }
        public string LocalEndPoint { get; set; }
        public string RemoteEndPoint { get; set; }
        public DateTime LastConnectTime { get; set; }
        public DateTime LastSendTime { get; set; }
        public DateTime LastReceiveTime { get; set; }
        public int AvailableBytes { get; set; }
        public int ConnectionTimeout { get; set; }
        public int ReadTimeout { get; set; }
        public int WriteTimeout { get; set; }

        public override string ToString()
        {
            return $"Socket [{(IsConnected ? "Connected" : "Disconnected")}] " +
                   $"{LocalEndPoint} -> {RemoteEndPoint} | " +
                   $"Connect: {LastConnectTime:yyyy-MM-dd HH:mm:ss} | " +
                   $"Send: {LastSendTime:HH:mm:ss} | " +
                   $"Receive: {LastReceiveTime:HH:mm:ss} | " +
                   $"Available: {AvailableBytes} bytes";
        }
    }
    #endregion
}