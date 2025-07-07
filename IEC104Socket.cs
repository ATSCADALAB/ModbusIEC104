using System;
using System.Net.Sockets;
using System.Threading;

namespace IEC104
{
    /// <summary>
    /// Trạng thái kết nối IEC104
    /// </summary>
    public enum IEC104ConnectionState
    {
        /// <summary>Không kết nối</summary>
        Disconnected = 0,
        /// <summary>Đang kết nối TCP</summary>
        Connecting = 1,
        /// <summary>TCP đã kết nối, chưa STARTDT</summary>
        Connected = 2,
        /// <summary>Đang gửi STARTDT</summary>
        StartingDataTransfer = 3,
        /// <summary>STARTDT thành công, sẵn sàng truyền dữ liệu</summary>
        DataTransferStarted = 4,
        /// <summary>Đang dừng truyền dữ liệu</summary>
        StoppingDataTransfer = 5,
        /// <summary>Lỗi kết nối</summary>
        Error = 6
    }

    /// <summary>
    /// Lớp xử lý kết nối TCP cho giao thức IEC104 - tương tự ModbusSocket
    /// </summary>
    public class IEC104Socket
    {
        #region FIELDS

        private Socket socket;
        private int lastError;
        private readonly object lockObject = new object();

        #endregion

        #region PROPERTIES

        /// <summary>Trạng thái kết nối TCP</summary>
        public bool Connected => socket != null && socket.Connected;

        /// <summary>Trạng thái kết nối IEC104</summary>
        public IEC104ConnectionState State { get; private set; } = IEC104ConnectionState.Disconnected;

        /// <summary>Timeout cho việc nhận dữ liệu (ms)</summary>
        public int ReceiveTimeout { get; set; } = 10000;

        /// <summary>Timeout cho việc gửi dữ liệu (ms)</summary>
        public int SendTimeout { get; set; } = 10000;

        /// <summary>Timeout cho việc kết nối (ms)</summary>
        public int ConnectTimeout { get; set; } = 30000;

        /// <summary>Lỗi cuối cùng</summary>
        public int LastError => lastError;

        #endregion

        #region CONSTRUCTORS

        /// <summary>
        /// Constructor mặc định
        /// </summary>
        public IEC104Socket()
        {
        }

        /// <summary>
        /// Destructor
        /// </summary>
        ~IEC104Socket()
        {
            Close();
        }

        #endregion

        #region CONNECTION METHODS

        /// <summary>
        /// Đóng kết nối socket
        /// </summary>
        public void Close()
        {
            lock (lockObject)
            {
                try
                {
                    if (socket != null)
                    {
                        if (socket.Connected)
                        {
                            socket.Shutdown(SocketShutdown.Both);
                        }
                        socket.Close();
                        socket.Dispose();
                        socket = null;
                    }
                }
                catch
                {
                    // Ignore exceptions during close
                }
                finally
                {
                    State = IEC104ConnectionState.Disconnected;
                }
            }
        }

        /// <summary>
        /// Tạo socket mới
        /// </summary>
        private void CreateSocket()
        {
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socket.NoDelay = true; // Disable Nagle's algorithm for real-time communication
            socket.ReceiveTimeout = ReceiveTimeout;
            socket.SendTimeout = SendTimeout;
        }

        /// <summary>
        /// Ping TCP để kiểm tra kết nối
        /// </summary>
        /// <param name="host">Địa chỉ IP hoặc hostname</param>
        /// <param name="port">Port</param>
        private void TCPPing(string host, int port)
        {
            lastError = IEC104Constants.RESULT_OK;
            Socket pingSocket = null;

            try
            {
                pingSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                IAsyncResult result = pingSocket.BeginConnect(host, port, null, null);
                bool success = result.AsyncWaitHandle.WaitOne(ConnectTimeout, true);

                if (!success)
                {
                    lastError = IEC104Constants.ERR_CONNECTION_TIMEOUT;
                }
                else
                {
                    pingSocket.EndConnect(result);
                }
            }
            catch (SocketException ex)
            {
                lastError = IEC104Constants.ERR_CONNECTION_FAILED;
            }
            catch
            {
                lastError = IEC104Constants.ERR_CONNECTION_FAILED;
            }
            finally
            {
                try
                {
                    pingSocket?.Close();
                    pingSocket?.Dispose();
                }
                catch { }
            }
        }

        /// <summary>
        /// Kết nối TCP đến server IEC104
        /// </summary>
        /// <param name="host">Địa chỉ IP hoặc hostname</param>
        /// <param name="port">Port (mặc định 2404)</param>
        /// <returns>Mã lỗi (0 = thành công)</returns>
        public int Connect(string host, int port = IEC104Constants.DEFAULT_PORT)
        {
            lastError = IEC104Constants.RESULT_OK;

            if (Connected)
            {
                return lastError; // Already connected
            }

            lock (lockObject)
            {
                try
                {
                    State = IEC104ConnectionState.Connecting;

                    // Ping trước để kiểm tra
                    TCPPing(host, port);
                    if (lastError != IEC104Constants.RESULT_OK)
                    {
                        State = IEC104ConnectionState.Error;
                        return lastError;
                    }

                    // Tạo socket và kết nối
                    CreateSocket();

                    IAsyncResult result = socket.BeginConnect(host, port, null, null);
                    bool success = result.AsyncWaitHandle.WaitOne(ConnectTimeout, true);

                    if (success && socket.Connected)
                    {
                        socket.EndConnect(result);
                        State = IEC104ConnectionState.Connected;
                    }
                    else
                    {
                        Close();
                        lastError = IEC104Constants.ERR_CONNECTION_FAILED;
                        State = IEC104ConnectionState.Error;
                    }
                }
                catch (SocketException ex)
                {
                    Close();
                    lastError = IEC104Constants.ERR_CONNECTION_FAILED;
                    State = IEC104ConnectionState.Error;
                }
                catch
                {
                    Close();
                    lastError = IEC104Constants.ERR_CONNECTION_FAILED;
                    State = IEC104ConnectionState.Error;
                }
            }

            return lastError;
        }

        #endregion

        #region DATA TRANSFER METHODS

        /// <summary>
        /// Gửi dữ liệu qua socket
        /// </summary>
        /// <param name="buffer">Buffer chứa dữ liệu</param>
        /// <param name="offset">Vị trí bắt đầu trong buffer</param>
        /// <param name="size">Số bytes cần gửi</param>
        /// <returns>Mã lỗi (0 = thành công)</returns>
        public int Send(byte[] buffer, int offset, int size)
        {
            if (!Connected)
            {
                return IEC104Constants.ERR_NOT_CONNECTED;
            }

            int startTickCount = Environment.TickCount;
            int sent = 0;

            lock (lockObject)
            {
                try
                {
                    do
                    {
                        if (socket == null || !socket.Connected)
                        {
                            return IEC104Constants.ERR_NOT_CONNECTED;
                        }

                        if (Environment.TickCount > startTickCount + SendTimeout)
                        {
                            return IEC104Constants.ERR_SEND_TIMEOUT;
                        }

                        try
                        {
                            int bytesSent = socket.Send(buffer, offset + sent, size - sent, SocketFlags.None);
                            sent += bytesSent;
                        }
                        catch (SocketException ex)
                        {
                            if (ex.SocketErrorCode == SocketError.WouldBlock ||
                                ex.SocketErrorCode == SocketError.IOPending ||
                                ex.SocketErrorCode == SocketError.NoBufferSpaceAvailable)
                            {
                                Thread.Sleep(10); // Wait a bit and retry
                            }
                            else
                            {
                                return IEC104Constants.ERR_CONNECTION_FAILED;
                            }
                        }
                    } while (sent < size);
                }
                catch
                {
                    return IEC104Constants.ERR_CONNECTION_FAILED;
                }
            }

            return IEC104Constants.RESULT_OK;
        }

        /// <summary>
        /// Nhận dữ liệu từ socket
        /// </summary>
        /// <param name="buffer">Buffer để chứa dữ liệu nhận được</param>
        /// <param name="offset">Vị trí bắt đầu trong buffer</param>
        /// <param name="size">Số bytes cần nhận</param>
        /// <returns>Mã lỗi (0 = thành công)</returns>
        public int Receive(byte[] buffer, int offset, int size)
        {
            if (!Connected)
            {
                return IEC104Constants.ERR_NOT_CONNECTED;
            }

            int startTickCount = Environment.TickCount;
            int received = 0;

            lock (lockObject)
            {
                try
                {
                    do
                    {
                        if (socket == null || !socket.Connected)
                        {
                            return IEC104Constants.ERR_NOT_CONNECTED;
                        }

                        if (Environment.TickCount > startTickCount + ReceiveTimeout)
                        {
                            return IEC104Constants.ERR_RECEIVE_TIMEOUT;
                        }

                        try
                        {
                            int bytesReceived = socket.Receive(buffer, offset + received, size - received, SocketFlags.None);
                            if (bytesReceived == 0)
                            {
                                // Connection closed by remote
                                return IEC104Constants.ERR_CONNECTION_FAILED;
                            }
                            received += bytesReceived;
                        }
                        catch (SocketException ex)
                        {
                            if (ex.SocketErrorCode == SocketError.WouldBlock ||
                                ex.SocketErrorCode == SocketError.IOPending ||
                                ex.SocketErrorCode == SocketError.NoBufferSpaceAvailable)
                            {
                                Thread.Sleep(10); // Wait a bit and retry
                            }
                            else
                            {
                                return IEC104Constants.ERR_CONNECTION_FAILED;
                            }
                        }
                    } while (received < size);
                }
                catch
                {
                    return IEC104Constants.ERR_CONNECTION_FAILED;
                }
            }

            return IEC104Constants.RESULT_OK;
        }

        /// <summary>
        /// Nhận frame IEC104 hoàn chỉnh
        /// </summary>
        /// <param name="buffer">Buffer để chứa frame</param>
        /// <param name="maxSize">Kích thước tối đa của buffer</param>
        /// <param name="frameLength">Độ dài frame thực tế nhận được</param>
        /// <returns>Mã lỗi (0 = thành công)</returns>
        public int ReceiveFrame(byte[] buffer, int maxSize, out int frameLength)
        {
            frameLength = 0;

            if (!Connected)
            {
                return IEC104Constants.ERR_NOT_CONNECTED;
            }

            try
            {
                // Đọc start byte và length
                int result = Receive(buffer, 0, 2);
                if (result != IEC104Constants.RESULT_OK)
                {
                    return result;
                }

                // Kiểm tra start byte
                if (buffer[0] != IEC104Constants.START_BYTE)
                {
                    return IEC104Constants.ERR_INVALID_FRAME;
                }

                // Lấy APDU length
                byte apduLength = buffer[1];
                if (apduLength > IEC104Constants.MAX_APDU_LENGTH || 2 + apduLength > maxSize)
                {
                    return IEC104Constants.ERR_INVALID_FRAME;
                }

                // Đọc phần còn lại của frame
                if (apduLength > 0)
                {
                    result = Receive(buffer, 2, apduLength);
                    if (result != IEC104Constants.RESULT_OK)
                    {
                        return result;
                    }
                }

                frameLength = 2 + apduLength;
                return IEC104Constants.RESULT_OK;
            }
            catch
            {
                return IEC104Constants.ERR_CONNECTION_FAILED;
            }
        }

        #endregion

        #region IEC104 PROTOCOL METHODS

        /// <summary>
        /// Gửi U-frame
        /// </summary>
        /// <param name="function">Chức năng U-frame</param>
        /// <returns>Mã lỗi (0 = thành công)</returns>
        public int SendUFrame(UFrameFunction function)
        {
            var frame = IEC104Frame.CreateUFrame(function);
            if (frame == null)
            {
                return IEC104Constants.ERR_INVALID_FRAME;
            }

            byte[] frameData = frame.ToByteArray();
            if (frameData == null)
            {
                return IEC104Constants.ERR_INVALID_FRAME;
            }

            return Send(frameData, 0, frameData.Length);
        }

        /// <summary>
        /// Gửi S-frame (Supervisory frame)
        /// </summary>
        /// <param name="receiveSequenceNumber">Receive sequence number</param>
        /// <returns>Mã lỗi (0 = thành công)</returns>
        public int SendSFrame(ushort receiveSequenceNumber)
        {
            var frame = IEC104Frame.CreateSFrame(receiveSequenceNumber);
            if (frame == null)
            {
                return IEC104Constants.ERR_INVALID_FRAME;
            }

            byte[] frameData = frame.ToByteArray();
            if (frameData == null)
            {
                return IEC104Constants.ERR_INVALID_FRAME;
            }

            return Send(frameData, 0, frameData.Length);
        }

        /// <summary>
        /// Gửi I-frame (Information frame)
        /// </summary>
        /// <param name="sendSequenceNumber">Send sequence number</param>
        /// <param name="receiveSequenceNumber">Receive sequence number</param>
        /// <param name="asduData">ASDU data</param>
        /// <returns>Mã lỗi (0 = thành công)</returns>
        public int SendIFrame(ushort sendSequenceNumber, ushort receiveSequenceNumber, byte[] asduData)
        {
            var frame = IEC104Frame.CreateIFrame(sendSequenceNumber, receiveSequenceNumber, asduData);
            if (frame == null)
            {
                return IEC104Constants.ERR_INVALID_FRAME;
            }

            byte[] frameData = frame.ToByteArray();
            if (frameData == null)
            {
                return IEC104Constants.ERR_INVALID_FRAME;
            }

            return Send(frameData, 0, frameData.Length);
        }

        /// <summary>
        /// Bắt đầu truyền dữ liệu (gửi STARTDT)
        /// </summary>
        /// <returns>Mã lỗi (0 = thành công)</returns>
        public int StartDataTransfer()
        {
            if (State != IEC104ConnectionState.Connected)
            {
                return IEC104Constants.ERR_NOT_CONNECTED;
            }

            State = IEC104ConnectionState.StartingDataTransfer;

            int result = SendUFrame(UFrameFunction.STARTDT_ACT);
            if (result != IEC104Constants.RESULT_OK)
            {
                State = IEC104ConnectionState.Error;
                return result;
            }

            // Đợi STARTDT_CON response
            byte[] buffer = new byte[256];
            result = ReceiveFrame(buffer, buffer.Length, out int frameLength);
            if (result != IEC104Constants.RESULT_OK)
            {
                State = IEC104ConnectionState.Error;
                return result;
            }

            // Parse response frame
            var responseFrame = IEC104Frame.FromByteArray(buffer);
            if (responseFrame == null || !responseFrame.IsUFrame(UFrameFunction.STARTDT_CON))
            {
                State = IEC104ConnectionState.Error;
                return IEC104Constants.ERR_STARTDT_FAILED;
            }

            State = IEC104ConnectionState.DataTransferStarted;
            return IEC104Constants.RESULT_OK;
        }

        /// <summary>
        /// Dừng truyền dữ liệu (gửi STOPDT)
        /// </summary>
        /// <returns>Mã lỗi (0 = thành công)</returns>
        public int StopDataTransfer()
        {
            if (State != IEC104ConnectionState.DataTransferStarted)
            {
                return IEC104Constants.RESULT_OK; // Already stopped
            }

            State = IEC104ConnectionState.StoppingDataTransfer;

            int result = SendUFrame(UFrameFunction.STOPDT_ACT);
            if (result != IEC104Constants.RESULT_OK)
            {
                State = IEC104ConnectionState.Error;
                return result;
            }

            // Đợi STOPDT_CON response (tùy chọn)
            try
            {
                byte[] buffer = new byte[256];
                result = ReceiveFrame(buffer, buffer.Length, out int frameLength);
                if (result == IEC104Constants.RESULT_OK)
                {
                    var responseFrame = IEC104Frame.FromByteArray(buffer);
                    // Kiểm tra STOPDT_CON nếu cần
                }
            }
            catch
            {
                // Ignore errors when stopping
            }

            State = IEC104ConnectionState.Connected;
            return IEC104Constants.RESULT_OK;
        }

        /// <summary>
        /// Gửi test frame
        /// </summary>
        /// <returns>Mã lỗi (0 = thành công)</returns>
        public int SendTestFrame()
        {
            if (State != IEC104ConnectionState.DataTransferStarted)
            {
                return IEC104Constants.ERR_NOT_CONNECTED;
            }

            int result = SendUFrame(UFrameFunction.TESTFR_ACT);
            if (result != IEC104Constants.RESULT_OK)
            {
                return result;
            }

            // Đợi TESTFR_CON response
            byte[] buffer = new byte[256];
            result = ReceiveFrame(buffer, buffer.Length, out int frameLength);
            if (result != IEC104Constants.RESULT_OK)
            {
                return result;
            }

            // Parse response frame
            var responseFrame = IEC104Frame.FromByteArray(buffer);
            if (responseFrame == null || !responseFrame.IsUFrame(UFrameFunction.TESTFR_CON))
            {
                return IEC104Constants.ERR_TESTFR_FAILED;
            }

            return IEC104Constants.RESULT_OK;
        }

        #endregion

        #region UTILITY METHODS

        /// <summary>
        /// Kiểm tra trạng thái kết nối
        /// </summary>
        /// <returns>True nếu sẵn sàng truyền dữ liệu</returns>
        public bool IsReadyForDataTransfer()
        {
            return State == IEC104ConnectionState.DataTransferStarted && Connected;
        }

        /// <summary>
        /// Reset trạng thái về Disconnected
        /// </summary>
        public void Reset()
        {
            Close();
            State = IEC104ConnectionState.Disconnected;
            lastError = IEC104Constants.RESULT_OK;
        }

        /// <summary>
        /// Lấy thông tin socket dưới dạng string để debug
        /// </summary>
        /// <returns>String mô tả socket</returns>
        public override string ToString()
        {
            return $"IEC104Socket: State={State}, Connected={Connected}, LastError={lastError}";
        }

        #endregion
    }
}