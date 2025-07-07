using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using ATDriver_Server;
using IEC104;

namespace ModbusIEC104
{
    /// <summary>
    /// Lớp Driver chính, thực thi giao diện IATDriver.
    /// Quản lý toàn bộ vòng đời của các kết nối, thiết bị và các tag.
    /// </summary>
    public class IEC104Driver : IATDriver, IDisposable
    {
        #region FIELDS
        private readonly List<DeviceReader> deviceReaders;
        private readonly Dictionary<string, IEC104Address> addressMapping; // Key: TagName
        private readonly List<IEC104ClientAdapter> clientAdapters;
        private readonly object editLock = new object();
        private readonly Timer statusMonitorTimer;
        private volatile bool isDisposed = false;
        #endregion

        #region PROPERTIES
        public string DriverName => "IEC104Driver";
        public string DriverVersion => "1.0.1"; // Updated version
        public bool IsRunning { get; private set; }
        public DateTime StartTime { get; private set; }
        public string LastError { get; private set; }
        public long TotalTagsRead { get; private set; }
        public long TotalErrors { get; private set; }
        #endregion

        #region CONSTRUCTORS
        public IEC104Driver()
        {
            deviceReaders = new List<DeviceReader>();
            addressMapping = new Dictionary<string, IEC104Address>();
            clientAdapters = new List<IEC104ClientAdapter>();
            statusMonitorTimer = new Timer(StatusMonitorCallback, null, 10000, 15000); // Mỗi 15 giây
        }
        #endregion

        #region IATDRIVER INTERFACE IMPLEMENTATION
        public bool Initialize()
        {
            if (IsRunning) return true;
            StartTime = DateTime.Now;
            IsRunning = true;
            return true;
        }

        public bool Start()
        {
            if (!IsRunning) Initialize();
            try
            {
                lock (editLock)
                {
                    foreach (var deviceReader in deviceReaders)
                    {
                        deviceReader.Start();
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                LastError = $"Driver Start failed: {ex.Message}";
                return false;
            }
        }

        public bool Stop()
        {
            if (!IsRunning) return true;
            try
            {
                lock (editLock)
                {
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
                LastError = $"Driver Stop failed: {ex.Message}";
                return false;
            }
        }

        public bool SetDevice(string deviceName, string deviceID)
        {
            if (string.IsNullOrEmpty(deviceName) || string.IsNullOrEmpty(deviceID))
            {
                LastError = "Device name and ID cannot be null or empty.";
                return false;
            }

            try
            {
                var deviceSettings = IEC104DeviceSettings.Initialize(deviceID);
                if (!deviceSettings.IsValid())
                {
                    LastError = $"Invalid device settings for device '{deviceName}'.";
                    return false;
                }

                lock (editLock)
                {
                    // Tạo Client Adapter nếu nó chưa tồn tại cho ClientID này
                    var clientAdapter = clientAdapters.FirstOrDefault(ca => ca.ClientID == deviceSettings.ClientID);
                    if (clientAdapter == null)
                    {
                        clientAdapter = new IEC104ClientAdapter(deviceSettings);
                        clientAdapters.Add(clientAdapter);
                    }

                    // Tìm hoặc tạo Device Reader
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
                        // Nếu đã tồn tại, cập nhật lại settings
                        deviceReader.Stop();
                        deviceReader.Settings = deviceSettings;
                        deviceReader.Initialize();
                    }

                    // Nếu driver đang chạy, khởi động device reader này
                    if (IsRunning)
                    {
                        deviceReader.Start();
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                LastError = $"SetDevice '{deviceName}' failed: {ex.Message}";
                return false;
            }
        }

        public bool SetTag(string tagName, string deviceName, string tagAddress, string tagType)
        {
            if (string.IsNullOrEmpty(tagName) || string.IsNullOrEmpty(tagAddress))
            {
                LastError = "Tag name and address cannot be null or empty.";
                return false;
            }

            try
            {
                var address = new IEC104Address(tagAddress);
                if (!address.IsValid())
                {
                    LastError = $"Invalid IEC104 address format: {tagAddress}";
                    return false;
                }

                // Gán deviceName vào tag để tiện truy xuất sau này
                address.DeviceName = deviceName;
                addressMapping[tagName] = address;
                return true;
            }
            catch (Exception ex)
            {
                LastError = $"SetTag '{tagName}' failed: {ex.Message}";
                return false;
            }
        }

        public bool ReadTag(string tagName, out object value)
        {
            value = null;

            if (!addressMapping.TryGetValue(tagName, out IEC104Address address))
            {
                LastError = $"Tag '{tagName}' not found.";
                return false;
            }

            var deviceReader = deviceReaders.FirstOrDefault(dr => dr.DeviceName == address.DeviceName);
            if (deviceReader == null)
            {
                LastError = $"Device '{address.DeviceName}' for tag '{tagName}' not found.";
                return false;
            }

            var infoObject = deviceReader.GetSingleDataPoint(address.InformationObjectAddress);
            if (infoObject != null)
            {
                if (infoObject.IsGoodQuality())
                {
                    value = ConvertValueByDataType(infoObject.Value, address.DataType);
                    TotalTagsRead++;
                    return true;
                }
                LastError = $"Tag '{tagName}' has bad quality: {infoObject.Quality:X2}";
                TotalErrors++;
                return false;
            }

            // Dữ liệu chưa có sẵn trong cache, đây không phải là lỗi
            LastError = $"Data for tag '{tagName}' is not yet available.";
            return false;
        }

        public bool WriteTag(string tagName, object value)
        {
            if (!addressMapping.TryGetValue(tagName, out IEC104Address address))
            {
                LastError = $"Tag '{tagName}' not found.";
                return false;
            }

            if (!address.IsCommandType)
            {
                LastError = $"Tag '{tagName}' is not a command type and cannot be written.";
                return false;
            }

            var deviceReader = deviceReaders.FirstOrDefault(dr => dr.DeviceName == address.DeviceName);
            if (deviceReader == null)
            {
                LastError = $"Device '{address.DeviceName}' for tag '{tagName}' not found.";
                return false;
            }

            var clientAdapter = deviceReader.GetClientAdapter() as IEC104ClientAdapter;
            if (clientAdapter == null || !clientAdapter.IsConnected)
            {
                LastError = $"No active connection for device '{address.DeviceName}'.";
                return false;
            }

            if (clientAdapter.SendCommand(address.CommonAddress, address.InformationObjectAddress, address.TypeIdentification, value))
            {
                return true;
            }

            LastError = $"WriteTag '{tagName}' failed. Adapter error: {clientAdapter.LastError}";
            TotalErrors++;
            return false;
        }

        // Các phương thức ReadTags và WriteTags không thay đổi
        public bool ReadTags(string[] tagNames, out object[] values)
        {
            values = new object[tagNames?.Length ?? 0];
            if (tagNames == null || tagNames.Length == 0) return false;

            bool allSuccess = true;
            for (int i = 0; i < tagNames.Length; i++)
            {
                if (!ReadTag(tagNames[i], out values[i]))
                {
                    allSuccess = false;
                }
            }
            return allSuccess;
        }

        public bool WriteTags(string[] tagNames, object[] values)
        {
            if (tagNames == null || values == null || tagNames.Length != values.Length) return false;

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

        #region INTERNAL METHODS
        public ClientAdapter GetClientAdapter(string clientID)
        {
            lock (editLock)
            {
                return clientAdapters.FirstOrDefault(ca => ca.ClientID == clientID);
            }
        }

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
                        if (int.TryParse(value.ToString(), out int i)) return i != 0;
                        return Convert.ToBoolean(value);

                    case IEC104DataType.DoublePoint:
                    case IEC104DataType.DoubleCommand:
                        if (value is DoublePointValue dpv) return dpv;
                        return (DoublePointValue)Convert.ToByte(value);

                    case IEC104DataType.FloatValue:
                    case IEC104DataType.FloatSetpoint:
                        if (value is float f) return f;
                        return Convert.ToSingle(value);

                    case IEC104DataType.ScaledValue:
                    case IEC104DataType.IntegratedTotals:
                        if (value is int intVal) return intVal;
                        return Convert.ToInt32(value);

                    case IEC104DataType.Bitstring32:
                        if (value is uint u) return u;
                        return Convert.ToUInt32(value);

                    default:
                        return value;
                }
            }
            catch { return value; } // Trả về giá trị gốc nếu chuyển đổi thất bại
        }

        private void StatusMonitorCallback(object state)
        {
            if (isDisposed) return;
            // Công việc của timer này là giám sát, ghi log trạng thái (nếu cần),
            // việc kết nối lại đã được xử lý trong ClientAdapter.
            // Có thể thêm logic cập nhật thống kê ở đây.
        }
        #endregion

        #region DISPOSE
        public void Dispose()
        {
            if (!isDisposed)
            {
                isDisposed = true;
                Stop();
                statusMonitorTimer?.Dispose();

                lock (editLock)
                {
                    foreach (var deviceReader in deviceReaders) deviceReader.Dispose();
                    deviceReaders.Clear();

                    foreach (var clientAdapter in clientAdapters) clientAdapter.Dispose();
                    clientAdapters.Clear();

                    addressMapping.Clear();
                }
            }
        }
        #endregion
    }
}