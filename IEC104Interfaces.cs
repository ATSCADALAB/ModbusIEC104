using System;
using System.Collections.Generic;

namespace ModbusIEC104.Common
{
    /// <summary>
    /// Base Client Adapter interface - MISSING INTERFACE FIXED
    /// </summary>
    public interface IClientAdapter : IDisposable
    {
        /// <summary>Client ID</summary>
        string ClientID { get; }

        /// <summary>Có kết nối không</summary>
        bool IsConnected { get; }

        /// <summary>Kết nối</summary>
        bool Connect();

        /// <summary>Ngắt kết nối</summary>
        bool Disconnect();

        /// <summary>Kiểm tra kết nối</summary>
        bool CheckConnection();

        /// <summary>Lỗi cuối</summary>
        string LastError { get; }
    }

    /// <summary>
    /// IEC104 specific interface - MISSING INTERFACE FIXED
    /// </summary>
    public interface IIEC104ClientAdapter : IClientAdapter
    {
        /// <summary>Trạng thái kết nối IEC104</summary>
        IEC104.IEC104ConnectionState ConnectionState { get; }

        /// <summary>Common Address</summary>
        ushort CommonAddress { get; }

        /// <summary>Gửi Interrogation</summary>
        bool SendInterrogation(ushort commonAddress, IEC104.InterrogationType type);

        /// <summary>Gửi Command</summary>
        bool SendCommand(ushort commonAddress, uint ioa, byte typeId, object value, bool selectBeforeOperate = false);

        /// <summary>Xử lý dữ liệu tự phát</summary>
        bool ProcessSpontaneousData(out List<IEC104.InformationObject> objects);
    }

    /// <summary>
    /// Device Reader interface - MISSING INTERFACE FIXED
    /// </summary>
    public interface IDeviceReader : IDisposable
    {
        /// <summary>Device name</summary>
        string DeviceName { get; }

        /// <summary>Device ID</summary>
        string DeviceID { get; }

        /// <summary>Có đang chạy không</summary>
        bool IsRunning { get; }

        /// <summary>Khởi tạo</summary>
        bool Initialize();

        /// <summary>Bắt đầu</summary>
        bool Start();

        /// <summary>Dừng</summary>
        bool Stop();

        /// <summary>Đọc tất cả blocks</summary>
        bool ReadAllBlocks();
    }

    /// <summary>
    /// Block Reader interface - MISSING INTERFACE FIXED
    /// </summary>
    public interface IBlockReader : IDisposable
    {
        /// <summary>Block name</summary>
        string BlockName { get; }

        /// <summary>Block có enabled không</summary>
        bool Enabled { get; }

        /// <summary>Khởi tạo block</summary>
        bool Initialize();

        /// <summary>Đọc block</summary>
        bool ReadBlock();

        /// <summary>Dừng block</summary>
        void Stop();
    }
}

namespace IEC104
{
    /// <summary>
    /// Information Object với extended properties - FIXED MISSING TYPE
    /// </summary>
    public class InformationObject
    {
        #region PROPERTIES
        /// <summary>Information Object Address (3 bytes)</summary>
        public uint InformationObjectAddress { get; set; }

        /// <summary>Type Identification</summary>
        public byte TypeID { get; set; }

        /// <summary>Element data (nội dung thực tế của object)</summary>
        public byte[] ElementData { get; set; }

        /// <summary>Parsed value - ADDED PROPERTY</summary>
        public object Value { get; set; }

        /// <summary>Quality descriptor - ADDED PROPERTY</summary>
        public byte Quality { get; set; }

        /// <summary>Timestamp - ADDED PROPERTY</summary>
        public DateTime TimeStamp { get; set; }

        /// <summary>Cause of Transmission - ADDED PROPERTY</summary>
        public byte CauseOfTransmission { get; set; }

        /// <summary>Common Address - ADDED PROPERTY</summary>
        public ushort CommonAddress { get; set; }
        #endregion

        #region CONSTRUCTORS
        /// <summary>
        /// Constructor mặc định
        /// </summary>
        public InformationObject()
        {
            ElementData = new byte[0];
            TimeStamp = DateTime.Now;
        }

        /// <summary>
        /// Constructor với basic parameters
        /// </summary>
        /// <param name="ioa">Information Object Address</param>
        /// <param name="typeId">Type ID</param>
        /// <param name="elementData">Element data</param>
        public InformationObject(uint ioa, byte typeId, byte[] elementData)
        {
            InformationObjectAddress = ioa;
            TypeID = typeId;
            ElementData = elementData ?? new byte[0];
            TimeStamp = DateTime.Now;
            ParseValue();
        }
        #endregion

        #region METHODS
        /// <summary>
        /// Parse value từ ElementData dựa trên TypeID - ADDED METHOD
        /// </summary>
        public void ParseValue()
        {
            if (ElementData == null || ElementData.Length == 0)
                return;

            try
            {
                switch (TypeID)
                {
                    case IEC104Constants.M_SP_NA_1: // Single Point
                        if (ElementData.Length >= 1)
                        {
                            Value = (ElementData[0] & 0x01) != 0;
                            Quality = (byte)(ElementData[0] & 0xFE);
                        }
                        break;

                    case IEC104Constants.M_DP_NA_1: // Double Point
                        if (ElementData.Length >= 1)
                        {
                            Value = (DoublePointValue)(ElementData[0] & 0x03);
                            Quality = (byte)(ElementData[0] & 0xFC);
                        }
                        break;

                    case IEC104Constants.M_ME_NC_1: // Float Value
                        if (ElementData.Length >= 5)
                        {
                            Value = BitConverter.ToSingle(ElementData, 0);
                            Quality = ElementData[4];
                        }
                        break;

                    case IEC104Constants.M_ME_NA_1: // Normalized Value
                    case IEC104Constants.M_ME_NB_1: // Scaled Value
                        if (ElementData.Length >= 3)
                        {
                            Value = BitConverter.ToInt16(ElementData, 0);
                            Quality = ElementData[2];
                        }
                        break;

                    case IEC104Constants.M_BO_NA_1: // Bitstring 32
                        if (ElementData.Length >= 5)
                        {
                            Value = BitConverter.ToUInt32(ElementData, 0);
                            Quality = ElementData[4];
                        }
                        break;

                    case IEC104Constants.M_IT_NA_1: // Integrated Totals
                        if (ElementData.Length >= 5)
                        {
                            Value = BitConverter.ToUInt32(ElementData, 0);
                            Quality = ElementData[4];
                        }
                        break;

                    default:
                        Value = ElementData;
                        Quality = 0;
                        break;
                }
            }
            catch
            {
                Value = null;
                Quality = 0x80; // Invalid
            }
        }

        /// <summary>
        /// Kiểm tra quality có tốt không - ADDED METHOD
        /// </summary>
        /// <returns>True nếu quality tốt</returns>
        public bool IsGoodQuality()
        {
            return (Quality & 0x80) == 0 && // Not invalid
                   (Quality & 0x40) == 0;   // Not not-topical
        }

        /// <summary>
        /// Get quality descriptor - ADDED METHOD
        /// </summary>
        /// <returns>Quality descriptor</returns>
        public IEC104QualityDescriptor GetQualityDescriptor()
        {
            return new IEC104QualityDescriptor(Quality);
        }

        /// <summary>
        /// Convert value to string - ADDED METHOD
        /// </summary>
        /// <returns>String representation of value</returns>
        public string GetValueAsString()
        {
            if (Value == null)
                return "NULL";

            switch (TypeID)
            {
                case IEC104Constants.M_SP_NA_1:
                case IEC104Constants.C_SC_NA_1:
                    return ((bool)Value) ? "1" : "0";

                case IEC104Constants.M_DP_NA_1:
                case IEC104Constants.C_DC_NA_1:
                    return ((int)(DoublePointValue)Value).ToString();

                case IEC104Constants.M_ME_NC_1:
                case IEC104Constants.C_SE_NC_1:
                    return ((float)Value).ToString("F6");

                default:
                    return Value.ToString();
            }
        }

        /// <summary>
        /// Lấy thông tin object dưới dạng string để debug
        /// </summary>
        /// <returns>String mô tả object</returns>
        public override string ToString()
        {
            return $"InfoObj: IOA={InformationObjectAddress}, TypeID={TypeID}, " +
                   $"Value={GetValueAsString()}, Quality={Quality:X2}, Data={ElementData?.Length ?? 0} bytes";
        }
        #endregion
    }

    /// <summary>
    /// Information Object Collection - ADDED UTILITY CLASS
    /// </summary>
    public class InformationObjectCollection : List<InformationObject>
    {
        /// <summary>
        /// Find by IOA
        /// </summary>
        /// <param name="ioa">Information Object Address</param>
        /// <returns>Found object or null</returns>
        public InformationObject FindByIOA(uint ioa)
        {
            return Find(obj => obj.InformationObjectAddress == ioa);
        }

        /// <summary>
        /// Find by Type ID
        /// </summary>
        /// <param name="typeId">Type ID</param>
        /// <returns>List of matching objects</returns>
        public List<InformationObject> FindByTypeID(byte typeId)
        {
            return FindAll(obj => obj.TypeID == typeId);
        }

        /// <summary>
        /// Get all with good quality
        /// </summary>
        /// <returns>List of objects with good quality</returns>
        public List<InformationObject> GetGoodQualityObjects()
        {
            return FindAll(obj => obj.IsGoodQuality());
        }

        /// <summary>
        /// Get all monitoring objects
        /// </summary>
        /// <returns>List of monitoring objects</returns>
        public List<InformationObject> GetMonitoringObjects()
        {
            return FindAll(obj => IEC104Address.IsMonitoringType(obj.TypeID));
        }

        /// <summary>
        /// Get all command objects
        /// </summary>
        /// <returns>List of command objects</returns>
        public List<InformationObject> GetCommandObjects()
        {
            return FindAll(obj => IEC104Address.IsControlType(obj.TypeID));
        }

        /// <summary>
        /// Update object by IOA
        /// </summary>
        /// <param name="updatedObject">Updated object</param>
        /// <returns>True if updated, false if added</returns>
        public bool UpdateByIOA(InformationObject updatedObject)
        {
            if (updatedObject == null)
                return false;

            int index = FindIndex(obj => obj.InformationObjectAddress == updatedObject.InformationObjectAddress);
            if (index >= 0)
            {
                this[index] = updatedObject;
                return true;
            }
            else
            {
                Add(updatedObject);
                return false;
            }
        }

        /// <summary>
        /// Remove by IOA
        /// </summary>
        /// <param name="ioa">Information Object Address</param>
        /// <returns>True if removed</returns>
        public bool RemoveByIOA(uint ioa)
        {
            return RemoveAll(obj => obj.InformationObjectAddress == ioa) > 0;
        }

        /// <summary>
        /// Export to dictionary
        /// </summary>
        /// <returns>Dictionary keyed by IOA</returns>
        public Dictionary<uint, InformationObject> ToDictionary()
        {
            var dict = new Dictionary<uint, InformationObject>();
            foreach (var obj in this)
            {
                dict[obj.InformationObjectAddress] = obj;
            }
            return dict;
        }
    }
}

namespace ModbusIEC104
{
    /// <summary>
    /// Block Reader base class - FIXED MISSING CLASS
    /// </summary>
    public class BlockReader : ModbusIEC104.Common.IBlockReader
    {
        #region FIELDS
        private readonly DeviceReader deviceReader;
        private readonly object lockObject = new object();
        private bool isDisposed = false;
        #endregion

        #region PROPERTIES
        /// <summary>Block name</summary>
        public string BlockName { get; protected set; }

        /// <summary>Block có enabled không</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>Block settings</summary>
        public object BlockSettings { get; protected set; }

        /// <summary>Last read time</summary>
        public DateTime LastReadTime { get; protected set; }

        /// <summary>Read count</summary>
        public int ReadCount { get; protected set; }

        /// <summary>Error count</summary>
        public int ErrorCount { get; protected set; }

        /// <summary>Last error</summary>
        public string LastError { get; protected set; }
        #endregion

        #region CONSTRUCTOR
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="deviceReader">Device reader</param>
        /// <param name="blockSettings">Block settings</param>
        public BlockReader(DeviceReader deviceReader, object blockSettings)
        {
            this.deviceReader = deviceReader ?? throw new ArgumentNullException(nameof(deviceReader));
            this.BlockSettings = blockSettings;
            this.BlockName = $"Block_{Guid.NewGuid().ToString("N")[..8]}";
        }
        #endregion

        #region INTERFACE IMPLEMENTATION
        /// <summary>
        /// Khởi tạo block
        /// </summary>
        /// <returns>True if successful</returns>
        public virtual bool Initialize()
        {
            try
            {
                // Override in derived classes for specific initialization
                return true;
            }
            catch (Exception ex)
            {
                LastError = $"Initialize failed: {ex.Message}";
                return false;
            }
        }

        /// <summary>
        /// Đọc block
        /// </summary>
        /// <returns>True if successful</returns>
        public virtual bool ReadBlock()
        {
            if (!Enabled || isDisposed)
                return false;

            try
            {
                lock (lockObject)
                {
                    // Override in derived classes for specific read logic
                    LastReadTime = DateTime.Now;
                    ReadCount++;
                    return true;
                }
            }
            catch (Exception ex)
            {
                LastError = $"ReadBlock failed: {ex.Message}";
                ErrorCount++;
                return false;
            }
        }

        /// <summary>
        /// Dừng block
        /// </summary>
        public virtual void Stop()
        {
            try
            {
                // Override in derived classes for specific stop logic
                Enabled = false;
            }
            catch (Exception ex)
            {
                LastError = $"Stop failed: {ex.Message}";
            }
        }

        /// <summary>
        /// Dispose
        /// </summary>
        public virtual void Dispose()
        {
            if (!isDisposed)
            {
                Stop();
                isDisposed = true;
            }
        }
        #endregion

        #region UTILITY METHODS
        /// <summary>
        /// Get device reader
        /// </summary>
        /// <returns>Device reader</returns>
        protected DeviceReader GetDeviceReader()
        {
            return deviceReader;
        }

        /// <summary>
        /// Get status
        /// </summary>
        /// <returns>Status string</returns>
        public string GetStatus()
        {
            return $"Block[{BlockName}] - {(Enabled ? "Enabled" : "Disabled")} | " +
                   $"Reads: {ReadCount} | Errors: {ErrorCount}";
        }

        /// <summary>
        /// Override ToString
        /// </summary>
        /// <returns>String representation</returns>
        public override string ToString()
        {
            return GetStatus();
        }
        #endregion
    }

    /// <summary>
    /// IEC104 specific Block Reader - ADDED CLASS
    /// </summary>
    public class IEC104BlockReader : BlockReader
    {
        #region ADDITIONAL PROPERTIES
        /// <summary>Common Address for this block</summary>
        public ushort CommonAddress { get; set; }

        /// <summary>Interrogation type</summary>
        public IEC104.InterrogationType InterrogationType { get; set; } = IEC104.InterrogationType.General;

        /// <summary>IOA filter list</summary>
        public HashSet<uint> FilteredIOAs { get; set; }

        /// <summary>Last interrogation time</summary>
        public DateTime LastInterrogationTime { get; private set; }
        #endregion

        #region CONSTRUCTOR
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="deviceReader">Device reader</param>
        /// <param name="blockSettings">Block settings</param>
        public IEC104BlockReader(DeviceReader deviceReader, object blockSettings)
            : base(deviceReader, blockSettings)
        {
            FilteredIOAs = new HashSet<uint>();
            ParseBlockSettings(blockSettings);
        }
        #endregion

        #region PRIVATE METHODS
        /// <summary>
        /// Parse block settings
        /// </summary>
        /// <param name="settings">Settings object</param>
        private void ParseBlockSettings(object settings)
        {
            // Parse settings based on type
            // Implementation depends on your settings format
            if (settings is string settingsString)
            {
                // Example: "CA1-General-1000-2000"
                var parts = settingsString.Split('-');
                if (parts.Length >= 2)
                {
                    if (ushort.TryParse(parts[0].Replace("CA", ""), out ushort ca))
                        CommonAddress = ca;

                    if (Enum.TryParse(parts[1], out IEC104.InterrogationType intType))
                        InterrogationType = intType;

                    // Parse IOA range if present
                    if (parts.Length >= 4)
                    {
                        if (uint.TryParse(parts[2], out uint startIOA) &&
                            uint.TryParse(parts[3], out uint endIOA))
                        {
                            for (uint ioa = startIOA; ioa <= endIOA; ioa++)
                            {
                                FilteredIOAs.Add(ioa);
                            }
                        }
                    }
                }
            }
        }
        #endregion

        #region OVERRIDE METHODS
        /// <summary>
        /// Initialize IEC104 block
        /// </summary>
        /// <returns>True if successful</returns>
        public override bool Initialize()
        {
            try
            {
                BlockName = $"IEC104Block_CA{CommonAddress}_{InterrogationType}";
                return base.Initialize();
            }
            catch (Exception ex)
            {
                LastError = $"IEC104 Initialize failed: {ex.Message}";
                return false;
            }
        }

        /// <summary>
        /// Read IEC104 block
        /// </summary>
        /// <returns>True if successful</returns>
        public override bool ReadBlock()
        {
            if (!base.ReadBlock())
                return false;

            try
            {
                var deviceReader = GetDeviceReader();
                if (deviceReader == null)
                    return false;

                // Check if interrogation is needed
                var timeSinceLastInterrogation = DateTime.Now - LastInterrogationTime;
                if (timeSinceLastInterrogation.TotalMinutes >= 5) // 5 minutes interval
                {
                    if (deviceReader.SendInterrogation(InterrogationType))
                    {
                        LastInterrogationTime = DateTime.Now;
                    }
                }

                // Process spontaneous data
                var spontaneousData = deviceReader.ProcessSpontaneousData();
                if (spontaneousData.Count > 0)
                {
                    // Filter data if needed
                    if (FilteredIOAs.Count > 0)
                    {
                        spontaneousData = spontaneousData.Where(obj =>
                            FilteredIOAs.Contains(obj.InformationObjectAddress)).ToList();
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                LastError = $"IEC104 ReadBlock failed: {ex.Message}";
                ErrorCount++;
                return false;
            }
        }
        #endregion
    }
}

// ADDITIONAL MISSING NAMESPACES AND CLASSES
namespace ATDriver_Server
{
    /// <summary>
    /// IATDriver interface - MISSING INTERFACE FIXED
    /// </summary>
    public interface IATDriver : IDisposable
    {
        /// <summary>Driver name</summary>
        string DriverName { get; }

        /// <summary>Driver version</summary>
        string DriverVersion { get; }

        /// <summary>Có đang chạy không</summary>
        bool IsRunning { get; }

        /// <summary>Initialize driver</summary>
        bool Initialize();

        /// <summary>Start driver</summary>
        bool Start();

        /// <summary>Stop driver</summary>
        bool Stop();

        /// <summary>Set device configuration</summary>
        bool SetDevice(string deviceName, string deviceID);

        /// <summary>Set tag configuration</summary>
        bool SetTag(string tagName, string deviceName, string tagAddress, string tagType);

        /// <summary>Read single tag</summary>
        bool ReadTag(string tagName, out object value);

        /// <summary>Write single tag</summary>
        bool WriteTag(string tagName, object value);

        /// <summary>Read multiple tags</summary>
        bool ReadTags(string[] tagNames, out object[] values);

        /// <summary>Write multiple tags</summary>
        bool WriteTags(string[] tagNames, object[] values);
    }

    /// <summary>
    /// Base ATDriver class - MISSING CLASS FIXED
    /// </summary>
    public abstract class ATDriverBase : IATDriver
    {
        #region PROPERTIES
        public abstract string DriverName { get; }
        public abstract string DriverVersion { get; }
        public virtual bool IsRunning { get; protected set; }
        public DateTime StartTime { get; protected set; }
        public string LastError { get; protected set; }
        #endregion

        #region ABSTRACT METHODS
        public abstract bool Initialize();
        public abstract bool Start();
        public abstract bool Stop();
        public abstract bool SetDevice(string deviceName, string deviceID);
        public abstract bool SetTag(string tagName, string deviceName, string tagAddress, string tagType);
        public abstract bool ReadTag(string tagName, out object value);
        public abstract bool WriteTag(string tagName, object value);
        #endregion

        #region VIRTUAL METHODS
        public virtual bool ReadTags(string[] tagNames, out object[] values)
        {
            values = new object[tagNames?.Length ?? 0];
            if (tagNames == null || tagNames.Length == 0)
                return false;

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

        public virtual bool WriteTags(string[] tagNames, object[] values)
        {
            if (tagNames == null || values == null || tagNames.Length != values.Length)
                return false;

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

        public virtual void Dispose()
        {
            Stop();
        }
        #endregion

        #region UTILITY METHODS
        protected virtual void SetError(string error)
        {
            LastError = error;
        }

        protected virtual void ClearError()
        {
            LastError = null;
        }

        public virtual string GetStatus()
        {
            return $"{DriverName} v{DriverVersion} - {(IsRunning ? "Running" : "Stopped")}";
        }
        #endregion
    }
}

// ADDITIONAL HELPER CLASSES
namespace ModbusIEC104
{
    /// <summary>
    /// Client Adapter Statistics - MISSING CLASS FIXED
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
    /// Data Point Information - ADDED HELPER CLASS
    /// </summary>
    public class DataPointInfo
    {
        public uint ObjectAddress { get; set; }
        public byte TypeId { get; set; }
        public object Value { get; set; }
        public DateTime TimeStamp { get; set; }
        public byte Quality { get; set; }

        /// <summary>
        /// Check if quality is good
        /// </summary>
        /// <returns>True if good quality</returns>
        public bool IsGoodQuality()
        {
            return (Quality & 0x80) == 0 && (Quality & 0x40) == 0;
        }

        public override string ToString()
        {
            return $"IOA: {ObjectAddress}, Type: {TypeId}, Value: {Value}, Quality: {Quality:X2}";
        }
    }

    /// <summary>
    /// IEC104 Utility Extensions - ADDED HELPER CLASS
    /// </summary>
    public static class IEC104Extensions
    {
        /// <summary>
        /// Convert Information Object to Data Point Info
        /// </summary>
        /// <param name="infoObj">Information Object</param>
        /// <returns>Data Point Info</returns>
        public static DataPointInfo ToDataPointInfo(this IEC104.InformationObject infoObj)
        {
            if (infoObj == null)
                return null;

            return new DataPointInfo
            {
                ObjectAddress = infoObj.InformationObjectAddress,
                TypeId = infoObj.TypeID,
                Value = infoObj.Value,
                TimeStamp = infoObj.TimeStamp,
                Quality = infoObj.Quality
            };
        }

        /// <summary>
        /// Convert Data Point Info to Information Object
        /// </summary>
        /// <param name="dataPoint">Data Point Info</param>
        /// <returns>Information Object</returns>
        public static IEC104.InformationObject ToInformationObject(this DataPointInfo dataPoint)
        {
            if (dataPoint == null)
                return null;

            var infoObj = new IEC104.InformationObject
            {
                InformationObjectAddress = dataPoint.ObjectAddress,
                TypeID = dataPoint.TypeId,
                Value = dataPoint.Value,
                TimeStamp = dataPoint.TimeStamp,
                Quality = dataPoint.Quality
            };

            return infoObj;
        }

        /// <summary>
        /// Check if TypeID is supported
        /// </summary>
        /// <param name="typeId">Type ID</param>
        /// <returns>True if supported</returns>
        public static bool IsSupported(this byte typeId)
        {
            return IEC104.IEC104Address.IsValidTypeID(typeId);
        }

        /// <summary>
        /// Get element size for TypeID
        /// </summary>
        /// <param name="typeId">Type ID</param>
        /// <returns>Element size in bytes</returns>
        public static int GetElementSize(this byte typeId)
        {
            return IEC104.IEC104Address.GetIEC104ElementSize(typeId);
        }

        /// <summary>
        /// Check if TypeID is monitoring type
        /// </summary>
        /// <param name="typeId">Type ID</param>
        /// <returns>True if monitoring type</returns>
        public static bool IsMonitoringType(this byte typeId)
        {
            return IEC104.IEC104Address.IsMonitoringType(typeId);
        }

        /// <summary>
        /// Check if TypeID is control type
        /// </summary>
        /// <param name="typeId">Type ID</param>
        /// <returns>True if control type</returns>
        public static bool IsControlType(this byte typeId)
        {
            return IEC104.IEC104Address.IsControlType(typeId);
        }
    }
}