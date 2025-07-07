using System;

namespace IEC104
{
    /// <summary>
    /// Counter Interrogation Type - MISSING TYPE FIXED
    /// </summary>
    public enum CounterInterrogationType : byte
    {
        /// <summary>Request counter group 1</summary>
        Group1 = IEC104Constants.QCC_RQT_GROUP_1,

        /// <summary>Request counter group 2</summary>
        Group2 = IEC104Constants.QCC_RQT_GROUP_2,

        /// <summary>Request counter group 3</summary>
        Group3 = IEC104Constants.QCC_RQT_GROUP_3,

        /// <summary>Request counter group 4</summary>
        Group4 = IEC104Constants.QCC_RQT_GROUP_4,

        /// <summary>General counter request</summary>
        General = IEC104Constants.QCC_RQT_GENERAL
    }

    /// <summary>
    /// Double Point State - MISSING TYPE FIXED
    /// </summary>
    public enum DoublePointState : byte
    {
        /// <summary>Indeterminate or intermediate state</summary>
        Indeterminate = IEC104Constants.DPI_INDETERMINATE,

        /// <summary>OFF state</summary>
        OFF = IEC104Constants.DPI_OFF,

        /// <summary>ON state</summary>
        ON = IEC104Constants.DPI_ON,

        /// <summary>Indeterminate state (alternative)</summary>
        Indeterminate2 = IEC104Constants.DPI_INDETERMINATE_2
    }

    /// <summary>
    /// IEC104 Client Statistics - MISSING TYPE FIXED
    /// </summary>
    public class IEC104ClientStatistics
    {
        #region CONNECTION STATISTICS
        /// <summary>Thời gian kết nối</summary>
        public DateTime ConnectTime { get; set; }

        /// <summary>Tổng thời gian kết nối (seconds)</summary>
        public double TotalConnectedTime { get; set; }

        /// <summary>Số lần kết nối thành công</summary>
        public int SuccessfulConnections { get; set; }

        /// <summary>Số lần kết nối thất bại</summary>
        public int FailedConnections { get; set; }

        /// <summary>Số lần ngắt kết nối</summary>
        public int DisconnectionCount { get; set; }
        #endregion

        #region FRAME STATISTICS
        /// <summary>Số I-frames đã gửi</summary>
        public long ISentFrames { get; set; }

        /// <summary>Số I-frames đã nhận</summary>
        public long IReceivedFrames { get; set; }

        /// <summary>Số S-frames đã gửi</summary>
        public long SSentFrames { get; set; }

        /// <summary>Số S-frames đã nhận</summary>
        public long SReceivedFrames { get; set; }

        /// <summary>Số U-frames đã gửi</summary>
        public long USentFrames { get; set; }

        /// <summary>Số U-frames đã nhận</summary>
        public long UReceivedFrames { get; set; }

        /// <summary>Tổng số bytes đã gửi</summary>
        public long TotalBytesSent { get; set; }

        /// <summary>Tổng số bytes đã nhận</summary>
        public long TotalBytesReceived { get; set; }
        #endregion

        #region ERROR STATISTICS
        /// <summary>Số lỗi frame</summary>
        public int FrameErrors { get; set; }

        /// <summary>Số lỗi timeout</summary>
        public int TimeoutErrors { get; set; }

        /// <summary>Số lỗi sequence number</summary>
        public int SequenceErrors { get; set; }

        /// <summary>Số frame invalid</summary>
        public int InvalidFrames { get; set; }

        /// <summary>Lỗi cuối cùng</summary>
        public string LastError { get; set; }

        /// <summary>Thời gian lỗi cuối</summary>
        public DateTime LastErrorTime { get; set; }
        #endregion

        #region COMMAND STATISTICS
        /// <summary>Số commands đã gửi</summary>
        public int CommandsSent { get; set; }

        /// <summary>Số commands thành công</summary>
        public int CommandsSuccessful { get; set; }

        /// <summary>Số commands thất bại</summary>
        public int CommandsFailed { get; set; }

        /// <summary>Số interrogations đã gửi</summary>
        public int InterrogationsSent { get; set; }

        /// <summary>Số test frames đã gửi</summary>
        public int TestFramesSent { get; set; }
        #endregion

        #region DATA STATISTICS
        /// <summary>Số objects trong cache</summary>
        public int CachedObjects { get; set; }

        /// <summary>Số spontaneous data objects nhận được</summary>
        public int SpontaneousDataCount { get; set; }

        /// <summary>Thời gian cập nhật cache cuối</summary>
        public DateTime LastCacheUpdate { get; set; }

        /// <summary>Thời gian interrogation cuối</summary>
        public DateTime LastInterrogationTime { get; set; }
        #endregion

        #region CONSTRUCTORS
        /// <summary>
        /// Constructor mặc định
        /// </summary>
        public IEC104ClientStatistics()
        {
            ConnectTime = DateTime.MinValue;
            LastErrorTime = DateTime.MinValue;
            LastCacheUpdate = DateTime.MinValue;
            LastInterrogationTime = DateTime.MinValue;
            LastError = string.Empty;
        }
        #endregion

        #region METHODS
        /// <summary>
        /// Reset tất cả statistics
        /// </summary>
        public void Reset()
        {
            // Connection stats
            TotalConnectedTime = 0;
            SuccessfulConnections = 0;
            FailedConnections = 0;
            DisconnectionCount = 0;

            // Frame stats
            ISentFrames = 0;
            IReceivedFrames = 0;
            SSentFrames = 0;
            SReceivedFrames = 0;
            USentFrames = 0;
            UReceivedFrames = 0;
            TotalBytesSent = 0;
            TotalBytesReceived = 0;

            // Error stats
            FrameErrors = 0;
            TimeoutErrors = 0;
            SequenceErrors = 0;
            InvalidFrames = 0;
            LastError = string.Empty;
            LastErrorTime = DateTime.MinValue;

            // Command stats
            CommandsSent = 0;
            CommandsSuccessful = 0;
            CommandsFailed = 0;
            InterrogationsSent = 0;
            TestFramesSent = 0;

            // Data stats
            CachedObjects = 0;
            SpontaneousDataCount = 0;
            LastCacheUpdate = DateTime.MinValue;
            LastInterrogationTime = DateTime.MinValue;
        }

        /// <summary>
        /// Lấy success rate cho connections
        /// </summary>
        /// <returns>Success rate (0.0 - 1.0)</returns>
        public double GetConnectionSuccessRate()
        {
            int total = SuccessfulConnections + FailedConnections;
            return total > 0 ? (double)SuccessfulConnections / total : 0.0;
        }

        /// <summary>
        /// Lấy success rate cho commands
        /// </summary>
        /// <returns>Success rate (0.0 - 1.0)</returns>
        public double GetCommandSuccessRate()
        {
            return CommandsSent > 0 ? (double)CommandsSuccessful / CommandsSent : 0.0;
        }

        /// <summary>
        /// Lấy tổng số frames
        /// </summary>
        /// <returns>Tổng số frames đã gửi và nhận</returns>
        public long GetTotalFrames()
        {
            return ISentFrames + IReceivedFrames + SSentFrames + SReceivedFrames + USentFrames + UReceivedFrames;
        }

        /// <summary>
        /// Lấy tổng số errors
        /// </summary>
        /// <returns>Tổng số errors</returns>
        public int GetTotalErrors()
        {
            return FrameErrors + TimeoutErrors + SequenceErrors + InvalidFrames;
        }

        /// <summary>
        /// Override ToString
        /// </summary>
        /// <returns>String representation</returns>
        public override string ToString()
        {
            return $"IEC104ClientStats[Conn:{SuccessfulConnections}/{SuccessfulConnections + FailedConnections}, " +
                   $"I-Frames:{ISentFrames}/{IReceivedFrames}, Commands:{CommandsSuccessful}/{CommandsSent}, " +
                   $"Cache:{CachedObjects}, Errors:{GetTotalErrors()}]";
        }
        #endregion
    }

    /// <summary>
    /// IEC104 Information Object Value - Extended Information Object
    /// </summary>
    public class IEC104InformationObjectValue
    {
        /// <summary>IOA address</summary>
        public uint InformationObjectAddress { get; set; }

        /// <summary>Type ID</summary>
        public byte TypeID { get; set; }

        /// <summary>Raw value</summary>
        public object Value { get; set; }

        /// <summary>Quality descriptor</summary>
        public IEC104QualityDescriptor Quality { get; set; }

        /// <summary>Timestamp</summary>
        public DateTime TimeStamp { get; set; }

        /// <summary>Cause of transmission</summary>
        public byte CauseOfTransmission { get; set; }

        /// <summary>
        /// Constructor
        /// </summary>
        public IEC104InformationObjectValue()
        {
            Quality = new IEC104QualityDescriptor(0);
            TimeStamp = DateTime.Now;
        }

        /// <summary>
        /// Kiểm tra quality có tốt không
        /// </summary>
        /// <returns>True nếu quality tốt</returns>
        public bool IsGoodQuality()
        {
            return Quality.IsGood;
        }

        /// <summary>
        /// Override ToString
        /// </summary>
        /// <returns>String representation</returns>
        public override string ToString()
        {
            return $"IOA:{InformationObjectAddress}, Type:{TypeID}, Value:{Value}, Quality:{Quality}";
        }
    }

    /// <summary>
    /// IEC104 Command Execution Mode
    /// </summary>
    public enum CommandExecutionMode
    {
        /// <summary>Direct execute (không cần select)</summary>
        DirectExecute = 0,

        /// <summary>Select before operate</summary>
        SelectBeforeOperate = 1
    }

    /// <summary>
    /// IEC104 Connection State Events
    /// </summary>
    public enum IEC104ConnectionEvent
    {
        /// <summary>TCP connection established</summary>
        TcpConnected,

        /// <summary>TCP connection lost</summary>
        TcpDisconnected,

        /// <summary>STARTDT activated</summary>
        StartDataTransfer,

        /// <summary>STOPDT activated</summary>
        StopDataTransfer,

        /// <summary>Test frame sent/received</summary>
        TestFrame,

        /// <summary>Connection error occurred</summary>
        ConnectionError,

        /// <summary>Timeout occurred</summary>
        Timeout
    }

    /// <summary>
    /// IEC104 Data Direction
    /// </summary>
    public enum IEC104DataDirection
    {
        /// <summary>Monitoring direction (from outstation to controlling station)</summary>
        Monitoring = 1,

        /// <summary>Control direction (from controlling station to outstation)</summary>
        Control = 2,

        /// <summary>Both directions</summary>
        Both = 3
    }
}