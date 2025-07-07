using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ModbusIEC104.Common;

namespace ModbusIEC104
{
    /// <summary>
    /// IEC104 Driver - Main driver class implementing IATDriver interface
    /// </summary>
    public class IEC104Driver : IATDriver, IDisposable
    {
        #region FIELDS
        private readonly List<DeviceReader> deviceReaders;
        private readonly Dictionary<string, IEC104DeviceSettings> deviceSettingMapping;
        private readonly Dictionary<string, IEC104Address> addressMapping;
        private readonly List<IEC104ClientAdapter> clientAdapters;
        private readonly object editLock = new object();
        private readonly Timer statusMonitorTimer;
        private bool isDisposed = false;
        #endregion

        #region PROPERTIES
        /// <summary>Driver name</summary>
        public string DriverName => "IEC104Driver";

        /// <summary>Driver version</summary>
        public string DriverVersion => "1.0.0";

        /// <summary>Số lượng device readers</summary>
        public int DeviceReadersCount => deviceReaders.Count;

        /// <summary>Số lượng client adapters</summary>
        public int ClientAdaptersCount => clientAdapters.Count;

        /// <summary>Số lượng addresses đã mapping</summary>
        public int AddressMappingCount => addressMapping.Count;

        /// <summary>Driver có đang chạy không</summary>
        public bool IsRunning { get; private set; }

        /// <summary>Thời gian khởi động</summary>
        public DateTime StartTime { get; private set; }

        /// <summary>Lỗi cuối cùng</summary>
        public string LastError { get; private set; }

        /// <summary>Tổng số tag đã đọc</summary>
        public long TotalTagsRead { get; private set; }

        /// <summary>Tổng số lỗi</summary>
        public long TotalErrors { get; private set; }
        #endregion

        #region CONSTRUCTORS
        /// <summary>
        /// Constructor
        /// </summary>
        public IEC104Driver()
        {
            deviceReaders = new List<DeviceReader>();
            deviceSettingMapping = new Dictionary<string, IEC104DeviceSettings>();
            addressMapping = new Dictionary<string, IEC104Address>();
            clientAdapters = new List<IEC104ClientAdapter>();

            // Initialize status monitor timer
            statusMonitorTimer = new Timer(StatusMonitorCallback, null, 10000, 10000); // Mỗi 10 giây

            StartTime = DateTime.Now;
        }
        #endregion

        #region IATDRIVER INTERFACE IMPLEMENTATION
        /// <summary>
        /// Khởi tạo driver
        /// </summary>
        /// <returns>True nếu thành công</returns>
        public bool Initialize()
        {
            try
            {
                IsRunning = true;
                LastError = null;
                return true;
            }
            catch (Exception ex)
            {
                LastError = $"Initialize failed: {ex.Message}";
                return false;
            }
        }

        /// <summary>
        /// Bắt đầu driver
        /// </summary>
        /// <returns>True nếu thành công</returns>
        public bool Start()
        {
            try
            {
                if (!IsRunning)
                {
                    Initialize();
                }

                lock (editLock)
                {
                    // Start all device readers
                    foreach (var deviceReader in deviceReaders)
                    {
                        deviceReader.Start();
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                LastError = $"Start failed: {ex.Message}";
                return false;
            }
        }

        /// <summary>
        /// Dừng driver
        /// </summary>
        /// <returns>True nếu thành công</returns>
        public bool Stop()
        {
            try
            {
                lock (editLock)
                {
                    // Stop all device readers
                    foreach (var deviceReader in deviceReaders)
                    {
                        deviceReader.Stop();
                    }
                }
                IsRunning = false;
                return true;
            }
            catch (Exception ex)
            {
                LastError = $"Stop failed: {ex.Message}";
                return false;
            }
        }

        /// <summary>
        /// Thiết lập device
        /// </summary>
        /// <param name="deviceName">Tên device</param>
        /// <param name="deviceID">Device ID</param>
        /// <returns>True nếu thành công</returns>
        public bool SetDevice(string deviceName, string deviceID)
        {
            try
            {
                if (string.IsNullOrEmpty(deviceName) || string.IsNullOrEmpty(deviceID))
                {
                    LastError = "Device name and ID cannot be null or empty";
                    return false;
                }

                // Parse device settings from deviceID
                var deviceSettings = IEC104DeviceSettings.Initialize(deviceID);
                if (deviceSettings == null || !deviceSettings.IsValid())
                {
                    LastError = $"Invalid device settings for device: {deviceName}";
                    return false;
                }

                lock (editLock)
                {
                    // Update device settings mapping
                    deviceSettingMapping[deviceName] = deviceSettings;

                    // Tạo client adapter nếu cần
                    AddIEC104ClientAdapter(deviceSettings);

                    // Tạo hoặc cập nhật device reader
                    var deviceReader = deviceReaders.FirstOrDefault(dr => dr.DeviceName == deviceName);
                    if (deviceReader == null)
                    {
                        deviceReader = new DeviceReader(this)
                        {
                            DeviceName = deviceName,
                            DeviceID = deviceID,
                            Settings = deviceSettings
                        };
                        deviceReaders.Add(deviceReader);
                        deviceReader.Initialize();
                    }
                    else
                    {
                        // Cập nhật settings nếu có thay đổi
                        deviceReader.Stop();
                        deviceReader.Settings = deviceSettings;
                        deviceReader.Initialize();
                    }

                    if (IsRunning) deviceReader.Start();
                }

                return true;
            }
            catch (Exception ex)
            {
                LastError = $"SetDevice failed: {ex.Message}";
                return false;
            }
        }

        /// <summary>
        /// Thiết lập tag
        /// </summary>
        /// <param name="tagName">Tên tag</param>
        /// <param name="deviceName">Tên device</param>
        /// <param name="tagAddress">Địa chỉ tag</param>
        /// <param name="tagType">Loại tag</param>
        /// <returns>True nếu thành công</returns>
        public bool SetTag(string tagName, string deviceName, string tagAddress, string tagType)
        {
            try
            {
                if (string.IsNullOrEmpty(tagName) || string.IsNullOrEmpty(tagAddress))
                {
                    LastError = "Tag name and address cannot be null or empty";
                    return false;
                }

                // Parse IEC104 address
                if (!GetIEC104Address(tagAddress, tagType, out IEC104Address address))
                {
                    LastError = $"Invalid IEC104 address: {tagAddress}";
                    return false;
                }

                lock (editLock)
                {
                    // Update address mapping
                    addressMapping[tagName] = address;
                }

                return true;
            }
            catch (Exception ex)
            {
                LastError = $"SetTag failed: {ex.Message}";
                return false;
            }
        }

        /// <summary>
        /// Đọc giá trị tag
        /// </summary>
        /// <param name="tagName">Tên tag</param>
        /// <param name="value">Giá trị đọc được</param>
        /// <returns>True nếu thành công</returns>
        public bool ReadTag(string tagName, out object value)
        {
            value = null;

            try
            {
                if (!addressMapping.TryGetValue(tagName, out IEC104Address address))
                {
                    LastError = $"Tag not found: {tagName}";
                    return false;
                }

                // Find appropriate device reader
                var deviceReader = FindDeviceReaderForAddress(address);
                if (deviceReader == null)
                {
                    LastError = $"No device reader found for tag: {tagName}";
                    return false;
                }

                // --- FIX: Logic lấy dữ liệu được sửa lại cho đúng ---
                // Lý do: Lấy trực tiếp điểm dữ liệu từ bộ đệm của BlockReader thay vì duyệt lại.
                var infoObject = deviceReader.GetSingleDataPoint(address.InformationObjectAddress);

                if (infoObject != null && infoObject.IsGoodQuality())
                {
                    value = ConvertValueByDataType(infoObject.Value, address.DataType);
                    TotalTagsRead++;
                    return true;
                }
                else
                {
                    // Không tìm thấy dữ liệu không nhất thiết là lỗi, chỉ là chưa có
                    LastError = $"No data available for tag: {tagName}";
                    TotalErrors++;
                    return false;
                }
            }
            catch (Exception ex)
            {
                LastError = $"ReadTag failed: {ex.Message}";
                TotalErrors++;
                return false;
            }
        }

        /// <summary>
        /// Ghi giá trị tag
        /// </summary>
        /// <param name="tagName">Tên tag</param>
        /// <param name="value">Giá trị cần ghi</param>
        /// <returns>True nếu thành công</returns>
        public bool WriteTag(string tagName, object value)
        {
            try
            {
                if (!addressMapping.TryGetValue(tagName, out IEC104Address address))
                {
                    LastError = $"Tag not found: {tagName}";
                    return false;
                }

                // Kiểm tra tag có thể ghi không
                if (!address.IsCommandType)
                {
                    LastError = $"Tag is not writable: {tagName}";
                    return false;
                }

                // Find appropriate client adapter
                var clientAdapter = FindClientAdapterForAddress(address);
                if (clientAdapter == null || !clientAdapter.IsConnected)
                {
                    LastError = $"No active client adapter found for tag: {tagName}";
                    return false;
                }

                // Send command
                var result = clientAdapter.SendCommand(
                    address.CommonAddress,
                    address.InformationObjectAddress,
                    address.TypeIdentification,
                    value);

                if (result)
                {
                    return true;
                }
                else
                {
                    LastError = $"Write command failed for tag: {tagName}. Details: {clientAdapter.LastError}";
                    TotalErrors++;
                    return false;
                }
            }
            catch (Exception ex)
            {
                LastError = $"WriteTag failed: {ex.Message}";
                TotalErrors++;
                return false;
            }
        }

        /// <summary>
        /// Đọc nhiều tag cùng lúc
        /// </summary>
        /// <param name="tagNames">Danh sách tên tag</param>
        /// <param name="values">Giá trị đọc được</param>
        /// <returns>True nếu thành công</returns>
        public bool ReadTags(string[] tagNames, out object[] values)
        {
            values = new object[tagNames?.Length ?? 0];

            if (tagNames == null || tagNames.Length == 0)
            {
                LastError = "Tag names cannot be null or empty";
                return false;
            }

            bool allSuccess = true;
            for (int i = 0; i < tagNames.Length; i++)
            {
                if (!ReadTag(tagNames[i], out object value))
                {
                    allSuccess = false;
                }
                values[i] = value;
            }
            return allSuccess;
        }

        /// <summary>
        /// Ghi nhiều tag cùng lúc
        /// </summary>
        /// <param name="tagNames">Danh sách tên tag</param>
        /// <param name="values">Giá trị cần ghi</param>
        /// <returns>True nếu thành công</returns>
        public bool WriteTags(string[] tagNames, object[] values)
        {
            if (tagNames == null || values == null || tagNames.Length != values.Length)
            {
                LastError = "Tag names and values arrays must have same length";
                return false;
            }

            bool allSuccess = true;
            for (int i = 0; i < tagNames.Length; i++)
            {
                if (!WriteTag(tagNames[i], values[i]))
                {
                    allSuccess = false;
                }
            }
            return allSuccess;
        }
        #endregion

        #region DEVICE READER MANAGEMENT

        #endregion

        #region CLIENT ADAPTER MANAGEMENT
        /// <summary>
        /// Thêm IEC104 Client Adapter
        /// </summary>
        /// <param name="settings">Device settings</param>
        /// <returns>Client Adapter</returns>
        private IEC104ClientAdapter AddIEC104ClientAdapter(IEC104DeviceSettings settings)
        {
            // Kiểm tra xem đã có client adapter cho connection này chưa
            var existingAdapter = clientAdapters.FirstOrDefault(ca => ca.ClientID == settings.ClientID);
            if (existingAdapter != null)
                return existingAdapter;

            lock (editLock)
            {
                // Double check sau khi lock
                existingAdapter = clientAdapters.FirstOrDefault(ca => ca.ClientID == settings.ClientID);
                if (existingAdapter != null) return existingAdapter;

                var clientAdapter = new IEC104ClientAdapter(settings);
                clientAdapters.Add(clientAdapter);
                clientAdapter.Connect(); // Thử kết nối ngay khi tạo
                return clientAdapter;
            }
        }

        /// <summary>
        /// Lấy Client Adapter theo ClientID (public method cho DeviceReader)
        /// </summary>
        /// <param name="clientID">Client ID</param>
        /// <returns>Client Adapter hoặc null</returns>
        public ClientAdapter GetClientAdapter(string clientID)
        {
            return clientAdapters.FirstOrDefault(ca => ca.ClientID == clientID);
        }
        #endregion

        #region ADDRESS PARSING
        /// <summary>
        /// Parse IEC104 address
        /// </summary>
        /// <param name="tagAddress">Tag address</param>
        /// <param name="tagType">Tag type</param>
        /// <param name="address">Parsed address</param>
        /// <returns>True nếu thành công</returns>
        public bool GetIEC104Address(string tagAddress, string tagType, out IEC104Address address)
        {
            address = null;

            try
            {
                if (string.IsNullOrEmpty(tagAddress))
                    return false;

                // Parse address using IEC104Address class
                address = new IEC104Address(tagAddress);
                return address.IsValid();
            }
            catch (Exception ex)
            {
                LastError = $"GetIEC104Address failed: {ex.Message}";
                return false;
            }
        }
        #endregion

        #region DATA ACCESS HELPERS
        /// <summary>
        /// Tìm Device Reader cho address
        /// </summary>
        /// <param name="address">IEC104 Address</param>
        /// <returns>Device Reader hoặc null</returns>
        private DeviceReader FindDeviceReaderForAddress(IEC104Address address)
        {
            if (address == null)
                return null;

            // Tìm reader có cùng Common Address
            return deviceReaders.FirstOrDefault(dr =>
                dr.Settings is IEC104DeviceSettings iec104Settings &&
                iec104Settings.CommonAddress == address.CommonAddress);
        }

        /// <summary>
        /// Tìm Client Adapter cho address
        /// </summary>
        /// <param name="address">IEC104 Address</param>
        /// <returns>Client Adapter hoặc null</returns>
        private IEC104ClientAdapter FindClientAdapterForAddress(IEC104Address address)
        {
            if (address == null)
                return null;

            var deviceReader = FindDeviceReaderForAddress(address);
            if (deviceReader != null && deviceReader.Settings is IEC104DeviceSettings settings)
            {
                return GetClientAdapter(settings.ClientID) as IEC104ClientAdapter;
            }
            return null;
        }

        // --- FIX: Chuyển đổi kiểu an toàn ---
        // Lý do: Tránh lỗi InvalidCastException khi dữ liệu nhận được có kiểu không mong muốn.
        private object ConvertValueByDataType(object value, IEC104DataType dataType)
        {
            if (value == null) return null;

            try
            {
                switch (dataType)
                {
                    case IEC104DataType.SinglePoint:
                    case IEC104DataType.SingleCommand:
                        if (value is bool b) return b;
                        return Convert.ToBoolean(value);

                    case IEC104DataType.DoublePoint:
                    case IEC104DataType.DoubleCommand:
                        if (value is DoublePointState state) return state;
                        return (DoublePointState)Convert.ToByte(value);

                    case IEC104DataType.FloatValue:
                    case IEC104DataType.FloatSetpoint:
                        if (value is float f) return f;
                        return Convert.ToSingle(value);

                    case IEC104DataType.ScaledValue:
                    case IEC104DataType.IntegratedTotals:
                        if (value is int i) return i;
                        return Convert.ToInt32(value);

                    case IEC104DataType.Bitstring32:
                        if (value is uint u) return u;
                        return Convert.ToUInt32(value);

                    default:
                        return value;
                }
            }
            catch
            {
                return value; // Trả về giá trị gốc nếu chuyển đổi thất bại
            }
        }
        #endregion

        #region STATUS & MONITORING
        /// <summary>
        /// Status monitor callback
        /// </summary>
        /// <param name="state">Timer state</param>
        private void StatusMonitorCallback(object state)
        {
            if (isDisposed) return;
            try
            {
                // Update statistics
                UpdateStatistics();
            }
            catch (Exception ex)
            {
                LastError = $"Status monitor error: {ex.Message}";
            }
        }

        /// <summary>
        /// Update statistics
        /// </summary>
        private void UpdateStatistics()
        {
            // Calculate total tags read and errors from all device readers
            TotalTagsRead = deviceReaders.Sum(dr => (long)dr.ReadCount);
            TotalErrors = deviceReaders.Sum(dr => (long)dr.ErrorCount);
        }

        /// <summary>
        /// Lấy thông tin trạng thái driver
        /// </summary>
        /// <returns>Thông tin trạng thái</returns>
        public string GetDriverStatus()
        {
            var uptime = DateTime.Now - StartTime;
            var status = IsRunning ? "Running" : "Stopped";

            return $"IEC104Driver - {status} | " +
                   $"Uptime: {uptime.Days}d {uptime.Hours:D2}h {uptime.Minutes:D2}m | " +
                   $"Devices: {DeviceReadersCount} | " +
                   $"Connections: {ClientAdaptersCount} | " +
                   $"Tags: {AddressMappingCount} | " +
                   $"Reads: {TotalTagsRead} | " +
                   $"Errors: {TotalErrors}";
        }
        #endregion

        #region DISPOSE
        /// <summary>
        /// Dispose driver
        /// </summary>
        public void Dispose()
        {
            if (!isDisposed)
            {
                isDisposed = true;
                Stop();

                // Dispose status monitor timer
                statusMonitorTimer?.Dispose();

                lock (editLock)
                {
                    // Dispose all device readers
                    foreach (var deviceReader in deviceReaders)
                    {
                        deviceReader?.Dispose();
                    }
                    deviceReaders.Clear();

                    // Dispose all client adapters
                    foreach (var clientAdapter in clientAdapters)
                    {
                        clientAdapter?.Dispose();
                    }
                    clientAdapters.Clear();

                    // Clear mappings
                    deviceSettingMapping.Clear();
                    addressMapping.Clear();
                }
            }
        }
        #endregion
    }

    #region SUPPORTING INTERFACES & CLASSES
    /// <summary>
    /// Interface for AT Driver
    /// </summary>
    public interface IATDriver
    {
        bool Initialize();
        bool Start();
        bool Stop();
        bool SetDevice(string deviceName, string deviceID);
        bool SetTag(string tagName, string deviceName, string tagAddress, string tagType);
        bool ReadTag(string tagName, out object value);
        bool WriteTag(string tagName, object value);
        bool ReadTags(string[] tagNames, out object[] values);
        bool WriteTags(string[] tagNames, object[] values);
    }

    // Các lớp Statistics không thay đổi nên được giữ nguyên
    #endregion
}