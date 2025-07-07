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
        public int ReadInterval => blockSettings?.ReadInterval > 0 ? blockSettings.ReadInterval : 1000;

        /// <summary>Common Address</summary>
        public ushort CommonAddress => blockSettings?.CommonAddress ?? 1;

        /// <summary>Loại interrogation</summary>
        public InterrogationType InterrogationType => blockSettings?.InterrogationType ?? InterrogationType.General;

        /// <summary>Dữ liệu đã đọc gần nhất</summary>
        public Dictionary<uint, InformationObject> LastReadData { get; private set; } = new Dictionary<uint, InformationObject>();
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
                isDisposed = true;
                Stop();
                readTimer?.Dispose();
                readTimer = null;
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
                readTimer?.Change(ReadInterval, ReadInterval);

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

            // --- FIX: Logic đọc được tổ chức lại hoàn toàn ---
            // Lý do: Hợp nhất việc đọc dữ liệu tự phát và interrogation vào một luồng,
            // giúp xử lý dữ liệu nhất quán và hiệu quả hơn.
            try
            {
                var clientAdapter = GetClientAdapter();
                if (clientAdapter == null || !clientAdapter.IsConnected)
                {
                    LastError = "Client adapter not available or not connected";
                    return false;
                }

                LastReadTime = DateTime.Now;
                ReadCount++;

                // 1. Luôn xử lý dữ liệu tự phát (spontaneous)
                ProcessSpontaneousData(clientAdapter);

                // 2. Kiểm tra xem có cần thực hiện Interrogation không
                if (ShouldPerformInterrogation())
                {
                    if (!PerformInterrogation(clientAdapter))
                    {
                        ErrorCount++;
                        return false; // Interrogation thất bại
                    }
                }

                SuccessfulReadCount++;
                LastSuccessfulReadTime = DateTime.Now;
                LastError = null;
                return true;
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
        private bool PerformInterrogation(IEC104ClientAdapter clientAdapter)
        {
            try
            {
                // Gửi interrogation command
                if (!clientAdapter.SendInterrogation(CommonAddress, InterrogationType))
                {
                    LastError = "Failed to send interrogation command";
                    return false;
                }

                // Dữ liệu từ interrogation sẽ được xử lý trong luồng ProcessSpontaneousData
                // vì nó cũng được đưa vào asduQueue. Chúng ta chỉ cần gửi lệnh.
                return true;
            }
            catch (Exception ex)
            {
                LastError = $"Interrogation failed: {ex.Message}";
                return false;
            }
        }

        /// <summary>
        /// Xử lý dữ liệu tự phát và dữ liệu từ interrogation
        /// </summary>
        /// <returns>True nếu thành công</returns>
        private void ProcessSpontaneousData(IEC104ClientAdapter clientAdapter)
        {
            // --- FIX: Logic được cải tiến để xử lý tất cả dữ liệu từ queue ---
            var asdus = clientAdapter.DequeueReceivedASDUs();
            if (asdus.Count > 0)
            {
                var receivedObjects = new List<InformationObject>();
                foreach (var asdu in asdus)
                {
                    // Chỉ xử lý các ASDU thuộc về CommonAddress của block này
                    if (asdu.CommonAddress == this.CommonAddress)
                    {
                        receivedObjects.AddRange(asdu.InformationObjects);
                    }
                }

                if (receivedObjects.Count > 0)
                {
                    ProcessReceivedData(receivedObjects);
                }
            }
        }

        #endregion

        #region HELPER METHODS

        /// <summary>
        /// Khởi tạo read timer
        /// </summary>
        private void InitializeReadTimer()
        {
            readTimer = new Timer(ReadTimerCallback, null, Timeout.Infinite, Timeout.Infinite);
        }

        /// <summary>
        /// Read timer callback
        /// </summary>
        /// <param name="state">Timer state</param>
        private void ReadTimerCallback(object state)
        {
            if (isDisposed || !IsRunning) return;

            // Ngăn timer chạy chồng chéo
            try
            {
                readTimer.Change(Timeout.Infinite, Timeout.Infinite);
                ReadBlock();
            }
            finally
            {
                if (IsRunning && !isDisposed)
                {
                    readTimer.Change(ReadInterval, ReadInterval);
                }
            }
        }

        /// <summary>
        /// Lấy client adapter từ device reader
        /// </summary>
        /// <returns>Client adapter hoặc null</returns>
        private IEC104ClientAdapter GetClientAdapter()
        {
            // Sửa lại để lấy đúng client adapter
            return deviceReader?.GetClientAdapter() as IEC104ClientAdapter;
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
                // --- FIX: Sử dụng Dictionary để cập nhật hiệu quả và tránh trùng lặp ---
                // Lý do: Dictionary cho phép ghi đè giá trị cũ một cách nhanh chóng,
                // đảm bảo mỗi IOA chỉ có một giá trị mới nhất.
                foreach (var obj in data)
                {
                    // Tạo một key duy nhất cho mỗi điểm dữ liệu
                    uint key = obj.ObjectAddress;
                    LastReadData[key] = obj;
                }
            }
        }

        /// <summary>
        /// Kiểm tra có nên thực hiện interrogation không
        /// </summary>
        /// <returns>True nếu nên thực hiện</returns>
        private bool ShouldPerformInterrogation()
        {
            // Thực hiện interrogation lần đầu tiên hoặc sau một khoảng thời gian nhất định
            if (LastSuccessfulReadTime == DateTime.MinValue) return true;

            var interrogationInterval = deviceReader.Settings?.InterrogationInterval ?? 300;
            if (interrogationInterval <= 0) return false;

            var timeSinceLastInterrogation = DateTime.Now - LastSuccessfulReadTime;
            return timeSinceLastInterrogation.TotalSeconds >= interrogationInterval;
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
                   $"Last: {LastReadTime:HH:mm:ss} | DataPoints: {LastReadData.Count}";
        }

        /// <summary>
        /// Lấy dữ liệu đã đọc gần nhất
        /// </summary>
        /// <returns>Bản copy của dữ liệu</returns>
        public List<InformationObject> GetLastReadData()
        {
            lock (lockObject)
            {
                return new List<InformationObject>(LastReadData.Values);
            }
        }

        public InformationObject GetSingleDataPoint(uint ioa)
        {
            lock (lockObject)
            {
                if (LastReadData.TryGetValue(ioa, out var infoObject))
                {
                    return infoObject;
                }
            }
            return null;
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

        /// <summary>Block ID duy nhất</summary>
        public override string BlockID => $"{CommonAddress}-{Name}";
        #endregion

        #region METHODS

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

    // --- Bỏ các class không cần thiết ở đây như IOARange, BlockReadStrategy, BlockStatistics ---
    // Lý do: Các chiến lược đọc phức tạp đã được đơn giản hóa.

    #endregion
}