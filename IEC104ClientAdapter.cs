using ModbusIEC104;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace IEC104
{
    /// <summary>
    /// Client Adapter cho IEC104 - wrapper around IEC104Client
    /// </summary>
    public class IEC104ClientAdapter : ClientAdapter
    {
        #region FIELDS
        private IEC104Client iec104Client;
        private readonly object lockObject = new object();
        private Timer connectionMonitorTimer;
        private Timer dataProcessingTimer;
        private bool isDisposed = false;
        #endregion

        #region PROPERTIES
        /// <summary>IEC104 Client instance</summary>
        public new IEC104Client Client => iec104Client;

        /// <summary>Trạng thái kết nối IEC104</summary>
        public IEC104ConnectionState ConnectionState => iec104Client?.State ?? IEC104ConnectionState.Disconnected;

        /// <summary>Có đang trong trạng thái truyền dữ liệu không</summary>
        public bool IsDataTransferActive => iec104Client?.IsDataTransferActive ?? false;

        /// <summary>Common Address được sử dụng</summary>
        public ushort CommonAddress { get; set; } = 1;

        /// <summary>Số I-frame đã gửi</summary>
        public long ISentCount => iec104Client?.ISentCount ?? 0;

        /// <summary>Số I-frame đã nhận</summary>
        public long IReceivedCount => iec104Client?.IReceivedCount ?? 0;

        /// <summary>Thời gian kết nối cuối</summary>
        public DateTime LastConnectTime { get; private set; }

        /// <summary>Thời gian Interrogation cuối</summary>
        public DateTime LastInterrogationTime { get; private set; }

        /// <summary>Số lần Interrogation đã thực hiện</summary>
        public int InterrogationCount { get; private set; }

        /// <summary>Số lần Command đã gửi</summary>
        public int CommandCount { get; private set; }

        /// <summary>Queue để lưu dữ liệu tự phát</summary>
        private readonly Queue<InformationObject> spontaneousDataQueue = new Queue<InformationObject>();
        #endregion

        #region CONSTRUCTORS
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="settings">IEC104 Device Settings</param>
        public IEC104ClientAdapter(IEC104DeviceSettings settings) : base()
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            // Create IEC104 client
            iec104Client = new IEC104Client(settings.IpAddress, settings.Port, settings.ClientID);

            // Configure client parameters
            ConfigureClient(settings);

            // Set common address
            CommonAddress = settings.CommonAddress;

            // Initialize timers
            InitializeTimers();

            LastConnectTime = DateTime.MinValue;
            LastInterrogationTime = DateTime.MinValue;
        }
        #endregion

        #region CONFIGURATION
        /// <summary>
        /// Configure IEC104 client với settings
        /// </summary>
        /// <param name="settings">Device settings</param>
        private void ConfigureClient(IEC104DeviceSettings settings)
        {
            if (iec104Client == null || settings == null)
                return;

            // Set protocol parameters
            iec104Client.ParameterK = settings.K;
            iec104Client.ParameterW = settings.W;
            iec104Client.TimeoutT0 = settings.T0;
            iec104Client.TimeoutT1 = settings.T1;
            iec104Client.TimeoutT2 = settings.T2;
            iec104Client.TimeoutT3 = settings.T3;

            // Set timeouts
            iec104Client.TimeOut = settings.ReadTimeout;
            iec104Client.CommonAddress = settings.CommonAddress;
        }

        /// <summary>
        /// Initialize timers
        /// </summary>
        private void InitializeTimers()
        {
            // Connection monitor timer - kiểm tra kết nối mỗi 30 giây
            connectionMonitorTimer = new Timer(ConnectionMonitorCallback, null, 30000, 30000);

            // Data processing timer - xử lý dữ liệu mỗi 100ms
            dataProcessingTimer = new Timer(DataProcessingCallback, null, 100, 100);
        }
        #endregion

        #region CONNECTION METHODS
        /// <summary>
        /// Kết nối đến server
        /// </summary>
        /// <returns>True nếu thành công</returns>
        public override bool Connect()
        {
            try
            {
                lock (lockObject)
                {
                    if (IsConnected && IsDataTransferActive)
                        return true;

                    // Connect TCP
                    var connectResult = iec104Client.Connect();
                    if (connectResult != IEC104Constants.ERROR_NONE)
                    {
                        LastError = $"TCP connection failed: {connectResult}";
                        return false;
                    }

                    // Start data transfer
                    var startResult = iec104Client.StartDataTransfer();
                    if (startResult != IEC104Constants.ERROR_NONE)
                    {
                        LastError = $"Start data transfer failed: {startResult}";
                        iec104Client.Disconnect();
                        return false;
                    }

                    LastConnectTime = DateTime.Now;
                    ConnectCount++;
                    LastError = null;

                    // Send initial general interrogation
                    Task.Run(() => SendInitialInterrogation());

                    return true;
                }
            }
            catch (Exception ex)
            {
                LastError = $"Connect error: {ex.Message}";
                return false;
            }
        }

        /// <summary>
        /// Ngắt kết nối
        /// </summary>
        /// <returns>True nếu thành công</returns>
        public override bool Disconnect()
        {
            try
            {
                lock (lockObject)
                {
                    var result = iec104Client?.Disconnect();
                    return result == IEC104Constants.ERROR_NONE;
                }
            }
            catch (Exception ex)
            {
                LastError = $"Disconnect error: {ex.Message}";
                return false;
            }
        }

        /// <summary>
        /// Kiểm tra có kết nối không
        /// </summary>
        public override bool IsConnected => iec104Client?.Connected == true && IsDataTransferActive;
        #endregion

        #region IEC104 SPECIFIC METHODS
        /// <summary>
        /// Gửi General Interrogation
        /// </summary>
        /// <param name="commonAddress">Common Address</param>
        /// <param name="type">Loại interrogation</param>
        /// <returns>True nếu thành công</returns>
        public bool SendInterrogation(ushort commonAddress, InterrogationType type = InterrogationType.General)
        {
            try
            {
                if (!IsConnected)
                {
                    LastError = "Not connected";
                    return false;
                }

                var qualifier = (byte)type;
                var result = iec104Client.SendInterrogation(commonAddress, qualifier);

                if (result == IEC104Constants.ERROR_NONE)
                {
                    LastInterrogationTime = DateTime.Now;
                    InterrogationCount++;
                    return true;
                }
                else
                {
                    LastError = $"Interrogation failed: {result}";
                    return false;
                }
            }
            catch (Exception ex)
            {
                LastError = $"Send interrogation error: {ex.Message}";
                return false;
            }
        }

        /// <summary>
        /// Gửi Counter Interrogation
        /// </summary>
        /// <param name="commonAddress">Common Address</param>
        /// <param name="type">Loại counter interrogation</param>
        /// <returns>True nếu thành công</returns>
        public bool SendCounterInterrogation(ushort commonAddress, CounterInterrogationType type = CounterInterrogationType.General)
        {
            try
            {
                if (!IsConnected)
                {
                    LastError = "Not connected";
                    return false;
                }

                var qualifier = (byte)type;
                // Use same SendInterrogation method but with counter interrogation TypeID
                var asdu = ASDU.CreateCounterInterrogation(commonAddress, qualifier);

                // Send via client (implementation would need SendASADU method)
                return true; // Placeholder
            }
            catch (Exception ex)
            {
                LastError = $"Send counter interrogation error: {ex.Message}";
                return false;
            }
        }

        /// <summary>
        /// Gửi lệnh điều khiển
        /// </summary>
        /// <param name="commonAddress">Common Address</param>
        /// <param name="ioa">Information Object Address</param>
        /// <param name="typeId">Type ID</param>
        /// <param name="value">Giá trị</param>
        /// <param name="selectBeforeOperate">Có dùng Select-Before-Operate không</param>
        /// <returns>True nếu thành công</returns>
        public bool SendCommand(ushort commonAddress, uint ioa, byte typeId, object value, bool selectBeforeOperate = false)
        {
            try
            {
                if (!IsConnected)
                {
                    LastError = "Not connected";
                    return false;
                }

                var result = iec104Client.SendCommand(commonAddress, ioa, typeId, value, selectBeforeOperate);

                if (result == IEC104Constants.ERROR_NONE)
                {
                    CommandCount++;
                    return true;
                }
                else
                {
                    LastError = $"Command failed: {result}";
                    return false;
                }
            }
            catch (Exception ex)
            {
                LastError = $"Send command error: {ex.Message}";
                return false;
            }
        }

        /// <summary>
        /// Gửi Single Command
        /// </summary>
        /// <param name="commonAddress">Common Address</param>
        /// <param name="ioa">Information Object Address</param>
        /// <param name="state">Command state (On/Off)</param>
        /// <param name="selectBeforeOperate">Select before operate</param>
        /// <returns>True nếu thành công</returns>
        public bool SendSingleCommand(ushort commonAddress, uint ioa, bool state, bool selectBeforeOperate = false)
        {
            return SendCommand(commonAddress, ioa, IEC104Constants.C_SC_NA_1, state, selectBeforeOperate);
        }

        /// <summary>
        /// Gửi Double Command
        /// </summary>
        /// <param name="commonAddress">Common Address</param>
        /// <param name="ioa">Information Object Address</param>
        /// <param name="state">Command state</param>
        /// <param name="selectBeforeOperate">Select before operate</param>
        /// <returns>True nếu thành công</returns>
        public bool SendDoubleCommand(ushort commonAddress, uint ioa, DoublePointState state, bool selectBeforeOperate = false)
        {
            return SendCommand(commonAddress, ioa, IEC104Constants.C_DC_NA_1, state, selectBeforeOperate);
        }

        /// <summary>
        /// Gửi Setpoint Command (Float)
        /// </summary>
        /// <param name="commonAddress">Common Address</param>
        /// <param name="ioa">Information Object Address</param>
        /// <param name="value">Float value</param>
        /// <param name="selectBeforeOperate">Select before operate</param>
        /// <returns>True nếu thành công</returns>
        public bool SendSetpointFloat(ushort commonAddress, uint ioa, float value, bool selectBeforeOperate = false)
        {
            return SendCommand(commonAddress, ioa, IEC104Constants.C_SE_NC_1, value, selectBeforeOperate);
        }

        /// <summary>
        /// Đọc Information Objects
        /// </summary>
        /// <param name="objects">Danh sách objects đã đọc</param>
        /// <returns>True nếu có dữ liệu</returns>
        public bool ReadInformationObjects(out List<InformationObject> objects)
        {
            objects = new List<InformationObject>();

            try
            {
                if (!IsConnected)
                    return false;

                var result = iec104Client.ReadInformationObjects(out objects);
                return result == IEC104Constants.ERROR_NONE && objects != null;
            }
            catch (Exception ex)
            {
                LastError = $"Read information objects error: {ex.Message}";
                return false;
            }
        }

        /// <summary>
        /// Xử lý dữ liệu tự phát
        /// </summary>
        /// <param name="objects">Danh sách objects tự phát</param>
        /// <returns>True nếu có dữ liệu</returns>
        public bool ProcessSpontaneousData(out List<InformationObject> objects)
        {
            objects = new List<InformationObject>();

            try
            {
                lock (lockObject)
                {
                    // Lấy dữ liệu từ queue
                    while (spontaneousDataQueue.Count > 0)
                    {
                        objects.Add(spontaneousDataQueue.Dequeue());
                    }

                    return objects.Count > 0;
                }
            }
            catch (Exception ex)
            {
                LastError = $"Process spontaneous data error: {ex.Message}";
                return false;
            }
        }

        /// <summary>
        /// Lấy dữ liệu tự phát
        /// </summary>
        /// <returns>Danh sách objects tự phát</returns>
        public List<InformationObject> ProcessSpontaneousData()
        {
            if (ProcessSpontaneousData(out List<InformationObject> objects))
            {
                return objects;
            }
            return new List<InformationObject>();
        }
        #endregion

        #region DATA PROCESSING
        /// <summary>
        /// Gửi General Interrogation ban đầu
        /// </summary>
        private void SendInitialInterrogation()
        {
            try
            {
                Thread.Sleep(1000); // Đợi 1 giây sau khi kết nối

                if (IsConnected)
                {
                    SendInterrogation(CommonAddress, InterrogationType.General);
                }
            }
            catch (Exception ex)
            {
                LastError = $"Initial interrogation error: {ex.Message}";
            }
        }

        /// <summary>
        /// Connection monitor callback
        /// </summary>
        /// <param name="state">Timer state</param>
        private void ConnectionMonitorCallback(object state)
        {
            try
            {
                if (!CheckConnection())
                {
                    // Thử kết nối lại nếu bị mất kết nối
                    Task.Run(() => ReconnectIfNeeded());
                }
            }
            catch (Exception ex)
            {
                LastError = $"Connection monitor error: {ex.Message}";
            }
        }

        /// <summary>
        /// Data processing callback
        /// </summary>
        /// <param name="state">Timer state</param>
        private void DataProcessingCallback(object state)
        {
            try
            {
                if (!IsConnected)
                    return;

                // Đọc dữ liệu từ client và phân loại
                if (ReadInformationObjects(out List<InformationObject> objects))
                {
                    ProcessIncomingData(objects);
                }
            }
            catch (Exception ex)
            {
                // Không log lỗi ở đây để tránh spam log
                System.Diagnostics.Debug.WriteLine($"Data processing error: {ex.Message}");
            }
        }

        /// <summary>
        /// Xử lý dữ liệu đến
        /// </summary>
        /// <param name="objects">Danh sách objects</param>
        private void ProcessIncomingData(List<InformationObject> objects)
        {
            if (objects == null || objects.Count == 0)
                return;

            lock (lockObject)
            {
                foreach (var obj in objects)
                {
                    // Phân loại dữ liệu: spontaneous vs response
                    if (IsSpontaneousData(obj))
                    {
                        // Thêm vào spontaneous queue
                        spontaneousDataQueue.Enqueue(obj);

                        // Giới hạn kích thước queue
                        while (spontaneousDataQueue.Count > 1000)
                        {
                            spontaneousDataQueue.Dequeue();
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Kiểm tra có phải dữ liệu tự phát không
        /// </summary>
        /// <param name="obj">Information object</param>
        /// <returns>True nếu là dữ liệu tự phát</returns>
        private bool IsSpontaneousData(InformationObject obj)
        {
            // Logic để xác định dữ liệu tự phát
            // Thường dựa vào COT (Cause of Transmission)
            // COT = 3 (Spontaneous) hoặc COT = 1 (Periodic)

            // Đây là implementation đơn giản
            // Thực tế cần parse ASDU để lấy COT
            return true; // Placeholder
        }

        /// <summary>
        /// Kết nối lại nếu cần
        /// </summary>
        private void ReconnectIfNeeded()
        {
            try
            {
                if (isDisposed)
                    return;

                // Đợi một chút trước khi thử kết nối lại
                Thread.Sleep(5000);

                if (!IsConnected)
                {
                    Connect();
                }
            }
            catch (Exception ex)
            {
                LastError = $"Reconnect error: {ex.Message}";
            }
        }
        #endregion

        #region STATUS METHODS
        /// <summary>
        /// Lấy thông tin trạng thái
        /// </summary>
        /// <returns>Thông tin trạng thái</returns>
        public override string GetStatusInfo()
        {
            var baseStatus = base.GetStatusInfo();
            var iec104Status = $"IEC104[State:{ConnectionState}, CA:{CommonAddress}, " +
                             $"I-Sent:{ISentCount}, I-Recv:{IReceivedCount}, " +
                             $"Interrogations:{InterrogationCount}, Commands:{CommandCount}]";

            return $"{baseStatus} | {iec104Status}";
        }

        /// <summary>
        /// Lấy thống kê IEC104
        /// </summary>
        /// <returns>Thống kê IEC104 client adapter</returns>
        public IEC104ClientAdapterStatistics GetIEC104Statistics()
        {
            var baseStats = GetStatistics();
            var iec104Stats = iec104Client?.GetStatistics();

            return new IEC104ClientAdapterStatistics
            {
                // Base statistics
                ClientID = baseStats.ClientID,
                IsConnected = baseStats.IsConnected,
                ConnectCount = baseStats.ConnectCount,
                LastConnectTime = baseStats.LastConnectTime,
                LastError = baseStats.LastError,

                // IEC104 specific
                ConnectionState = ConnectionState,
                IsDataTransferActive = IsDataTransferActive,
                CommonAddress = CommonAddress,
                LastInterrogationTime = LastInterrogationTime,
                InterrogationCount = InterrogationCount,
                CommandCount = CommandCount,
                SpontaneousDataQueueCount = spontaneousDataQueue.Count,

                // Client statistics
                IEC104ClientStatistics = iec104Stats
            };
        }

        /// <summary>
        /// Reset counters
        /// </summary>
        public void ResetCounters()
        {
            InterrogationCount = 0;
            CommandCount = 0;

            lock (lockObject)
            {
                spontaneousDataQueue.Clear();
            }
        }
        #endregion

        #region DISPOSE
        /// <summary>
        /// Dispose adapter
        /// </summary>
        public override void Dispose()
        {
            if (!isDisposed)
            {
                // Stop timers
                connectionMonitorTimer?.Dispose();
                connectionMonitorTimer = null;

                dataProcessingTimer?.Dispose();
                dataProcessingTimer = null;

                // Disconnect client
                iec104Client?.Disconnect();
                iec104Client?.Dispose();
                iec104Client = null;

                // Clear queue
                lock (lockObject)
                {
                    spontaneousDataQueue.Clear();
                }

                isDisposed = true;
                base.Dispose();
            }
        }
        #endregion
    }

    #region SUPPORTING CLASSES
    /// <summary>
    /// Base Client Adapter class
    /// </summary>
    public abstract class ClientAdapter : IDisposable
    {
        #region PROPERTIES
        /// <summary>Client ID</summary>
        public string ClientID { get; protected set; }

        /// <summary>Có kết nối không</summary>
        public abstract bool IsConnected { get; }

        /// <summary>Số lần kết nối</summary>
        public int ConnectCount { get; protected set; }

        /// <summary>Thời gian kết nối cuối</summary>
        public DateTime LastConnectTime { get; protected set; }

        /// <summary>Lỗi cuối</summary>
        public string LastError { get; protected set; }
        #endregion

        #region ABSTRACT METHODS
        /// <summary>Kết nối</summary>
        public abstract bool Connect();

        /// <summary>Ngắt kết nối</summary>
        public abstract bool Disconnect();

        /// <summary>Kiểm tra kết nối</summary>
        public abstract bool CheckConnection();
        #endregion

        #region VIRTUAL METHODS
        /// <summary>
        /// Lấy thông tin trạng thái
        /// </summary>
        /// <returns>Thông tin trạng thái</returns>
        public virtual string GetStatusInfo()
        {
            return $"ClientAdapter[{ClientID}] - {(IsConnected ? "Connected" : "Disconnected")} | " +
                   $"Connects: {ConnectCount} | Last: {LastConnectTime:HH:mm:ss}";
        }

        /// <summary>
        /// Lấy thống kê
        /// </summary>
        /// <returns>Thống kê</returns>
        public virtual ClientAdapterStatistics GetStatistics()
        {
            return new ClientAdapterStatistics
            {
                ClientID = ClientID,
                IsConnected = IsConnected,
                ConnectCount = ConnectCount,
                LastConnectTime = LastConnectTime,
                LastError = LastError
            };
        }

        /// <summary>
        /// Dispose
        /// </summary>
        public virtual void Dispose()
        {
            // Override in derived classes
        }
        #endregion
    }

    /// <summary>
    /// Client Adapter Statistics
    /// </summary>
    public class ClientAdapterStatistics
    {
        public string ClientID { get; set; }
        public bool IsConnected { get; set; }
        public int ConnectCount { get; set; }
        public DateTime LastConnectTime { get; set; }
        public string LastError { get; set; }

        public override string ToString()
        {
            return $"ClientAdapter[{ClientID}] - {(IsConnected ? "Connected" : "Disconnected")} | " +
                   $"Connects: {ConnectCount}";
        }
    }

    /// <summary>
    /// IEC104 Client Adapter Statistics
    /// </summary>
    public class IEC104ClientAdapterStatistics : ClientAdapterStatistics
    {
        public IEC104ConnectionState ConnectionState { get; set; }
        public bool IsDataTransferActive { get; set; }
        public ushort CommonAddress { get; set; }
        public DateTime LastInterrogationTime { get; set; }
        public int InterrogationCount { get; set; }
        public int CommandCount { get; set; }
        public int SpontaneousDataQueueCount { get; set; }
        public IEC104ClientStatistics IEC104ClientStatistics { get; set; }

        public override string ToString()
        {
            return $"IEC104ClientAdapter[{ClientID}] - State:{ConnectionState} | " +
                   $"CA:{CommonAddress} | Interrogations:{InterrogationCount} | " +
                   $"Commands:{CommandCount} | Queue:{SpontaneousDataQueueCount}";
        }
    }
    #endregion
}