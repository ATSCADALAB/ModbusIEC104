using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ModbusIEC104.Common;

namespace ModbusIEC104
{
    /// <summary>
    /// Block Reader cho IEC104 - quản lý việc đọc dữ liệu theo block/group
    /// </summary>
    public class BlockReader : IDisposable
    {
        #region FIELDS
        private readonly DeviceReader deviceReader;
        private readonly IEC104BlockSettings blockSettings;
        private readonly object lockObject = new object();
        private Timer readTimer;
        private bool isDisposed = false;
        private bool isInitialized = false;
        #endregion

        #region PROPERTIES
        /// <summary>Tên block</summary>
        public string BlockName => blockSettings?.Name ?? "Unknown";

        /// <summary>Block có được kích hoạt không</summary>
        public bool Enabled => blockSettings?.Enabled ?? false;

        /// <summary>Block có đang chạy không</summary>
        public bool IsRunning { get; private set; }

        /// <summary>Thời gian đọc cuối</summary>
        public DateTime LastReadTime { get; private set; }

        /// <summary>Thời gian đọc thành công cuối</summary>
        public DateTime LastSuccessfulReadTime { get; private set; }

        /// <summary>Lỗi cuối</summary>
        public string LastError { get; private set; }

        /// <summary>Số lần đọc</summary>
        public int ReadCount { get; private set; }

        /// <summary>Số lần đọc thành công</summary>
        public int SuccessfulReadCount { get; private set; }

        /// <summary>Số lần lỗi</summary>
        public int ErrorCount { get; private set; }

        /// <summary>Khoảng thời gian đọc (ms)</summary>
        public int ReadInterval => blockSettings?.ReadInterval ?? 1000;

        /// <summary>Common Address</summary>
        public ushort CommonAddress => blockSettings?.CommonAddress ?? 1;

        /// <summary>Loại interrogation</summary>
        public InterrogationType InterrogationType => blockSettings?.InterrogationType ?? InterrogationType.General;

        /// <summary>Danh sách IOA ranges</summary>
        public List<IOARange> IOARanges => blockSettings?.IOARanges ?? new List<IOARange>();

        /// <summary>Danh sách TypeID filters</summary>
        public List<byte> TypeIDFilters => blockSettings?.TypeIDFilters ?? new List<byte>();

        /// <summary>Dữ liệu đã đọc gần nhất</summary>
        public List<InformationObject> LastReadData { get; private set; } = new List<InformationObject>();
        #endregion

        #region CONSTRUCTORS
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="deviceReader">Device reader parent</param>
        /// <param name="blockSettings">Block settings</param>
        public BlockReader(DeviceReader deviceReader, IEC104BlockSettings blockSettings)
        {
            this.deviceReader = deviceReader ?? throw new ArgumentNullException(nameof(deviceReader));
            this.blockSettings = blockSettings ?? throw new ArgumentNullException(nameof(blockSettings));

            LastReadTime = DateTime.MinValue;
            LastSuccessfulReadTime = DateTime.MinValue;
        }
        #endregion

        #region INITIALIZE & DISPOSE
        /// <summary>
        /// Khởi tạo block reader
        /// </summary>
        /// <returns>True nếu thành công</returns>
        public bool Initialize()
        {
            try
            {
                if (isInitialized)
                    return true;

                if (!Enabled)
                {
                    LastError = "Block is disabled";
                    return false;
                }

                // Validate settings
                if (!ValidateSettings())
                    return false;

                // Initialize read timer
                InitializeReadTimer();

                isInitialized = true;
                return true;
            }
            catch (Exception ex)
            {
                LastError = $"Initialize failed: {ex.Message}";
                return false;
            }
        }

        /// <summary>
        /// Dispose block reader
        /// </summary>
        public void Dispose()
        {
            if (!isDisposed)
            {
                Stop();
                readTimer?.Dispose();
                readTimer = null;
                isDisposed = true;
            }
        }
        #endregion

        #region PUBLIC METHODS
        /// <summary>
        /// Bắt đầu đọc block
        /// </summary>
        /// <returns>True nếu thành công</returns>
        public bool Start()
        {
            try
            {
                if (!isInitialized)
                {
                    if (!Initialize())
                        return false;
                }

                if (IsRunning)
                    return true;

                if (!Enabled)
                {
                    LastError = "Block is disabled";
                    return false;
                }

                // Start timer
                var interval = ReadInterval > 0 ? ReadInterval : 1000;
                readTimer?.Change(0, interval);

                IsRunning = true;
                return true;
            }
            catch (Exception ex)
            {
                LastError = $"Start failed: {ex.Message}";
                return false;
            }
        }

        /// <summary>
        /// Dừng đọc block
        /// </summary>
        /// <returns>True nếu thành công</returns>
        public bool Stop()
        {
            try
            {
                if (!IsRunning)
                    return true;

                // Stop timer
                readTimer?.Change(Timeout.Infinite, Timeout.Infinite);

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
        /// Đọc block một lần
        /// </summary>
        /// <returns>True nếu thành công</returns>
        public bool ReadBlock()
        {
            if (!Enabled || isDisposed)
                return false;

            try
            {
                lock (lockObject)
                {
                    LastReadTime = DateTime.Now;
                    ReadCount++;

                    bool success = false;

                    switch (blockSettings.ReadStrategy)
                    {
                        case BlockReadStrategy.Interrogation:
                            success = PerformInterrogation();
                            break;

                        case BlockReadStrategy.SpontaneousData:
                            success = ProcessSpontaneousData();
                            break;

                        case BlockReadStrategy.SpecificIOAs:
                            success = ReadSpecificIOAs();
                            break;

                        case BlockReadStrategy.Cyclic:
                            success = PerformCyclicRead();
                            break;

                        default:
                            success = PerformInterrogation(); // Default fallback
                            break;
                    }

                    if (success)
                    {
                        SuccessfulReadCount++;
                        LastSuccessfulReadTime = DateTime.Now;
                        LastError = null;
                    }
                    else
                    {
                        ErrorCount++;
                    }

                    return success;
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
        /// Đọc block bất đồng bộ
        /// </summary>
        /// <returns>Task với kết quả đọc</returns>
        public async Task<bool> ReadBlockAsync()
        {
            return await Task.Run(() => ReadBlock());
        }
        #endregion

        #region READING STRATEGIES
        /// <summary>
        /// Thực hiện Interrogation
        /// </summary>
        /// <returns>True nếu thành công</returns>
        private bool PerformInterrogation()
        {
            try
            {
                var clientAdapter = GetClientAdapter();
                if (clientAdapter == null)
                {
                    LastError = "Client adapter not available";
                    return false;
                }

                // Gửi interrogation command
                var result = clientAdapter.SendInterrogation(CommonAddress, InterrogationType);
                if (!result)
                {
                    LastError = "Failed to send interrogation command";
                    return false;
                }

                // Đợi và xử lý response
                var responseData = WaitForInterrogationResponse();
                if (responseData != null && responseData.Count > 0)
                {
                    ProcessReceivedData(responseData);
                    return true;
                }
                else
                {
                    LastError = "No data received from interrogation";
                    return false;
                }
            }
            catch (Exception ex)
            {
                LastError = $"Interrogation failed: {ex.Message}";
                return false;
            }
        }

        /// <summary>
        /// Xử lý dữ liệu tự phát
        /// </summary>
        /// <returns>True nếu thành công</returns>
        private bool ProcessSpontaneousData()
        {
            try
            {
                var clientAdapter = GetClientAdapter();
                if (clientAdapter == null)
                {
                    LastError = "Client adapter not available";
                    return false;
                }

                var spontaneousData = clientAdapter.ProcessSpontaneousData();
                if (spontaneousData != null && spontaneousData.Count > 0)
                {
                    // Filter dữ liệu theo block settings
                    var filteredData = FilterData(spontaneousData);
                    ProcessReceivedData(filteredData);
                    return true;
                }
                else
                {
                    // Không có dữ liệu tự phát - không phải lỗi
                    return true;
                }
            }
            catch (Exception ex)
            {
                LastError = $"Spontaneous data processing failed: {ex.Message}";
                return false;
            }
        }

        /// <summary>
        /// Đọc các IOA cụ thể
        /// </summary>
        /// <returns>True nếu thành công</returns>
        private bool ReadSpecificIOAs()
        {
            try
            {
                var clientAdapter = GetClientAdapter();
                if (clientAdapter == null)
                {
                    LastError = "Client adapter not available";
                    return false;
                }

                var allData = new List<InformationObject>();

                // Đọc từng IOA range
                foreach (var ioaRange in IOARanges)
                {
                    var rangeData = ReadIOARange(clientAdapter, ioaRange);
                    if (rangeData != null)
                    {
                        allData.AddRange(rangeData);
                    }
                }

                if (allData.Count > 0)
                {
                    ProcessReceivedData(allData);
                    return true;
                }
                else
                {
                    LastError = "No data read from specific IOAs";
                    return false;
                }
            }
            catch (Exception ex)
            {
                LastError = $"Specific IOA reading failed: {ex.Message}";
                return false;
            }
        }

        /// <summary>
        /// Thực hiện đọc theo chu kỳ
        /// </summary>
        /// <returns>True nếu thành công</returns>
        private bool PerformCyclicRead()
        {
            try
            {
                var clientAdapter = GetClientAdapter();
                if (clientAdapter == null)
                {
                    LastError = "Client adapter not available";
                    return false;
                }

                // Combine interrogation và spontaneous data
                var allData = new List<InformationObject>();

                // 1. Đọc dữ liệu tự phát trước
                var spontaneousData = clientAdapter.ProcessSpontaneousData();
                if (spontaneousData != null)
                {
                    allData.AddRange(FilterData(spontaneousData));
                }

                // 2. Nếu cần, thực hiện interrogation
                if (ShouldPerformInterrogation())
                {
                    var interrogationData = WaitForInterrogationResponse();
                    if (interrogationData != null)
                    {
                        allData.AddRange(FilterData(interrogationData));
                    }
                }

                ProcessReceivedData(allData);
                return true;
            }
            catch (Exception ex)
            {
                LastError = $"Cyclic read failed: {ex.Message}";
                return false;
            }
        }
        #endregion

        #region HELPER METHODS
        /// <summary>
        /// Validate block settings
        /// </summary>
        /// <returns>True nếu hợp lệ</returns>
        private bool ValidateSettings()
        {
            if (blockSettings == null)
            {
                LastError = "Block settings is null";
                return false;
            }

            if (!IEC104Constants.IsValidCommonAddress(CommonAddress))
            {
                LastError = $"Invalid Common Address: {CommonAddress}";
                return false;
            }

            if (ReadInterval <= 0)
            {
                LastError = $"Invalid read interval: {ReadInterval}";
                return false;
            }

            // Validate IOA ranges
            foreach (var range in IOARanges)
            {
                if (!IEC104Constants.IsValidIOA(range.StartIOA) || !IEC104Constants.IsValidIOA(range.EndIOA))
                {
                    LastError = $"Invalid IOA range: {range.StartIOA} - {range.EndIOA}";
                    return false;
                }

                if (range.StartIOA > range.EndIOA)
                {
                    LastError = $"Invalid IOA range order: {range.StartIOA} > {range.EndIOA}";
                    return false;
                }
            }

            // Validate TypeID filters
            foreach (var typeId in TypeIDFilters)
            {
                if (!IEC104Constants.IsValidTypeID(typeId))
                {
                    LastError = $"Invalid TypeID filter: {typeId}";
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Khởi tạo read timer
        /// </summary>
        private void InitializeReadTimer()
        {
            var interval = ReadInterval > 0 ? ReadInterval : 1000;
            readTimer = new Timer(ReadTimerCallback, null, Timeout.Infinite, interval);
        }

        /// <summary>
        /// Read timer callback
        /// </summary>
        /// <param name="state">Timer state</param>
        private void ReadTimerCallback(object state)
        {
            if (!IsRunning || isDisposed || !Enabled)
                return;

            Task.Run(() => ReadBlock());
        }

        /// <summary>
        /// Lấy client adapter từ device reader
        /// </summary>
        /// <returns>Client adapter hoặc null</returns>
        private IEC104ClientAdapter GetClientAdapter()
        {
            return deviceReader?.GetClientAdapter() as IEC104ClientAdapter;
        }

        /// <summary>
        /// Đợi response từ interrogation
        /// </summary>
        /// <returns>Danh sách information objects</returns>
        private List<InformationObject> WaitForInterrogationResponse()
        {
            var timeout = 5000; // 5 seconds timeout
            var startTime = DateTime.Now;
            var allData = new List<InformationObject>();

            try
            {
                var clientAdapter = GetClientAdapter();
                if (clientAdapter == null)
                    return null;

                while ((DateTime.Now - startTime).TotalMilliseconds < timeout)
                {
                    if (clientAdapter.ReadInformationObjects(out List<InformationObject> objects))
                    {
                        if (objects != null && objects.Count > 0)
                        {
                            var filteredObjects = FilterData(objects);
                            allData.AddRange(filteredObjects);

                            // Kiểm tra xem có phải là kết thúc interrogation không
                            if (IsInterrogationComplete(objects))
                            {
                                break;
                            }
                        }
                    }

                    Thread.Sleep(50); // Chờ 50ms trước khi thử lại
                }

                return allData;
            }
            catch (Exception ex)
            {
                LastError = $"Wait for interrogation response failed: {ex.Message}";
                return null;
            }
        }

        /// <summary>
        /// Đọc dữ liệu từ một IOA range
        /// </summary>
        /// <param name="clientAdapter">Client adapter</param>
        /// <param name="ioaRange">IOA range</param>
        /// <returns>Danh sách information objects</returns>
        private List<InformationObject> ReadIOARange(IEC104ClientAdapter clientAdapter, IOARange ioaRange)
        {
            try
            {
                var rangeData = new List<InformationObject>();

                for (uint ioa = ioaRange.StartIOA; ioa <= ioaRange.EndIOA; ioa++)
                {
                    // Gửi read command cho từng IOA
                    if (clientAdapter.SendCommand(CommonAddress, ioa, IEC104Constants.C_RD_NA_1, null))
                    {
                        // Đợi response
                        Thread.Sleep(10); // Small delay between reads
                    }
                }

                // Đọc tất cả responses
                if (clientAdapter.ReadInformationObjects(out List<InformationObject> objects))
                {
                    if (objects != null)
                    {
                        // Filter theo IOA range
                        var filteredObjects = objects.Where(obj =>
                            obj.ObjectAddress >= ioaRange.StartIOA &&
                            obj.ObjectAddress <= ioaRange.EndIOA).ToList();

                        rangeData.AddRange(filteredObjects);
                    }
                }

                return rangeData;
            }
            catch (Exception ex)
            {
                LastError = $"Read IOA range failed: {ex.Message}";
                return null;
            }
        }

        /// <summary>
        /// Filter dữ liệu theo block settings
        /// </summary>
        /// <param name="data">Dữ liệu gốc</param>
        /// <returns>Dữ liệu đã filter</returns>
        private List<InformationObject> FilterData(List<InformationObject> data)
        {
            if (data == null || data.Count == 0)
                return new List<InformationObject>();

            var filteredData = data.AsEnumerable();

            // Filter theo IOA ranges
            if (IOARanges.Count > 0)
            {
                filteredData = filteredData.Where(obj =>
                    IOARanges.Any(range =>
                        obj.ObjectAddress >= range.StartIOA &&
                        obj.ObjectAddress <= range.EndIOA));
            }

            // Filter theo TypeID
            if (TypeIDFilters.Count > 0)
            {
                filteredData = filteredData.Where(obj =>
                    TypeIDFilters.Contains(obj.TypeId));
            }

            return filteredData.ToList();
        }

        /// <summary>
        /// Xử lý dữ liệu đã nhận
        /// </summary>
        /// <param name="data">Dữ liệu</param>
        private void ProcessReceivedData(List<InformationObject> data)
        {
            if (data == null || data.Count == 0)
                return;

            lock (lockObject)
            {
                // Update last read data
                LastReadData = new List<InformationObject>(data);

                // Có thể thêm logic xử lý dữ liệu khác ở đây
                // Ví dụ: lưu vào database, gửi events, etc.
            }
        }

        /// <summary>
        /// Kiểm tra xem interrogation đã hoàn thành chưa
        /// </summary>
        /// <param name="objects">Objects vừa nhận</param>
        /// <returns>True nếu đã hoàn thành</returns>
        private bool IsInterrogationComplete(List<InformationObject> objects)
        {
            if (objects == null || objects.Count == 0)
                return false;

            // Kiểm tra COT = Activation Termination (10)
            // Thông thường server sẽ gửi frame với COT = 10 để báo kết thúc interrogation
            // Đây là implementation đơn giản - có thể cần điều chỉnh tùy theo server cụ thể

            return false; // Implement theo logic cụ thể của server
        }

        /// <summary>
        /// Kiểm tra có nên thực hiện interrogation không
        /// </summary>
        /// <returns>True nếu nên thực hiện</returns>
        private bool ShouldPerformInterrogation()
        {
            // Logic để quyết định khi nào cần interrogation
            // Ví dụ: mỗi 10 lần đọc, hoặc khi có lỗi, etc.

            var timeSinceLastInterrogation = DateTime.Now - LastSuccessfulReadTime;
            return timeSinceLastInterrogation.TotalMinutes > 5; // Mỗi 5 phút một lần
        }
        #endregion

        #region STATUS METHODS
        /// <summary>
        /// Lấy thông tin trạng thái block
        /// </summary>
        /// <returns>Thông tin trạng thái</returns>
        public string GetStatusInfo()
        {
            var status = IsRunning ? "Running" : "Stopped";
            var enabled = Enabled ? "Enabled" : "Disabled";

            return $"Block[{BlockName}] - {status} | {enabled} | " +
                   $"Reads: {ReadCount} | Success: {SuccessfulReadCount} | Errors: {ErrorCount} | " +
                   $"Last: {LastReadTime:HH:mm:ss} | Data: {LastReadData.Count} objects";
        }

        /// <summary>
        /// Lấy thống kê chi tiết
        /// </summary>
        /// <returns>Thống kê block</returns>
        public BlockStatistics GetStatistics()
        {
            lock (lockObject)
            {
                return new BlockStatistics
                {
                    BlockName = BlockName,
                    Enabled = Enabled,
                    IsRunning = IsRunning,
                    ReadInterval = ReadInterval,
                    CommonAddress = CommonAddress,
                    InterrogationType = InterrogationType,
                    ReadStrategy = blockSettings?.ReadStrategy ?? BlockReadStrategy.Interrogation,

                    ReadCount = ReadCount,
                    SuccessfulReadCount = SuccessfulReadCount,
                    ErrorCount = ErrorCount,
                    SuccessRate = ReadCount > 0 ? (double)SuccessfulReadCount / ReadCount * 100 : 0,

                    LastReadTime = LastReadTime,
                    LastSuccessfulReadTime = LastSuccessfulReadTime,
                    LastError = LastError,

                    IOARangesCount = IOARanges.Count,
                    TypeIDFiltersCount = TypeIDFilters.Count,
                    LastDataObjectsCount = LastReadData.Count,

                    IOARanges = new List<IOARange>(IOARanges),
                    TypeIDFilters = new List<byte>(TypeIDFilters)
                };
            }
        }

        /// <summary>
        /// Lấy dữ liệu đã đọc gần nhất
        /// </summary>
        /// <returns>Bản copy của dữ liệu</returns>
        public List<InformationObject> GetLastReadData()
        {
            lock (lockObject)
            {
                return new List<InformationObject>(LastReadData);
            }
        }
        #endregion

        #region OVERRIDE METHODS
        /// <summary>
        /// String representation
        /// </summary>
        /// <returns>String representation</returns>
        public override string ToString()
        {
            return $"BlockReader[{BlockName}] - CA:{CommonAddress}, Type:{InterrogationType}, Running:{IsRunning}";
        }
        #endregion
    }

    #region SUPPORTING CLASSES
    /// <summary>
    /// IEC104 Block Settings
    /// </summary>
    public class IEC104BlockSettings : BlockSettings
    {
        #region PROPERTIES
        /// <summary>Common Address</summary>
        public ushort CommonAddress { get; set; } = 1;

        /// <summary>Loại Interrogation</summary>
        public InterrogationType InterrogationType { get; set; } = InterrogationType.General;

        /// <summary>Chiến lược đọc</summary>
        public BlockReadStrategy ReadStrategy { get; set; } = BlockReadStrategy.Interrogation;

        /// <summary>Danh sách IOA ranges</summary>
        public List<IOARange> IOARanges { get; set; } = new List<IOARange>();

        /// <summary>Danh sách TypeID filters</summary>
        public List<byte> TypeIDFilters { get; set; } = new List<byte>();

        /// <summary>Block ID duy nhất</summary>
        public override string BlockID => $"{CommonAddress}-{InterrogationType}-{Name}";
        #endregion

        #region METHODS
        /// <summary>
        /// Thêm IOA range
        /// </summary>
        /// <param name="startIOA">IOA bắt đầu</param>
        /// <param name="endIOA">IOA kết thúc</param>
        public void AddIOARange(uint startIOA, uint endIOA)
        {
            if (IEC104Constants.IsValidIOA(startIOA) && IEC104Constants.IsValidIOA(endIOA) && startIOA <= endIOA)
            {
                IOARanges.Add(new IOARange { StartIOA = startIOA, EndIOA = endIOA });
            }
        }

        /// <summary>
        /// Thêm TypeID filter
        /// </summary>
        /// <param name="typeId">Type ID</param>
        public void AddTypeIDFilter(byte typeId)
        {
            if (IEC104Constants.IsValidTypeID(typeId) && !TypeIDFilters.Contains(typeId))
            {
                TypeIDFilters.Add(typeId);
            }
        }

        /// <summary>
        /// Validate settings
        /// </summary>
        /// <returns>True nếu hợp lệ</returns>
        public override bool IsValid()
        {
            return base.IsValid() &&
                   IEC104Constants.IsValidCommonAddress(CommonAddress) &&
                   Enum.IsDefined(typeof(InterrogationType), InterrogationType);
        }
        #endregion
    }

    /// <summary>
    /// IOA Range
    /// </summary>
    public class IOARange
    {
        public uint StartIOA { get; set; }
        public uint EndIOA { get; set; }

        public uint Count => EndIOA >= StartIOA ? EndIOA - StartIOA + 1 : 0;

        public override string ToString()
        {
            return $"{StartIOA}-{EndIOA} ({Count} IOAs)";
        }
    }

    /// <summary>
    /// Chiến lược đọc block
    /// </summary>
    public enum BlockReadStrategy
    {
        /// <summary>Sử dụng Interrogation</summary>
        Interrogation,

        /// <summary>Xử lý dữ liệu tự phát</summary>
        SpontaneousData,

        /// <summary>Đọc các IOA cụ thể</summary>
        SpecificIOAs,

        /// <summary>Kết hợp các phương pháp</summary>
        Cyclic
    }

    /// <summary>
    /// Thống kê Block Reader
    /// </summary>
    public class BlockStatistics
    {
        public string BlockName { get; set; }
        public bool Enabled { get; set; }
        public bool IsRunning { get; set; }
        public int ReadInterval { get; set; }
        public ushort CommonAddress { get; set; }
        public InterrogationType InterrogationType { get; set; }
        public BlockReadStrategy ReadStrategy { get; set; }

        public int ReadCount { get; set; }
        public int SuccessfulReadCount { get; set; }
        public int ErrorCount { get; set; }
        public double SuccessRate { get; set; }

        public DateTime LastReadTime { get; set; }
        public DateTime LastSuccessfulReadTime { get; set; }
        public string LastError { get; set; }

        public int IOARangesCount { get; set; }
        public int TypeIDFiltersCount { get; set; }
        public int LastDataObjectsCount { get; set; }

        public List<IOARange> IOARanges { get; set; }
        public List<byte> TypeIDFilters { get; set; }

        public override string ToString()
        {
            return $"Block[{BlockName}] CA:{CommonAddress} | Strategy:{ReadStrategy} | " +
                   $"Success:{SuccessfulReadCount}/{ReadCount} ({SuccessRate:F1}%) | " +
                   $"IOAs:{IOARangesCount} ranges, TypeIDs:{TypeIDFiltersCount} filters | " +
                   $"LastData:{LastDataObjectsCount} objects";
        }
    }
    #endregion
}