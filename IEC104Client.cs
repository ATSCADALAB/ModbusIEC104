using System;
using System.Collections.Generic;
using System.Threading;

namespace IEC104
{
    /// <summary>
    /// IEC104 Client - lớp chính quản lý kết nối và giao tiếp IEC104
    /// Tương tự ModbusTCPClient trong source gốc
    /// </summary>
    public class IEC104Client : IDisposable
    {
        #region FIELDS

        private string ipAddress = "127.0.0.1";
        private int port = IEC104Constants.DEFAULT_PORT;
        private int timeOut = 10000;
        private readonly IEC104Socket iec104Socket;
        private readonly byte[] receiveBuffer = new byte[1024];
        private readonly object sequenceLock = new object();

        // Sequence numbers for I-frames
        private ushort sendSequenceNumber = 0;
        private ushort receiveSequenceNumber = 0;

        // Protocol parameters
        private ushort k = IEC104Constants.DEFAULT_K;  // Max difference send/receive
        private ushort w = IEC104Constants.DEFAULT_W;  // Acknowledge window

        // Timeouts
        private ushort t0 = IEC104Constants.DEFAULT_T0; // Connection timeout
        private ushort t1 = IEC104Constants.DEFAULT_T1; // Send timeout  
        private ushort t2 = IEC104Constants.DEFAULT_T2; // Acknowledge timeout
        private ushort t3 = IEC104Constants.DEFAULT_T3; // Test frame timeout

        // Cache for received data
        private readonly Dictionary<uint, InformationObject> dataCache;
        private readonly object cacheLock = new object();

        #endregion

        #region PROPERTIES

        /// <summary>Tên client</summary>
        public string Name { get; }

        /// <summary>Địa chỉ IP server</summary>
        public string IpAddress
        {
            get => this.ipAddress;
            set => this.ipAddress = value ?? "127.0.0.1";
        }

        /// <summary>Port server</summary>
        public int Port
        {
            get => this.port;
            set => this.port = value > 0 ? value : IEC104Constants.DEFAULT_PORT;
        }

        /// <summary>Timeout chung (ms)</summary>
        public int TimeOut
        {
            get => this.timeOut;
            set
            {
                if (value < 1000) return;
                this.timeOut = value;
                if (this.iec104Socket != null)
                {
                    this.iec104Socket.ConnectTimeout = value;
                    this.iec104Socket.ReceiveTimeout = value;
                    this.iec104Socket.SendTimeout = value;
                }
            }
        }

        /// <summary>Trạng thái kết nối TCP</summary>
        public bool Connected => this.iec104Socket?.Connected ?? false;

        /// <summary>Trạng thái kết nối IEC104</summary>
        public IEC104ConnectionState State => this.iec104Socket?.State ?? IEC104ConnectionState.Disconnected;

        /// <summary>Sẵn sàng truyền dữ liệu</summary>
        public bool IsReadyForDataTransfer => this.iec104Socket?.IsReadyForDataTransfer() ?? false;

        /// <summary>Protocol parameter K</summary>
        public ushort K
        {
            get => this.k;
            set => this.k = value > 0 ? value : IEC104Constants.DEFAULT_K;
        }

        /// <summary>Protocol parameter W</summary>
        public ushort W
        {
            get => this.w;
            set => this.w = value > 0 ? value : IEC104Constants.DEFAULT_W;
        }

        /// <summary>T0 timeout (connection)</summary>
        public ushort T0
        {
            get => this.t0;
            set => this.t0 = value > 0 ? value : IEC104Constants.DEFAULT_T0;
        }

        /// <summary>T1 timeout (send)</summary>
        public ushort T1
        {
            get => this.t1;
            set => this.t1 = value > 0 ? value : IEC104Constants.DEFAULT_T1;
        }

        /// <summary>T2 timeout (acknowledge)</summary>
        public ushort T2
        {
            get => this.t2;
            set => this.t2 = value > 0 ? value : IEC104Constants.DEFAULT_T2;
        }

        /// <summary>T3 timeout (test frame)</summary>
        public ushort T3
        {
            get => this.t3;
            set => this.t3 = value > 0 ? value : IEC104Constants.DEFAULT_T3;
        }

        #endregion

        #region CONSTRUCTORS

        /// <summary>
        /// Constructor mặc định
        /// </summary>
        public IEC104Client()
        {
            this.iec104Socket = new IEC104Socket();
            this.dataCache = new Dictionary<uint, InformationObject>();
        }

        /// <summary>
        /// Constructor với tên
        /// </summary>
        /// <param name="name">Tên client</param>
        public IEC104Client(string name) : this()
        {
            Name = name;
        }

        /// <summary>
        /// Destructor
        /// </summary>
        ~IEC104Client()
        {
            Dispose();
        }

        #endregion

        #region CONNECTION METHODS

        /// <summary>
        /// Kết nối đến server IEC104
        /// </summary>
        /// <returns>Mã lỗi (0 = thành công)</returns>
        public int Connect()
        {
            try
            {
                // Set timeouts
                this.iec104Socket.ConnectTimeout = this.t0 * 1000;
                this.iec104Socket.SendTimeout = this.t1 * 1000;
                this.iec104Socket.ReceiveTimeout = this.t2 * 1000;

                // Kết nối TCP
                int result = this.iec104Socket.Connect(this.ipAddress, this.port);
                if (result != IEC104Constants.RESULT_OK)
                {
                    return result;
                }

                // Bắt đầu truyền dữ liệu (STARTDT)
                result = this.iec104Socket.StartDataTransfer();
                if (result != IEC104Constants.RESULT_OK)
                {
                    this.iec104Socket.Close();
                    return result;
                }

                // Reset sequence numbers
                lock (this.sequenceLock)
                {
                    this.sendSequenceNumber = 0;
                    this.receiveSequenceNumber = 0;
                }

                // Clear cache
                lock (this.cacheLock)
                {
                    this.dataCache.Clear();
                }

                return IEC104Constants.RESULT_OK;
            }
            catch
            {
                return IEC104Constants.ERR_CONNECTION_FAILED;
            }
        }

        /// <summary>
        /// Kết nối đến địa chỉ cụ thể
        /// </summary>
        /// <param name="ipAddress">Địa chỉ IP</param>
        /// <param name="port">Port</param>
        /// <returns>Mã lỗi (0 = thành công)</returns>
        public int ConnectTo(string ipAddress, int port)
        {
            this.IpAddress = ipAddress;
            this.Port = port;
            return Connect();
        }

        /// <summary>
        /// Ngắt kết nối
        /// </summary>
        /// <returns>Mã lỗi (0 = thành công)</returns>
        public int Disconnect()
        {
            try
            {
                // Dừng truyền dữ liệu (STOPDT)
                this.iec104Socket?.StopDataTransfer();

                // Đóng kết nối TCP
                this.iec104Socket?.Close();

                return IEC104Constants.RESULT_OK;
            }
            catch
            {
                return IEC104Constants.RESULT_ERROR;
            }
        }

        #endregion

        #region SEQUENCE NUMBER MANAGEMENT

        /// <summary>
        /// Lấy send sequence number tiếp theo
        /// </summary>
        /// <returns>Send sequence number</returns>
        private ushort GetNextSendSequenceNumber()
        {
            lock (this.sequenceLock)
            {
                ushort current = this.sendSequenceNumber;
                this.sendSequenceNumber = (ushort)((this.sendSequenceNumber + 1) & 0x7FFF); // 15 bits
                return current;
            }
        }

        /// <summary>
        /// Cập nhật receive sequence number
        /// </summary>
        /// <param name="sequenceNumber">Sequence number nhận được</param>
        private void UpdateReceiveSequenceNumber(ushort sequenceNumber)
        {
            lock (this.sequenceLock)
            {
                this.receiveSequenceNumber = (ushort)((sequenceNumber + 1) & 0x7FFF); // 15 bits
            }
        }

        /// <summary>
        /// Lấy receive sequence number hiện tại
        /// </summary>
        /// <returns>Receive sequence number</returns>
        private ushort GetCurrentReceiveSequenceNumber()
        {
            lock (this.sequenceLock)
            {
                return this.receiveSequenceNumber;
            }
        }

        #endregion

        #region DATA METHODS

        /// <summary>
        /// Gửi General Interrogation
        /// </summary>
        /// <param name="commonAddress">Common Address</param>
        /// <param name="qualifier">Qualifier of Interrogation</param>
        /// <returns>Mã lỗi (0 = thành công)</returns>
        public int SendGeneralInterrogation(ushort commonAddress, byte qualifier = IEC104Constants.QOI_STATION)
        {
            if (!IsReadyForDataTransfer)
            {
                return IEC104Constants.ERR_NOT_CONNECTED;
            }

            try
            {
                // Tạo ASDU cho interrogation
                var asdu = ASDU.CreateInterrogationCommand(commonAddress, qualifier);
                if (asdu == null)
                {
                    return IEC104Constants.ERR_INVALID_FRAME;
                }

                // Chuyển ASDU thành byte array
                byte[] asduData = asdu.ToByteArray();
                if (asduData == null)
                {
                    return IEC104Constants.ERR_INVALID_FRAME;
                }

                // Gửi I-frame
                ushort sendSeq = GetNextSendSequenceNumber();
                ushort recvSeq = GetCurrentReceiveSequenceNumber();

                int result = this.iec104Socket.SendIFrame(sendSeq, recvSeq, asduData);
                if (result != IEC104Constants.RESULT_OK)
                {
                    return result;
                }

                // Nhận confirmation
                return ReceiveConfirmation(commonAddress, IEC104Constants.C_IC_NA_1);
            }
            catch
            {
                return IEC104Constants.RESULT_ERROR;
            }
        }

        /// <summary>
        /// Gửi Single Command
        /// </summary>
        /// <param name="commonAddress">Common Address</param>
        /// <param name="ioa">Information Object Address</param>
        /// <param name="commandValue">Command value (true/false)</param>
        /// <param name="selectBeforeOperate">Select before operate</param>
        /// <param name="qualifier">Qualifier of command</param>
        /// <returns>Mã lỗi (0 = thành công)</returns>
        public int SendSingleCommand(ushort commonAddress, uint ioa, bool commandValue,
            bool selectBeforeOperate = false, byte qualifier = IEC104Constants.QU_NO_ADDITIONAL)
        {
            if (!IsReadyForDataTransfer)
            {
                return IEC104Constants.ERR_NOT_CONNECTED;
            }

            try
            {
                if (selectBeforeOperate)
                {
                    // Select-Before-Operate: gửi SELECT trước
                    int selectResult = SendSingleCommandInternal(commonAddress, ioa, commandValue, true, qualifier);
                    if (selectResult != IEC104Constants.RESULT_OK)
                    {
                        return selectResult;
                    }

                    // Đợi một chút rồi gửi EXECUTE
                    Thread.Sleep(100);
                }

                // Gửi EXECUTE command
                return SendSingleCommandInternal(commonAddress, ioa, commandValue, false, qualifier);
            }
            catch
            {
                return IEC104Constants.RESULT_ERROR;
            }
        }

        /// <summary>
        /// Gửi Single Command internal
        /// </summary>
        private int SendSingleCommandInternal(ushort commonAddress, uint ioa, bool commandValue,
            bool select, byte qualifier)
        {
            // Tạo ASDU cho single command
            var asdu = ASDU.CreateSingleCommand(commonAddress, ioa, commandValue, select, qualifier);
            if (asdu == null)
            {
                return IEC104Constants.ERR_INVALID_FRAME;
            }

            // Chuyển ASDU thành byte array
            byte[] asduData = asdu.ToByteArray();
            if (asduData == null)
            {
                return IEC104Constants.ERR_INVALID_FRAME;
            }

            // Gửi I-frame
            ushort sendSeq = GetNextSendSequenceNumber();
            ushort recvSeq = GetCurrentReceiveSequenceNumber();

            int result = this.iec104Socket.SendIFrame(sendSeq, recvSeq, asduData);
            if (result != IEC104Constants.RESULT_OK)
            {
                return result;
            }

            // Nhận confirmation
            return ReceiveConfirmation(commonAddress, IEC104Constants.C_SC_NA_1);
        }

        /// <summary>
        /// Đọc dữ liệu từ cache hoặc từ server
        /// </summary>
        /// <param name="commonAddress">Common Address</param>
        /// <param name="ioa">Information Object Address</param>
        /// <param name="infoObject">Information Object nhận được</param>
        /// <returns>Mã lỗi (0 = thành công)</returns>
        public int ReadInformationObject(ushort commonAddress, uint ioa, out InformationObject infoObject)
        {
            infoObject = null;

            // Kiểm tra cache trước
            lock (this.cacheLock)
            {
                if (this.dataCache.TryGetValue(ioa, out infoObject))
                {
                    return IEC104Constants.RESULT_OK;
                }
            }

            // Nếu không có trong cache, gửi General Interrogation
            int result = SendGeneralInterrogation(commonAddress);
            if (result != IEC104Constants.RESULT_OK)
            {
                return result;
            }

            // Kiểm tra cache lại sau khi interrogation
            lock (this.cacheLock)
            {
                if (this.dataCache.TryGetValue(ioa, out infoObject))
                {
                    return IEC104Constants.RESULT_OK;
                }
            }

            return IEC104Constants.RESULT_ERROR;
        }

        /// <summary>
        /// Nhận và xử lý các frame spontaneous từ server
        /// </summary>
        /// <param name="informationObjects">Danh sách Information Objects nhận được</param>
        /// <returns>Mã lỗi (0 = thành công)</returns>
        public int ReceiveSpontaneousData(out List<InformationObject> informationObjects)
        {
            informationObjects = new List<InformationObject>();

            if (!IsReadyForDataTransfer)
            {
                return IEC104Constants.ERR_NOT_CONNECTED;
            }

            try
            {
                // Đặt timeout ngắn để không block quá lâu
                int originalTimeout = this.iec104Socket.ReceiveTimeout;
                this.iec104Socket.ReceiveTimeout = 1000; // 1 second

                try
                {
                    int result = this.iec104Socket.ReceiveFrame(this.receiveBuffer, this.receiveBuffer.Length, out int frameLength);
                    if (result != IEC104Constants.RESULT_OK)
                    {
                        return result;
                    }

                    // Parse frame
                    var frame = IEC104Frame.FromByteArray(this.receiveBuffer);
                    if (frame == null || !frame.IsValid)
                    {
                        return IEC104Constants.ERR_INVALID_FRAME;
                    }

                    // Xử lý frame dựa trên loại
                    return ProcessReceivedFrame(frame, informationObjects);
                }
                finally
                {
                    this.iec104Socket.ReceiveTimeout = originalTimeout;
                }
            }
            catch
            {
                return IEC104Constants.ERR_RECEIVE_TIMEOUT;
            }
        }

        #endregion

        #region FRAME PROCESSING

        /// <summary>
        /// Xử lý frame nhận được
        /// </summary>
        /// <param name="frame">Frame nhận được</param>
        /// <param name="informationObjects">Danh sách Information Objects</param>
        /// <returns>Mã lỗi (0 = thành công)</returns>
        private int ProcessReceivedFrame(IEC104Frame frame, List<InformationObject> informationObjects)
        {
            if (frame.IsIFrame())
            {
                // I-frame: chứa ASDU data
                UpdateReceiveSequenceNumber(frame.SendSequenceNumber);

                // Parse ASDU
                if (frame.ASDUData != null && frame.ASDUData.Length > 0)
                {
                    var asdu = ASDU.FromByteArray(frame.ASDUData);
                    if (asdu != null && asdu.IsValid)
                    {
                        // Cập nhật cache và trả về data
                        lock (this.cacheLock)
                        {
                            foreach (var infoObj in asdu.InformationObjects)
                            {
                                this.dataCache[infoObj.InformationObjectAddress] = infoObj;
                                informationObjects.Add(infoObj);
                            }
                        }

                        // Gửi S-frame để acknowledge
                        ushort recvSeq = GetCurrentReceiveSequenceNumber();
                        this.iec104Socket.SendSFrame(recvSeq);
                    }
                }

                return IEC104Constants.RESULT_OK;
            }
            else if (frame.IsSFrame())
            {
                // S-frame: acknowledge, không cần xử lý gì
                return IEC104Constants.RESULT_OK;
            }
            else if (frame.IsUFrame(UFrameFunction.TESTFR_ACT))
            {
                // Test frame activation: gửi confirmation
                return this.iec104Socket.SendUFrame(UFrameFunction.TESTFR_CON);
            }

            return IEC104Constants.RESULT_OK;
        }

        /// <summary>
        /// Nhận confirmation cho command đã gửi
        /// </summary>
        /// <param name="expectedCA">Common Address mong đợi</param>
        /// <param name="expectedTypeID">Type ID mong đợi</param>
        /// <returns>Mã lỗi (0 = thành công)</returns>
        private int ReceiveConfirmation(ushort expectedCA, byte expectedTypeID)
        {
            try
            {
                int result = this.iec104Socket.ReceiveFrame(this.receiveBuffer, this.receiveBuffer.Length, out int frameLength);
                if (result != IEC104Constants.RESULT_OK)
                {
                    return result;
                }

                // Parse frame
                var frame = IEC104Frame.FromByteArray(this.receiveBuffer);
                if (frame == null || !frame.IsValid || !frame.IsIFrame())
                {
                    return IEC104Constants.ERR_INVALID_FRAME;
                }

                // Update sequence number
                UpdateReceiveSequenceNumber(frame.SendSequenceNumber);

                // Parse ASDU
                if (frame.ASDUData != null && frame.ASDUData.Length > 0)
                {
                    var asdu = ASDU.FromByteArray(frame.ASDUData);
                    if (asdu != null && asdu.IsValid)
                    {
                        // Kiểm tra confirmation
                        if (asdu.CommonAddress == expectedCA &&
                            asdu.TypeID == expectedTypeID &&
                            (asdu.CauseOfTransmission == IEC104Constants.COT_ACTIVATION_CON ||
                             asdu.CauseOfTransmission == IEC104Constants.COT_DEACTIVATION_CON))
                        {
                            // Gửi S-frame để acknowledge
                            ushort recvSeq = GetCurrentReceiveSequenceNumber();
                            this.iec104Socket.SendSFrame(recvSeq);

                            // Kiểm tra negative confirmation
                            if (asdu.IsNegativeConfirmation())
                            {
                                return IEC104Constants.RESULT_ERROR;
                            }

                            return IEC104Constants.RESULT_OK;
                        }
                    }
                }

                return IEC104Constants.ERR_INVALID_FRAME;
            }
            catch
            {
                return IEC104Constants.ERR_RECEIVE_TIMEOUT;
            }
        }

        #endregion

        #region TEST AND UTILITY METHODS

        /// <summary>
        /// Gửi test frame để kiểm tra kết nối
        /// </summary>
        /// <returns>Mã lỗi (0 = thành công)</returns>
        public int SendTestFrame()
        {
            if (!IsReadyForDataTransfer)
            {
                return IEC104Constants.ERR_NOT_CONNECTED;
            }

            return this.iec104Socket.SendTestFrame();
        }

        /// <summary>
        /// Clear cache dữ liệu
        /// </summary>
        public void ClearCache()
        {
            lock (this.cacheLock)
            {
                this.dataCache.Clear();
            }
        }

        /// <summary>
        /// Lấy số lượng objects trong cache
        /// </summary>
        /// <returns>Số lượng objects</returns>
        public int GetCacheCount()
        {
            lock (this.cacheLock)
            {
                return this.dataCache.Count;
            }
        }

        #endregion

        #region DISPOSE

        /// <summary>
        /// Giải phóng tài nguyên
        /// </summary>
        public void Dispose()
        {
            try
            {
                Disconnect();
                this.iec104Socket?.Close();
            }
            catch
            {
                // Ignore exceptions during dispose
            }
        }

        #endregion

        #region DEBUG

        /// <summary>
        /// Lấy thông tin client dưới dạng string để debug
        /// </summary>
        /// <returns>String mô tả client</returns>
        public override string ToString()
        {
            return $"IEC104Client: {Name ?? "Unnamed"}, {IpAddress}:{Port}, State={State}, Cache={GetCacheCount()}";
        }

        #endregion
    }
}