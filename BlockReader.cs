using System;
using System.Collections.Generic;
using System.Linq;

namespace IEC104
{
    /// <summary>
    /// Loại interrogation cho IEC104
    /// </summary>
    public enum InterrogationType : byte
    {
        /// <summary>Station interrogation (global)</summary>
        Station = IEC104Constants.QOI_STATION,
        /// <summary>Group 1 interrogation</summary>
        Group1 = IEC104Constants.QOI_GROUP_1,
        /// <summary>Group 2 interrogation</summary>
        Group2 = IEC104Constants.QOI_GROUP_2,
        /// <summary>Group 3 interrogation</summary>
        Group3 = IEC104Constants.QOI_GROUP_3,
        /// <summary>Group 4 interrogation</summary>
        Group4 = IEC104Constants.QOI_GROUP_4
    }

    /// <summary>
    /// IEC104 Block Reader - quản lý việc đọc data theo blocks/groups
    /// Khác với Modbus, IEC104 sử dụng Interrogation thay vì đọc sequential addresses
    /// </summary>
    public class IEC104BlockReader
    {
        #region PROPERTIES

        /// <summary>Common Address của station</summary>
        public ushort CommonAddress { get; set; }

        /// <summary>Loại interrogation</summary>
        public InterrogationType InterrogationType { get; set; }

        /// <summary>Danh sách IOA được quan tâm (để filter data)</summary>
        public HashSet<uint> FilteredIOAs { get; set; }

        /// <summary>Danh sách TypeID được quan tâm</summary>
        public HashSet<byte> FilteredTypeIDs { get; set; }

        /// <summary>Cache chứa data nhận được từ interrogation</summary>
        public Dictionary<uint, InformationObject> DataCache { get; private set; }

        /// <summary>Thời gian interrogation cuối cùng</summary>
        public DateTime LastInterrogationTime { get; private set; }

        /// <summary>Kết quả interrogation cuối cùng</summary>
        public bool LastInterrogationSuccess { get; private set; }

        /// <summary>Số lượng objects nhận được</summary>
        public int ReceivedObjectCount => DataCache?.Count ?? 0;

        /// <summary>Block có hợp lệ không</summary>
        public bool IsValid { get; private set; }

        /// <summary>Tên block để debug</summary>
        public string BlockName { get; set; }

        /// <summary>Enable/disable block này</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>Timeout cho interrogation (seconds)</summary>
        public int InterrogationTimeout { get; set; } = 30;

        #endregion

        #region CONSTRUCTORS

        /// <summary>
        /// Constructor mặc định
        /// </summary>
        public IEC104BlockReader()
        {
            FilteredIOAs = new HashSet<uint>();
            FilteredTypeIDs = new HashSet<byte>();
            DataCache = new Dictionary<uint, InformationObject>();
            InterrogationType = InterrogationType.Station;
            LastInterrogationTime = DateTime.MinValue;
            IsValid = true;
        }

        /// <summary>
        /// Constructor với Common Address
        /// </summary>
        /// <param name="commonAddress">Common Address</param>
        /// <param name="interrogationType">Loại interrogation</param>
        public IEC104BlockReader(ushort commonAddress, InterrogationType interrogationType = InterrogationType.Station)
            : this()
        {
            CommonAddress = commonAddress;
            InterrogationType = interrogationType;
            BlockName = $"CA{commonAddress}-{interrogationType}";
        }

        #endregion

        #region FILTER METHODS

        /// <summary>
        /// Thêm IOA vào filter list
        /// </summary>
        /// <param name="ioa">Information Object Address</param>
        public void AddIOAFilter(uint ioa)
        {
            if (ioa > 0 && ioa <= 16777215)
            {
                FilteredIOAs.Add(ioa);
            }
        }

        /// <summary>
        /// Thêm range IOA vào filter
        /// </summary>
        /// <param name="fromIOA">IOA bắt đầu</param>
        /// <param name="toIOA">IOA kết thúc</param>
        public void AddIOARange(uint fromIOA, uint toIOA)
        {
            if (fromIOA > toIOA || fromIOA == 0 || toIOA > 16777215)
                return;

            for (uint ioa = fromIOA; ioa <= toIOA; ioa++)
            {
                FilteredIOAs.Add(ioa);
            }
        }

        /// <summary>
        /// Thêm TypeID vào filter
        /// </summary>
        /// <param name="typeId">Type ID</param>
        public void AddTypeIDFilter(byte typeId)
        {
            if (typeId > 0 && typeId <= 127)
            {
                FilteredTypeIDs.Add(typeId);
            }
        }

        /// <summary>
        /// Clear tất cả filters
        /// </summary>
        public void ClearFilters()
        {
            FilteredIOAs.Clear();
            FilteredTypeIDs.Clear();
        }

        /// <summary>
        /// Kiểm tra Information Object có pass qua filter không
        /// </summary>
        /// <param name="infoObject">Information Object</param>
        /// <returns>True nếu pass filter</returns>
        public bool PassesFilter(InformationObject infoObject)
        {
            if (infoObject == null)
                return false;

            // Nếu không có filter, pass tất cả
            bool ioaFilterEmpty = FilteredIOAs.Count == 0;
            bool typeFilterEmpty = FilteredTypeIDs.Count == 0;

            if (ioaFilterEmpty && typeFilterEmpty)
                return true;

            // Check IOA filter
            bool passIOAFilter = ioaFilterEmpty || FilteredIOAs.Contains(infoObject.InformationObjectAddress);

            // Check TypeID filter
            bool passTypeFilter = typeFilterEmpty || FilteredTypeIDs.Contains(infoObject.TypeID);

            return passIOAFilter && passTypeFilter;
        }

        #endregion

        #region INTERROGATION METHODS

        /// <summary>
        /// Thực hiện interrogation với IEC104 client
        /// </summary>
        /// <param name="client">IEC104 Client</param>
        /// <returns>True nếu thành công</returns>
        public bool PerformInterrogation(IEC104Client client)
        {
            if (!Enabled || client == null || !client.IsReadyForDataTransfer)
            {
                LastInterrogationSuccess = false;
                return false;
            }

            try
            {
                // Gửi General Interrogation
                int result = client.SendGeneralInterrogation(CommonAddress, (byte)InterrogationType);

                LastInterrogationTime = DateTime.Now;
                LastInterrogationSuccess = (result == IEC104Constants.RESULT_OK);

                if (LastInterrogationSuccess)
                {
                    // Đợi một chút để nhận data response
                    System.Threading.Thread.Sleep(100);

                    // Receive spontaneous data để lấy kết quả interrogation
                    List<InformationObject> receivedObjects;
                    int receiveResult = client.ReceiveSpontaneousData(out receivedObjects);

                    if (receiveResult == IEC104Constants.RESULT_OK && receivedObjects != null)
                    {
                        ProcessReceivedData(receivedObjects);
                    }
                }

                return LastInterrogationSuccess;
            }
            catch
            {
                LastInterrogationSuccess = false;
                return false;
            }
        }

        /// <summary>
        /// Xử lý data nhận được từ interrogation
        /// </summary>
        /// <param name="informationObjects">Danh sách Information Objects</param>
        public void ProcessReceivedData(List<InformationObject> informationObjects)
        {
            if (informationObjects == null)
                return;

            foreach (var infoObj in informationObjects)
            {
                // Chỉ lưu những objects pass qua filter
                if (PassesFilter(infoObj))
                {
                    DataCache[infoObj.InformationObjectAddress] = infoObj;
                }
            }
        }

        #endregion

        #region DATA ACCESS METHODS

        /// <summary>
        /// Lấy Information Object theo IOA
        /// </summary>
        /// <param name="ioa">Information Object Address</param>
        /// <returns>Information Object hoặc null</returns>
        public InformationObject GetInformationObject(uint ioa)
        {
            return DataCache.TryGetValue(ioa, out InformationObject infoObj) ? infoObj : null;
        }

        /// <summary>
        /// Kiểm tra IOA có tồn tại trong cache không
        /// </summary>
        /// <param name="ioa">Information Object Address</param>
        /// <returns>True nếu tồn tại</returns>
        public bool ContainsIOA(uint ioa)
        {
            return DataCache.ContainsKey(ioa);
        }

        /// <summary>
        /// Lấy tất cả Information Objects theo TypeID
        /// </summary>
        /// <param name="typeId">Type ID</param>
        /// <returns>Danh sách Information Objects</returns>
        public List<InformationObject> GetObjectsByTypeID(byte typeId)
        {
            return DataCache.Values.Where(obj => obj.TypeID == typeId).ToList();
        }

        /// <summary>
        /// Lấy tất cả IOAs trong cache
        /// </summary>
        /// <returns>Danh sách IOAs</returns>
        public List<uint> GetAllIOAs()
        {
            return DataCache.Keys.ToList();
        }

        /// <summary>
        /// Clear cache data
        /// </summary>
        public void ClearCache()
        {
            DataCache.Clear();
        }

        #endregion

        #region VALIDATION AND STATUS

        /// <summary>
        /// Kiểm tra block có cần interrogation không
        /// </summary>
        /// <param name="maxInterval">Interval tối đa (seconds)</param>
        /// <returns>True nếu cần interrogation</returns>
        public bool NeedsInterrogation(int maxInterval = 300)
        {
            if (!Enabled || !LastInterrogationSuccess)
                return true;

            var elapsed = DateTime.Now - LastInterrogationTime;
            return elapsed.TotalSeconds >= maxInterval;
        }

        /// <summary>
        /// Validate block configuration
        /// </summary>
        /// <param name="errorMessage">Error message nếu có</param>
        /// <returns>True nếu valid</returns>
        public bool Validate(out string errorMessage)
        {
            errorMessage = "";

            if (CommonAddress == 0 || CommonAddress > 65535)
            {
                errorMessage = "Common Address phải từ 1-65535";
                IsValid = false;
                return false;
            }

            if (!Enum.IsDefined(typeof(InterrogationType), InterrogationType))
            {
                errorMessage = "Interrogation Type không hợp lệ";
                IsValid = false;
                return false;
            }

            if (InterrogationTimeout <= 0 || InterrogationTimeout > 300)
            {
                errorMessage = "Interrogation Timeout phải từ 1-300 seconds";
                IsValid = false;
                return false;
            }

            IsValid = true;
            return true;
        }

        /// <summary>
        /// Lấy thống kê của block
        /// </summary>
        /// <returns>Statistics string</returns>
        public string GetStatistics()
        {
            var stats = new List<string>();
            stats.Add($"CA: {CommonAddress}");
            stats.Add($"Type: {InterrogationType}");
            stats.Add($"Objects: {ReceivedObjectCount}");
            stats.Add($"Filters: IOA({FilteredIOAs.Count}), TypeID({FilteredTypeIDs.Count})");
            stats.Add($"Last: {LastInterrogationTime:HH:mm:ss}");
            stats.Add($"Success: {LastInterrogationSuccess}");

            return string.Join(", ", stats);
        }

        #endregion

        #region STATIC FACTORY METHODS

        /// <summary>
        /// Tạo block readers từ cấu hình string
        /// Format: CA-InterrogationType-IOAFrom-IOATo/TypeIDs/Enabled
        /// Example: "1-20-1-1000/1,3,9/true"
        /// </summary>
        /// <param name="blockSetting">Block setting string</param>
        /// <returns>Block reader hoặc null nếu lỗi</returns>
        public static IEC104BlockReader Initialize(string blockSetting)
        {
            if (string.IsNullOrWhiteSpace(blockSetting))
                return null;

            try
            {
                var parts = blockSetting.Split('/');
                if (parts.Length < 1) return null;

                // Parse main part: CA-InterrogationType-IOAFrom-IOATo
                var mainParts = parts[0].Split('-');
                if (mainParts.Length < 2) return null;

                // Parse Common Address
                if (!ushort.TryParse(mainParts[0], out ushort commonAddress) || commonAddress == 0)
                    return null;

                // Parse Interrogation Type
                if (!byte.TryParse(mainParts[1], out byte intType))
                    return null;

                var interrogationType = (InterrogationType)intType;
                if (!Enum.IsDefined(typeof(InterrogationType), interrogationType))
                    return null;

                // Tạo block reader
                var blockReader = new IEC104BlockReader(commonAddress, interrogationType);

                // Parse IOA range if provided
                if (mainParts.Length >= 4)
                {
                    if (uint.TryParse(mainParts[2], out uint ioaFrom) &&
                        uint.TryParse(mainParts[3], out uint ioaTo))
                    {
                        blockReader.AddIOARange(ioaFrom, ioaTo);
                    }
                }

                // Parse TypeID filters if provided
                if (parts.Length >= 2 && !string.IsNullOrWhiteSpace(parts[1]))
                {
                    var typeIds = parts[1].Split(',');
                    foreach (var typeIdStr in typeIds)
                    {
                        if (byte.TryParse(typeIdStr.Trim(), out byte typeId))
                        {
                            blockReader.AddTypeIDFilter(typeId);
                        }
                    }
                }

                // Parse Enabled flag if provided
                if (parts.Length >= 3 && !string.IsNullOrWhiteSpace(parts[2]))
                {
                    if (bool.TryParse(parts[2].Trim(), out bool enabled))
                    {
                        blockReader.Enabled = enabled;
                    }
                }

                // Validate
                if (blockReader.Validate(out string errorMessage))
                {
                    return blockReader;
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Tạo multiple block readers từ settings string
        /// Format: "block1|block2|block3"
        /// </summary>
        /// <param name="blockSettings">Block settings string</param>
        /// <returns>Danh sách block readers</returns>
        public static List<IEC104BlockReader> InitializeMultiple(string blockSettings)
        {
            var blockReaders = new List<IEC104BlockReader>();

            if (string.IsNullOrWhiteSpace(blockSettings))
                return blockReaders;

            var blocks = blockSettings.Split('|');
            foreach (var blockSetting in blocks)
            {
                var blockReader = Initialize(blockSetting.Trim());
                if (blockReader != null)
                {
                    blockReaders.Add(blockReader);
                }
            }

            return blockReaders;
        }

        /// <summary>
        /// Tạo default block reader cho station interrogation
        /// </summary>
        /// <param name="commonAddress">Common Address</param>
        /// <returns>Default block reader</returns>
        public static IEC104BlockReader CreateDefault(ushort commonAddress)
        {
            return new IEC104BlockReader(commonAddress, InterrogationType.Station)
            {
                BlockName = $"Default-CA{commonAddress}",
                Enabled = true
            };
        }

        #endregion

        #region OVERRIDE METHODS

        /// <summary>
        /// Override ToString
        /// </summary>
        /// <returns>String representation</returns>
        public override string ToString()
        {
            return $"IEC104BlockReader: {BlockName ?? "Unnamed"} - {GetStatistics()}";
        }

        /// <summary>
        /// Override Equals
        /// </summary>
        /// <param name="obj">Object to compare</param>
        /// <returns>True if equal</returns>
        public override bool Equals(object obj)
        {
            if (obj is IEC104BlockReader other)
            {
                return CommonAddress == other.CommonAddress &&
                       InterrogationType == other.InterrogationType;
            }
            return false;
        }

        /// <summary>
        /// Override GetHashCode
        /// </summary>
        /// <returns>Hash code</returns>
        public override int GetHashCode()
        {
            return HashCode.Combine(CommonAddress, InterrogationType);
        }

        #endregion
    }
}