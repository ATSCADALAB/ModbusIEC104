using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IEC104;
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
            statusMonitorTimer = new Timer(StatusMonitorCallback, null, 60000, 60000); // Every minute

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
                if (IsRunning)
                    return true;

                // Driver initialization logic here
                // Có thể load config, validate settings, etc.

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
                    if (!Initialize())
                        return false;
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

                    // Remove old device reader if different settings
                    RemoveDeviceReaderIfNotUse(deviceName, deviceSettings);

                    // Create new device reader if needed
                    CreateDeviceReaderIfNotExist(deviceName, deviceID, deviceSettings);

                    // Update device reader if settings changed
                    UpdateDeviceReaderIfChanged(deviceName, deviceSettings);
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

                // Get value from device reader's last read data
                value = GetTagValueFromDeviceReader(deviceReader, address);

                if (value != null)
                {
                    TotalTagsRead++;
                    return true;
                }
                else
                {
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
                if (clientAdapter == null)
                {
                    LastError = $"No client adapter found for tag: {tagName}";
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
                    LastError = $"Write command failed for tag: {tagName}";
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

            try
            {
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
                        values[i] = null;
                    }
                    else
                    {
                        values[i] = value;
                    }
                }

                return allSuccess;
            }
            catch (Exception ex)
            {
                LastError = $"ReadTags failed: {ex.Message}";
                return false;
            }
        }

        /// <summary>
        /// Ghi nhiều tag cùng lúc
        /// </summary>
        /// <param name="tagNames">Danh sách tên tag</param>
        /// <param name="values">Giá trị cần ghi</param>
        /// <returns>True nếu thành công</returns>
        public bool WriteTags(string[] tagNames, object[] values)
        {
            try
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
            catch (Exception ex)
            {
                LastError = $"WriteTags failed: {ex.Message}";
                return false;
            }
        }
        #endregion

        #region DEVICE READER MANAGEMENT
        /// <summary>
        /// Tạo Device Reader nếu chưa có
        /// </summary>
        /// <param name="deviceName">Tên device</param>
        /// <param name="deviceID">Device ID</param>
        /// <param name="deviceSettings">Device settings</param>
        /// <returns>True nếu thành công</returns>
        private bool CreateDeviceReaderIfNotExist(string deviceName, string deviceID, IEC104DeviceSettings deviceSettings)
        {
            try
            {
                var index = deviceReaders.FindIndex(x => x.DeviceID == deviceID);
                if (index < 0)
                {
                    lock (editLock)
                    {
                        var deviceReader = new DeviceReader(this)
                        {
                            DeviceName = deviceName,
                            DeviceID = deviceID,
                            Settings = deviceSettings
                        };

                        deviceReaders.Add(deviceReader);
                        deviceReader.Initialize();

                        // Create client adapter if needed
                        AddIEC104ClientAdapter(deviceSettings);
                    }

                    return true;
                }
            }
            catch (Exception ex)
            {
                LastError = $"CreateDeviceReaderIfNotExist failed: {ex.Message}";
            }
            return false;
        }

        /// <summary>
        /// Xóa Device Reader nếu không sử dụng
        /// </summary>
        /// <param name="deviceName">Tên device</param>
        /// <param name="deviceSettings">Device settings</param>
        /// <returns>True nếu thành công</returns>
        private bool RemoveDeviceReaderIfNotUse(string deviceName, IEC104DeviceSettings deviceSettings)
        {
            try
            {
                var index = deviceReaders.FindIndex(x =>
                    x.DeviceName == deviceName &&
                    (x.Settings.IpAddress != deviceSettings.IpAddress ||
                     x.Settings.Port != deviceSettings.Port ||
                     ((IEC104DeviceSettings)x.Settings).CommonAddress != deviceSettings.CommonAddress));

                if (index > -1)
                {
                    var deviceReader = deviceReaders[index];
                    deviceReaders.Remove(deviceReader);

                    // Remove client adapter if not used by other device readers
                    if (TryGetClientAdapter(deviceSettings.ClientID, out IEC104ClientAdapter adapter))
                    {
                        if (!deviceReaders.Any(dr => ((IEC104DeviceSettings)dr.Settings).ClientID == deviceSettings.ClientID))
                        {
                            lock (editLock)
                            {
                                clientAdapters.Remove(adapter);
                                adapter?.Dispose();
                            }
                        }
                    }

                    deviceReader.Dispose();
                    return true;
                }
            }
            catch (Exception ex)
            {
                LastError = $"RemoveDeviceReaderIfNotUse failed: {ex.Message}";
            }
            return false;
        }

        /// <summary>
        /// Update Device Reader nếu settings thay đổi
        /// </summary>
        /// <param name="deviceName">Tên device</param>
        /// <param name="deviceSettings">Device settings</param>
        /// <returns>True nếu thành công</returns>
        private bool UpdateDeviceReaderIfChanged(string deviceName, IEC104DeviceSettings deviceSettings)
        {
            try
            {
                var index = deviceReaders.FindIndex(x =>
                    x.DeviceName == deviceName &&
                    x.Settings.IpAddress == deviceSettings.IpAddress &&
                    x.Settings.Port == deviceSettings.Port &&
                    ((IEC104DeviceSettings)x.Settings).CommonAddress == deviceSettings.CommonAddress &&
                    !AreBlockSettingsEqual(x.Settings.BlockSettings, deviceSettings.BlockSettings));

                if (index > -1)
                {
                    lock (editLock)
                    {
                        var deviceReader = deviceReaders[index];
                        deviceReader.Settings = deviceSettings;
                        deviceReader.Initialize(); // Re-initialize with new settings
                    }
                    return true;
                }
            }
            catch (Exception ex)
            {
                LastError = $"UpdateDeviceReaderIfChanged failed: {ex.Message}";
            }
            return false;
        }

        /// <summary>
        /// So sánh block settings
        /// </summary>
        /// <param name="settings1">Settings 1</param>
        /// <param name="settings2">Settings 2</param>
        /// <returns>True nếu giống nhau</returns>
        private bool AreBlockSettingsEqual(List<BlockSettings> settings1, List<BlockSettings> settings2)
        {
            if (settings1 == null && settings2 == null) return true;
            if (settings1 == null || settings2 == null) return false;
            if (settings1.Count != settings2.Count) return false;

            for (int i = 0; i < settings1.Count; i++)
            {
                if (settings1[i].BlockID != settings2[i].BlockID)
                    return false;
            }

            return true;
        }
        #endregion

        #region CLIENT ADAPTER MANAGEMENT
        /// <summary>
        /// Thêm IEC104 Client Adapter
        /// </summary>
        /// <param name="settings">Device settings</param>
        /// <returns>Client Adapter</returns>
        private IEC104ClientAdapter AddIEC104ClientAdapter(IEC104DeviceSettings settings)
        {
            try
            {
                // Kiểm tra xem đã có client adapter cho connection này chưa
                var existingAdapter = clientAdapters.FirstOrDefault(ca => ca.ClientID == settings.ClientID);
                if (existingAdapter != null)
                    return existingAdapter;

                lock (editLock)
                {
                    var clientAdapter = new IEC104ClientAdapter(settings);
                    clientAdapters.Add(clientAdapter);
                    return clientAdapter;
                }
            }
            catch (Exception ex)
            {
                LastError = $"AddIEC104ClientAdapter failed: {ex.Message}";
                return null;
            }
        }

        /// <summary>
        /// Lấy Client Adapter theo ClientID
        /// </summary>
        /// <param name="clientID">Client ID</param>
        /// <param name="adapter">Client Adapter</param>
        /// <returns>True nếu tìm thấy</returns>
        private bool TryGetClientAdapter(string clientID, out IEC104ClientAdapter adapter)
        {
            adapter = clientAdapters.FirstOrDefault(ca => ca.ClientID == clientID);
            return adapter != null;
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

            return clientAdapters.FirstOrDefault(ca =>
                ca.CommonAddress == address.CommonAddress);
        }

        /// <summary>
        /// Lấy giá trị tag từ Device Reader
        /// </summary>
        /// <param name="deviceReader">Device Reader</param>
        /// <param name="address">IEC104 Address</param>
        /// <returns>Giá trị tag hoặc null</returns>
        private object GetTagValueFromDeviceReader(DeviceReader deviceReader, IEC104Address address)
        {
            try
            {
                // Lấy dữ liệu từ tất cả BlockReader thuộc DeviceReader này
                var allData = deviceReader.GetAllLastReadData(); // <-- Hàm mới cần thêm vào DeviceReader

                // Tìm kiếm đối tượng thông tin khớp với địa chỉ và loại dữ liệu
                // Tìm từ cuối danh sách để lấy giá trị mới nhất nếu có trùng lặp
                var infoObject = allData.LastOrDefault(obj =>
                    obj.ObjectAddress == address.InformationObjectAddress &&
                    obj.TypeId == address.TypeIdentification);

                if (infoObject != null && infoObject.IsGoodQuality())
                {
                    return ConvertValueByDataType(infoObject.Value, address.DataType);
                }

                return null;
            }
            catch (Exception ex)
            {
                LastError = $"GetTagValueFromDeviceReader failed: {ex.Message}";
                return null;
            }
        }

        /// <summary>
        /// Convert giá trị theo data type
        /// </summary>
        /// <param name="value">Giá trị gốc</param>
        /// <param name="dataType">Data type</param>
        /// <returns>Giá trị đã convert</returns>
        private object ConvertValueByDataType(object value, IEC104DataType dataType)
        {
            if (value == null)
                return null;

            try
            {
                switch (dataType)
                {
                    case IEC104DataType.SinglePoint:
                        return Convert.ToBoolean(value);

                    case IEC104DataType.DoublePoint:
                        return value is DoublePointState state ? (int)state : Convert.ToInt32(value);

                    case IEC104DataType.StepPosition:
                        return Convert.ToSByte(value);

                    case IEC104DataType.Bitstring32:
                        return Convert.ToUInt32(value);

                    case IEC104DataType.NormalizedValue:
                    case IEC104DataType.FloatValue:
                        return Convert.ToSingle(value);

                    case IEC104DataType.ScaledValue:
                    case IEC104DataType.IntegratedTotals:
                        return Convert.ToInt32(value);

                    default:
                        return value;
                }
            }
            catch
            {
                return value; // Return original value if conversion fails
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
            try
            {
                // Monitor client adapters and reconnect if needed
                Parallel.ForEach(clientAdapters, adapter =>
                {
                    if (!adapter.IsConnected)
                    {
                        Task.Run(() => adapter.Connect());
                    }
                });

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
            TotalTagsRead = deviceReaders.Sum(dr => dr.ReadCount);
            TotalErrors = deviceReaders.Sum(dr => dr.ErrorCount);
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

        /// <summary>
        /// Lấy thống kê chi tiết
        /// </summary>
        /// <returns>Thống kê driver</returns>
        public DriverStatistics GetDriverStatistics()
        {
            var uptime = DateTime.Now - StartTime;

            return new DriverStatistics
            {
                DriverName = DriverName,
                DriverVersion = DriverVersion,
                IsRunning = IsRunning,
                StartTime = StartTime,
                Uptime = uptime,
                LastError = LastError,

                DeviceReadersCount = DeviceReadersCount,
                ClientAdaptersCount = ClientAdaptersCount,
                AddressMappingCount = AddressMappingCount,

                TotalTagsRead = TotalTagsRead,
                TotalErrors = TotalErrors,
                SuccessRate = TotalTagsRead + TotalErrors > 0 ?
                    (double)TotalTagsRead / (TotalTagsRead + TotalErrors) * 100 : 100,

                DeviceReaderStatistics = deviceReaders.Select(dr => dr.GetDiagnosticInfo()).ToList(),
                ClientAdapterStatistics = clientAdapters.Select(ca => ca.GetIEC104Statistics()).ToList()
            };
        }

        /// <summary>
        /// Lấy danh sách device readers
        /// </summary>
        /// <returns>Danh sách device readers</returns>
        public List<DeviceReader> GetDeviceReaders()
        {
            lock (editLock)
            {
                return new List<DeviceReader>(deviceReaders);
            }
        }

        /// <summary>
        /// Lấy danh sách client adapters
        /// </summary>
        /// <returns>Danh sách client adapters</returns>
        public List<IEC104ClientAdapter> GetClientAdapters()
        {
            lock (editLock)
            {
                return new List<IEC104ClientAdapter>(clientAdapters);
            }
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

                IsRunning = false;
                isDisposed = true;
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

    /// <summary>
    /// Driver Statistics
    /// </summary>
    public class DriverStatistics
    {
        public string DriverName { get; set; }
        public string DriverVersion { get; set; }
        public bool IsRunning { get; set; }
        public DateTime StartTime { get; set; }
        public TimeSpan Uptime { get; set; }
        public string LastError { get; set; }

        public int DeviceReadersCount { get; set; }
        public int ClientAdaptersCount { get; set; }
        public int AddressMappingCount { get; set; }

        public long TotalTagsRead { get; set; }
        public long TotalErrors { get; set; }
        public double SuccessRate { get; set; }

        public List<Dictionary<string, object>> DeviceReaderStatistics { get; set; }
        public List<IEC104ClientAdapterStatistics> ClientAdapterStatistics { get; set; }

        public override string ToString()
        {
            return $"IEC104Driver[{DriverName} v{DriverVersion}] - " +
                   $"{(IsRunning ? "Running" : "Stopped")} | " +
                   $"Uptime: {Uptime.Days}d {Uptime.Hours:D2}h {Uptime.Minutes:D2}m | " +
                   $"Devices: {DeviceReadersCount} | " +
                   $"Success Rate: {SuccessRate:F1}%";
        }
    }
    #endregion
}