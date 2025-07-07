using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ModbusIEC104.Common;

namespace ModbusIEC104
{
    /// <summary>
    /// Client chính cho giao thức IEC 60870-5-104
    /// </summary>
    public class IEC104Client : IDisposable
    {
        #region FIELDS
        private IEC104Socket socket;
        private readonly object lockObject = new object();
        private readonly byte[] receiveBuffer = new byte[IEC104Constants.MAX_APDU_LENGTH + 10];
        private bool isDisposed = false;

        // Sequence numbers
        private ushort sendSequenceNumber = 0;
        private ushort receiveSequenceNumber = 0;

        // Protocol parameters
        private ushort parameterK = IEC104Constants.DEFAULT_K_PARAMETER;
        private ushort parameterW = IEC104Constants.DEFAULT_W_PARAMETER;
        private ushort timeoutT0 = IEC104Constants.DEFAULT_T0_TIMEOUT;
        private ushort timeoutT1 = IEC104Constants.DEFAULT_T1_TIMEOUT;
        private ushort timeoutT2 = IEC104Constants.DEFAULT_T2_TIMEOUT;
        private ushort timeoutT3 = IEC104Constants.DEFAULT_T3_TIMEOUT;

        // Timers
        private Timer t1Timer; // Send/Test timeout
        private Timer t2Timer; // Acknowledge timeout
        private Timer t3Timer; // Test frame timeout

        // --- FIX: Thay đổi Queue từ IEC104Frame sang ASDU ---
        // Lý do: Cần truy cập vào thông tin trong ASDU (như COT) ở các lớp cao hơn.
        private readonly Queue<ASDU> asduQueue = new Queue<ASDU>();
        private readonly Queue<IEC104Frame> U_S_FrameQueue = new Queue<IEC104Frame>(); // Queue cho U và S frame nếu cần

        // State
        private IEC104ConnectionState connectionState = IEC104ConnectionState.Disconnected;
        private int unacknowledgedISent = 0;
        private int unacknowledgedIReceived = 0;
        private DateTime lastSentTime = DateTime.MinValue;
        private DateTime lastReceivedTime = DateTime.MinValue;
        #endregion

        #region PROPERTIES
        /// <summary>Tên client</summary>
        public string Name { get; private set; }

        /// <summary>Địa chỉ IP server</summary>
        public string IpAddress { get; private set; }

        /// <summary>Cổng kết nối</summary>
        public int Port { get; private set; }

        /// <summary>Timeout chung</summary>
        public int TimeOut { get; set; } = IEC104Constants.DEFAULT_READ_TIMEOUT;

        /// <summary>Trạng thái kết nối TCP</summary>
        public bool Connected => socket?.Connected == true;

        /// <summary>Trạng thái giao thức IEC104</summary>
        public IEC104ConnectionState State => connectionState;

        /// <summary>Kiểm tra xem có đang trong trạng thái truyền dữ liệu không</summary>
        public bool IsDataTransferActive => connectionState == IEC104ConnectionState.DataTransferStarted;

        /// <summary>Common Address sử dụng</summary>
        public ushort CommonAddress { get; set; } = 1;

        /// <summary>Lỗi cuối cùng</summary>
        public string LastError { get; private set; }

        /// <summary>Số I-frame đã gửi</summary>
        public long ISentCount { get; private set; }

        /// <summary>Số I-frame đã nhận</summary>
        public long IReceivedCount { get; private set; }

        /// <summary>Số S-frame đã gửi</summary>
        public long SSentCount { get; private set; }

        /// <summary>Số U-frame đã gửi</summary>
        public long USentCount { get; private set; }
        #endregion

        #region PROTOCOL PARAMETERS PROPERTIES
        /// <summary>Tham số K - Số I-frame tối đa không được ACK</summary>
        public ushort ParameterK
        {
            get => parameterK;
            set => parameterK = value > 0 ? value : IEC104Constants.DEFAULT_K_PARAMETER;
        }

        /// <summary>Tham số W - Số I-frame trước khi gửi ACK</summary>
        public ushort ParameterW
        {
            get => parameterW;
            set => parameterW = value > 0 ? value : IEC104Constants.DEFAULT_W_PARAMETER;
        }

        /// <summary>Timeout T0 - Kết nối (giây)</summary>
        public ushort TimeoutT0
        {
            get => timeoutT0;
            set => timeoutT0 = value > 0 ? value : IEC104Constants.DEFAULT_T0_TIMEOUT;
        }

        /// <summary>Timeout T1 - Gửi/Test (giây)</summary>
        public ushort TimeoutT1
        {
            get => timeoutT1;
            set => timeoutT1 = value > 0 ? value : IEC104Constants.DEFAULT_T1_TIMEOUT;
        }

        /// <summary>Timeout T2 - ACK (giây)</summary>
        public ushort TimeoutT2
        {
            get => timeoutT2;
            set => timeoutT2 = value > 0 ? value : IEC104Constants.DEFAULT_T2_TIMEOUT;
        }

        /// <summary>Timeout T3 - Test khi idle (giây)</summary>
        public ushort TimeoutT3
        {
            get => timeoutT3;
            set => timeoutT3 = value > 0 ? value : IEC104Constants.DEFAULT_T3_TIMEOUT;
        }
        #endregion

        #region CONSTRUCTORS
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="ipAddress">Địa chỉ IP</param>
        /// <param name="port">Cổng kết nối</param>
        /// <param name="name">Tên client</param>
        public IEC104Client(string ipAddress, int port = IEC104Constants.DEFAULT_PORT, string name = null)
        {
            IpAddress = ipAddress ?? throw new ArgumentNullException(nameof(ipAddress));
            Port = port > 0 ? port : IEC104Constants.DEFAULT_PORT;
            Name = name ?? $"IEC104Client_{ipAddress}_{port}";

            socket = new IEC104Socket(IpAddress, Port);
        }
        #endregion

        #region CONNECTION METHODS
        /// <summary>
        /// Kết nối đến server
        /// </summary>
        /// <returns>Mã lỗi (0 = thành công)</returns>
        public int Connect()
        {
            try
            {
                lock (lockObject)
                {
                    if (Connected && connectionState != IEC104ConnectionState.Disconnected)
                        return IEC104Constants.ERROR_NONE;

                    // Reset state
                    connectionState = IEC104ConnectionState.Disconnected;
                    ResetSequenceNumbers();
                    ResetCounters();

                    // Kết nối TCP
                    if (!socket.Connect())
                    {
                        LastError = "Failed to establish TCP connection";
                        return IEC104Constants.ERROR_CONNECTION_TIMEOUT;
                    }

                    connectionState = IEC104ConnectionState.Connected;

                    // Khởi động timers
                    StartTimers();

                    // Bắt đầu một luồng riêng để xử lý dữ liệu nhận được
                    Task.Run(() => ProcessReceiveLoop());

                    return IEC104Constants.ERROR_NONE;
                }
            }
            catch (Exception ex)
            {
                LastError = $"Connect error: {ex.Message}";
                return IEC104Constants.ERROR_SOCKET;
            }
        }

        /// <summary>
        /// Bắt đầu truyền dữ liệu (gửi STARTDT)
        /// </summary>
        /// <returns>Mã lỗi (0 = thành công)</returns>
        public int StartDataTransfer()
        {
            try
            {
                lock (lockObject)
                {
                    if (!Connected)
                        return IEC104Constants.ERROR_SOCKET;

                    if (connectionState == IEC104ConnectionState.DataTransferStarted)
                        return IEC104Constants.ERROR_NONE;

                    // Gửi STARTDT ACT
                    var startdtFrame = IEC104Frame.CreateUFrame(UFrameFunction.STARTDT_ACT);
                    if (!SendFrameInternal(startdtFrame))
                    {
                        LastError = "Failed to send STARTDT ACT";
                        return IEC104Constants.ERROR_SOCKET;
                    }

                    USentCount++;

                    // Đợi STARTDT CON với timeout T1
                    var response = WaitForUFrame(UFrameFunction.STARTDT_CON, TimeoutT1 * 1000);
                    if (response == null)
                    {
                        LastError = "STARTDT CON timeout";
                        return IEC104Constants.ERROR_STARTDT_REJECTED;
                    }

                    connectionState = IEC104ConnectionState.DataTransferStarted;
                    return IEC104Constants.ERROR_NONE;
                }
            }
            catch (Exception ex)
            {
                LastError = $"StartDataTransfer error: {ex.Message}";
                return IEC104Constants.ERROR_SOCKET;
            }
        }

        /// <summary>
        /// Dừng truyền dữ liệu (gửi STOPDT)
        /// </summary>
        /// <returns>Mã lỗi (0 = thành công)</returns>
        public int StopDataTransfer()
        {
            try
            {
                lock (lockObject)
                {
                    if (!Connected)
                        return IEC104Constants.ERROR_SOCKET;

                    if (connectionState != IEC104ConnectionState.DataTransferStarted)
                        return IEC104Constants.ERROR_NONE;

                    // Gửi STOPDT ACT
                    var stopdtFrame = IEC104Frame.CreateUFrame(UFrameFunction.STOPDT_ACT);
                    if (!SendFrameInternal(stopdtFrame))
                    {
                        LastError = "Failed to send STOPDT ACT";
                        return IEC104Constants.ERROR_SOCKET;
                    }

                    USentCount++;

                    // Đợi STOPDT CON với timeout T1
                    var response = WaitForUFrame(UFrameFunction.STOPDT_CON, TimeoutT1 * 1000);
                    if (response == null)
                    {
                        LastError = "STOPDT CON timeout";
                    }

                    connectionState = IEC104ConnectionState.Connected;
                    return IEC104Constants.ERROR_NONE;
                }
            }
            catch (Exception ex)
            {
                LastError = $"StopDataTransfer error: {ex.Message}";
                return IEC104Constants.ERROR_SOCKET;
            }
        }

        /// <summary>
        /// Gửi test frame
        /// </summary>
        /// <returns>Mã lỗi (0 = thành công)</returns>
        public int SendTestFrame()
        {
            try
            {
                lock (lockObject)
                {
                    if (!Connected)
                        return IEC104Constants.ERROR_SOCKET;

                    // Gửi TESTFR ACT
                    var testFrame = IEC104Frame.CreateUFrame(UFrameFunction.TESTFR_ACT);
                    if (!SendFrameInternal(testFrame))
                    {
                        LastError = "Failed to send TESTFR ACT";
                        return IEC104Constants.ERROR_SOCKET;
                    }

                    USentCount++;

                    // Đợi TESTFR CON với timeout T1
                    var response = WaitForUFrame(UFrameFunction.TESTFR_CON, TimeoutT1 * 1000);
                    if (response == null)
                    {
                        LastError = "TESTFR CON timeout";
                        return IEC104Constants.ERROR_CONNECTION_TIMEOUT;
                    }

                    return IEC104Constants.ERROR_NONE;
                }
            }
            catch (Exception ex)
            {
                LastError = $"SendTestFrame error: {ex.Message}";
                return IEC104Constants.ERROR_SOCKET;
            }
        }

        /// <summary>
        /// Ngắt kết nối
        /// </summary>
        /// <returns>Mã lỗi (0 = thành công)</returns>
        public int Disconnect()
        {
            try
            {
                lock (lockObject)
                {
                    // Dừng truyền dữ liệu trước
                    if (connectionState == IEC104ConnectionState.DataTransferStarted)
                    {
                        StopDataTransfer();
                    }

                    connectionState = IEC104ConnectionState.Disconnected;

                    // Dừng timers
                    StopTimers();

                    // Đóng socket
                    socket?.Close();

                    return IEC104Constants.ERROR_NONE;
                }
            }
            catch (Exception ex)
            {
                LastError = $"Disconnect error: {ex.Message}";
                return IEC104Constants.ERROR_SOCKET;
            }
        }
        #endregion

        #region DATA METHODS
        /// <summary>
        /// Gửi lệnh interrogation
        /// </summary>
        /// <param name="commonAddress">Common address</param>
        /// <param name="qualifier">Qualifier of interrogation (mặc định = 20 - station interrogation)</param>
        /// <returns>Mã lỗi (0 = thành công)</returns>
        public int SendInterrogation(ushort commonAddress, byte qualifier = IEC104Constants.QOI_STATION_INTERROGATION)
        {
            try
            {
                if (!IsDataTransferActive)
                {
                    LastError = "Data transfer not active";
                    return IEC104Constants.ERROR_SOCKET;
                }

                // Tạo ASDU cho lệnh interrogation
                var asdu = ASDU.CreateGeneralInterrogation(commonAddress, qualifier);

                return SendASDU(asdu);
            }
            catch (Exception ex)
            {
                LastError = $"SendInterrogation error: {ex.Message}";
                return IEC104Constants.ERROR_INVALID_ASDU;
            }
        }

        /// <summary>
        /// Gửi lệnh điều khiển
        /// </summary>
        /// <param name="commonAddress">Common address</param>
        /// <param name="ioa">Information object address</param>
        /// <param name="typeId">Type ID</param>
        /// <param name="value">Giá trị điều khiển</param>
        /// <param name="selectBeforeOperate">Có dùng select-before-operate không</param>
        /// <returns>Mã lỗi (0 = thành công)</returns>
        public int SendCommand(ushort commonAddress, uint ioa, byte typeId, object value, bool selectBeforeOperate = false)
        {
            try
            {
                if (!IsDataTransferActive)
                {
                    LastError = "Data transfer not active";
                    return IEC104Constants.ERROR_SOCKET;
                }

                if (!IEC104Constants.IsValidIOA(ioa))
                {
                    LastError = "Invalid IOA";
                    return IEC104Constants.ERROR_INVALID_PARAMETERS;
                }

                // Tạo ASDU cho lệnh điều khiển
                var asdu = new ASDU
                {
                    TypeID = typeId,
                    SequenceBit = false,
                    NumberOfElements = 1,
                    TestBit = false,
                    NegativeBit = false,
                    CauseOfTransmission = selectBeforeOperate ? IEC104Constants.COT_ACTIVATION : IEC104Constants.COT_ACTIVATION,
                    OriginatorAddress = 0,
                    CommonAddress = commonAddress
                };

                // Tạo information object
                var infoObject = new InformationObject
                {
                    ObjectAddress = ioa,
                    Value = value,
                    Quality = selectBeforeOperate ? IEC104Constants.SELECT_COMMAND : IEC104Constants.EXECUTE_COMMAND
                };

                asdu.AddInformationObject(infoObject);

                return SendASDU(asdu);
            }
            catch (Exception ex)
            {
                LastError = $"SendCommand error: {ex.Message}";
                return IEC104Constants.ERROR_INVALID_ASDU;
            }
        }

        // --- FIX: Thêm hàm DequeueAllASDUs ---
        // Lý do: Cung cấp một cách an toàn để lớp cao hơn (Adapter) lấy dữ liệu đã nhận.
        public List<ASDU> DequeueAllASDUs()
        {
            var list = new List<ASDU>();
            lock (asduQueue)
            {
                while (asduQueue.Count > 0)
                {
                    list.Add(asduQueue.Dequeue());
                }
            }
            return list;
        }

        #endregion

        #region FRAME PROCESSING METHODS
        /// <summary>
        /// Gửi ASDU
        /// </summary>
        /// <param name="asdu">ASDU cần gửi</param>
        /// <returns>Mã lỗi (0 = thành công)</returns>
        private int SendASDU(ASDU asdu)
        {
            try
            {
                var asduBytes = asdu.ToByteArray();
                var iframe = IEC104Frame.CreateIFrame(GetNextSendSequence(), receiveSequenceNumber, asduBytes);

                if (!SendFrameInternal(iframe))
                {
                    LastError = "Failed to send I-frame";
                    return IEC104Constants.ERROR_SOCKET;
                }

                ISentCount++;
                unacknowledgedISent++;

                return IEC104Constants.ERROR_NONE;
            }
            catch (Exception ex)
            {
                LastError = $"SendASADU error: {ex.Message}";
                return IEC104Constants.ERROR_INVALID_ASDU;
            }
        }

        /// <summary>
        /// Gửi frame (internal method)
        /// </summary>
        /// <param name="frame">Frame cần gửi</param>
        /// <returns>True nếu thành công</returns>
        private bool SendFrameInternal(IEC104Frame frame)
        {
            try
            {
                var frameBytes = frame.ToByteArray();
                var bytesSent = socket.Send(frameBytes);

                lastSentTime = DateTime.Now;
                return bytesSent == frameBytes.Length;
            }
            catch
            {
                Disconnect();
                return false;
            }
        }

        // --- FIX: Tạo vòng lặp xử lý nhận dữ liệu riêng biệt ---
        // Lý do: Tách biệt việc nhận dữ liệu khỏi các luồng gọi chính (như ReadTag),
        // đảm bảo dữ liệu được xử lý liên tục và không bị block.
        private void ProcessReceiveLoop()
        {
            while (connectionState != IEC104ConnectionState.Disconnected)
            {
                try
                {
                    if (socket.Available > 0)
                    {
                        var frame = ReceiveFrame();
                        if (frame != null && frame.IsValid)
                        {
                            ProcessReceivedFrame(frame);
                        }
                    }
                    else
                    {
                        Thread.Sleep(10); // Đợi một chút nếu không có dữ liệu
                    }
                }
                catch
                {
                    // Lỗi xảy ra, có thể do mất kết nối
                    Disconnect();
                    break;
                }
            }
        }


        /// <summary>
        /// Nhận một frame từ socket
        /// </summary>
        /// <returns>Frame nhận được hoặc null</returns>
        private IEC104Frame ReceiveFrame()
        {
            try
            {
                // Đọc header (2 bytes: start + length)
                if (!socket.ReceiveExact(receiveBuffer, 0, 2, 1000))
                    return null;

                // Kiểm tra start byte
                if (receiveBuffer[0] != IEC104Constants.START_BYTE)
                    return null;

                // Lấy độ dài APDU
                var apduLength = receiveBuffer[1];
                if (apduLength > IEC104Constants.MAX_APDU_LENGTH || apduLength < IEC104Constants.CONTROL_FIELD_LENGTH)
                    return null;

                // Đọc phần còn lại của frame
                if (!socket.ReceiveExact(receiveBuffer, 2, apduLength, 2000))
                    return null;

                // Tạo frame từ dữ liệu nhận được
                var frameData = new byte[apduLength + 2];
                Array.Copy(receiveBuffer, 0, frameData, 0, frameData.Length);

                lastReceivedTime = DateTime.Now;
                return new IEC104Frame(frameData);
            }
            catch
            {
                Disconnect();
                return null;
            }
        }

        /// <summary>
        /// Xử lý frame đã nhận
        /// </summary>
        /// <param name="frame">Frame cần xử lý</param>
        private void ProcessReceivedFrame(IEC104Frame frame)
        {
            try
            {
                switch (frame.Format)
                {
                    case FrameFormat.I_FORMAT:
                        ProcessIFrame(frame);
                        break;
                    case FrameFormat.S_FORMAT:
                        ProcessSFrame(frame);
                        break;
                    case FrameFormat.U_FORMAT:
                        ProcessUFrame(frame);
                        break;
                }
            }
            catch (Exception ex)
            {
                LastError = $"ProcessReceivedFrame error: {ex.Message}";
            }
        }

        /// <summary>
        /// Xử lý I-frame
        /// </summary>
        /// <param name="frame">I-frame</param>
        private void ProcessIFrame(IEC104Frame frame)
        {
            // Cập nhật sequence numbers
            receiveSequenceNumber = (ushort)((frame.SendSequenceNumber + 1) % (IEC104Constants.MAX_SEQUENCE_NUMBER + 1));
            unacknowledgedIReceived++;
            IReceivedCount++;

            // --- FIX: Parse ASDU và đưa vào asduQueue ---
            var asdu = new ASDU(frame.ASDUData);
            if (asdu.IsValid)
            {
                lock (asduQueue)
                {
                    asduQueue.Enqueue(asdu);
                }
            }
            else
            {
                // Xử lý lỗi ASDU không hợp lệ nếu cần
            }

            // Gửi S-frame ACK nếu cần
            if (unacknowledgedIReceived >= parameterW)
            {
                SendSFrame();
            }

            // Xử lý ACK cho các I-frame đã gửi
            var ackedFrames = frame.ReceiveSequenceNumber - sendSequenceNumber;
            if (ackedFrames > 0)
            {
                unacknowledgedISent = Math.Max(0, unacknowledgedISent - ackedFrames);
            }
        }

        /// <summary>
        /// Xử lý S-frame
        /// </summary>
        /// <param name="frame">S-frame</param>
        private void ProcessSFrame(IEC104Frame frame)
        {
            // Xử lý ACK cho các I-frame đã gửi
            var ackedFrames = frame.ReceiveSequenceNumber - sendSequenceNumber;
            if (ackedFrames > 0)
            {
                unacknowledgedISent = Math.Max(0, unacknowledgedISent - ackedFrames);
            }

            lock (U_S_FrameQueue)
            {
                U_S_FrameQueue.Enqueue(frame);
            }
        }

        /// <summary>
        /// Xử lý U-frame
        /// </summary>
        /// <param name="frame">U-frame</param>
        private void ProcessUFrame(IEC104Frame frame)
        {
            var function = frame.GetUFrameFunction();

            switch (function)
            {
                case UFrameFunction.STARTDT_ACT:
                    // Gửi STARTDT CON
                    SendUFrame(UFrameFunction.STARTDT_CON);
                    connectionState = IEC104ConnectionState.DataTransferStarted;
                    break;

                case UFrameFunction.STOPDT_ACT:
                    // Gửi STOPDT CON
                    SendUFrame(UFrameFunction.STOPDT_CON);
                    connectionState = IEC104ConnectionState.Connected;
                    break;

                case UFrameFunction.TESTFR_ACT:
                    // Gửi TESTFR CON
                    SendUFrame(UFrameFunction.TESTFR_CON);
                    break;
            }

            lock (U_S_FrameQueue)
            {
                U_S_FrameQueue.Enqueue(frame);
            }
        }

        /// <summary>
        /// Gửi S-frame
        /// </summary>
        private void SendSFrame()
        {
            try
            {
                var sframe = IEC104Frame.CreateSFrame(receiveSequenceNumber);
                if (SendFrameInternal(sframe))
                {
                    SSentCount++;
                    unacknowledgedIReceived = 0;
                }
            }
            catch (Exception ex)
            {
                LastError = $"SendSFrame error: {ex.Message}";
            }
        }

        /// <summary>
        /// Gửi U-frame
        /// </summary>
        /// <param name="function">Chức năng U-frame</param>
        private void SendUFrame(UFrameFunction function)
        {
            try
            {
                var uframe = IEC104Frame.CreateUFrame(function);
                if (SendFrameInternal(uframe))
                {
                    USentCount++;
                }
            }
            catch (Exception ex)
            {
                LastError = $"SendUFrame error: {ex.Message}";
            }
        }

        /// <summary>
        /// Đợi nhận U-frame với function cụ thể
        /// </summary>
        /// <param name="expectedFunction">Function mong đợi</param>
        /// <param name="timeoutMs">Timeout (ms)</param>
        /// <returns>Frame nhận được hoặc null</returns>
        private IEC104Frame WaitForUFrame(UFrameFunction expectedFunction, int timeoutMs)
        {
            var startTime = DateTime.Now;

            while ((DateTime.Now - startTime).TotalMilliseconds < timeoutMs)
            {
                lock (U_S_FrameQueue)
                {
                    for (int i = 0; i < U_S_FrameQueue.Count; i++)
                    {
                        var frame = U_S_FrameQueue.Dequeue();
                        if (frame.Format == FrameFormat.U_FORMAT && frame.GetUFrameFunction() == expectedFunction)
                        {
                            return frame;
                        }
                        // Đưa frame không khớp trở lại queue
                        U_S_FrameQueue.Enqueue(frame);
                    }
                }
                Thread.Sleep(20);
            }

            return null;
        }
        #endregion

        #region SEQUENCE NUMBER METHODS
        /// <summary>
        /// Lấy sequence number tiếp theo để gửi
        /// </summary>
        /// <returns>Send sequence number</returns>
        private ushort GetNextSendSequence()
        {
            var current = sendSequenceNumber;
            sendSequenceNumber = (ushort)((sendSequenceNumber + 1) % (IEC104Constants.MAX_SEQUENCE_NUMBER + 1));
            return current;
        }

        /// <summary>
        /// Reset sequence numbers
        /// </summary>
        private void ResetSequenceNumbers()
        {
            sendSequenceNumber = 0;
            receiveSequenceNumber = 0;
            unacknowledgedISent = 0;
            unacknowledgedIReceived = 0;
        }

        /// <summary>
        /// Reset counters
        /// </summary>
        private void ResetCounters()
        {
            ISentCount = 0;
            IReceivedCount = 0;
            SSentCount = 0;
            USentCount = 0;
        }
        #endregion

        #region TIMER METHODS
        /// <summary>
        /// Khởi động timers
        /// </summary>
        private void StartTimers()
        {
            StopTimers(); // Dừng timer cũ trước khi bắt đầu

            // T1 timer - Send/Test timeout
            t1Timer = new Timer(T1TimerCallback, null, Timeout.Infinite, Timeout.Infinite);

            // T2 timer - ACK timeout
            t2Timer = new Timer(T2TimerCallback, null, TimeoutT2 * 1000, TimeoutT2 * 1000);

            // T3 timer - Test frame timeout
            t3Timer = new Timer(T3TimerCallback, null, TimeoutT3 * 1000, TimeoutT3 * 1000);
        }

        /// <summary>
        /// Dừng timers
        /// </summary>
        private void StopTimers()
        {
            t1Timer?.Dispose();
            t1Timer = null;

            t2Timer?.Dispose();
            t2Timer = null;

            t3Timer?.Dispose();
            t3Timer = null;
        }

        /// <summary>
        /// T1 timer callback
        /// </summary>
        private void T1TimerCallback(object state)
        {
            // T1 timeout - connection lost
            LastError = "T1 timeout";
            Disconnect();
        }

        /// <summary>
        /// T2 timer callback
        /// </summary>
        private void T2TimerCallback(object state)
        {
            try
            {
                lock (lockObject)
                {
                    // Gửi S-frame nếu có I-frame chưa ACK và không có traffic gửi đi
                    if (unacknowledgedIReceived > 0 && (DateTime.Now - lastSentTime).TotalSeconds > TimeoutT2)
                    {
                        SendSFrame();
                    }
                }
            }
            catch (Exception ex)
            {
                LastError = $"T2 timer error: {ex.Message}";
            }
        }

        /// <summary>
        /// T3 timer callback
        /// </summary>
        private void T3TimerCallback(object state)
        {
            try
            {
                lock (lockObject)
                {
                    if (connectionState != IEC104ConnectionState.DataTransferStarted) return;

                    // Kiểm tra xem có activity gần đây không
                    var timeSinceLastActivity = DateTime.Now - lastReceivedTime;

                    if (timeSinceLastActivity.TotalSeconds > TimeoutT3)
                    {
                        // Gửi test frame
                        SendTestFrame();
                    }
                }
            }
            catch (Exception ex)
            {
                LastError = $"T3 timer error: {ex.Message}";
            }
        }
        #endregion

        #region STATUS METHODS
        /// <summary>
        /// Lấy thông tin trạng thái
        /// </summary>
        /// <returns>Thông tin trạng thái</returns>
        public string GetStatusInfo()
        {
            lock (lockObject)
            {
                return $"IEC104Client [{Name}] | " +
                       $"State: {connectionState} | " +
                       $"TCP: {(Connected ? "Connected" : "Disconnected")} | " +
                       $"I-Sent: {ISentCount} | I-Recv: {IReceivedCount} | " +
                       $"S-Sent: {SSentCount} | U-Sent: {USentCount} | " +
                       $"UnAck-Sent: {unacknowledgedISent} | UnAck-Recv: {unacknowledgedIReceived}";
            }
        }

        /// <summary>
        /// Lấy thống kê chi tiết
        /// </summary>
        /// <returns>Thống kê client</returns>
        public IEC104ClientStatistics GetStatistics()
        {
            lock (lockObject)
            {
                return new IEC104ClientStatistics
                {
                    Name = Name,
                    IpAddress = IpAddress,
                    Port = Port,
                    ConnectionState = connectionState,
                    IsConnected = Connected,
                    IsDataTransferActive = IsDataTransferActive,
                    CommonAddress = CommonAddress,
                    LastError = LastError,

                    // Sequence numbers
                    SendSequenceNumber = sendSequenceNumber,
                    ReceiveSequenceNumber = receiveSequenceNumber,
                    UnacknowledgedISent = unacknowledgedISent,
                    UnacknowledgedIReceived = unacknowledgedIReceived,

                    // Counters
                    ISentCount = ISentCount,
                    IReceivedCount = IReceivedCount,
                    SSentCount = SSentCount,
                    USentCount = USentCount,

                    // Parameters
                    ParameterK = parameterK,
                    ParameterW = parameterW,
                    TimeoutT0 = timeoutT0,
                    TimeoutT1 = timeoutT1,
                    TimeoutT2 = timeoutT2,
                    TimeoutT3 = timeoutT3,

                    // Times
                    LastSentTime = lastSentTime,
                    LastReceivedTime = lastReceivedTime,

                    // Socket stats
                    SocketStatistics = socket?.GetStatistics()
                };
            }
        }
        #endregion

        #region DISPOSE
        /// <summary>
        /// Dispose client
        /// </summary>
        public void Dispose()
        {
            if (!isDisposed)
            {
                isDisposed = true;
                Disconnect();
                socket?.Dispose();
            }
        }
        #endregion
    }

    #region SUPPORTING ENUMS
    /// <summary>
    /// Trạng thái kết nối IEC104
    /// </summary>
    public enum IEC104ConnectionState
    {
        Disconnected,
        Connected,
        DataTransferStarted
    }

    /// <summary>
    /// Chức năng U-frame
    /// </summary>
    public enum UFrameFunction : byte
    {
        STARTDT_ACT = IEC104Constants.STARTDT_ACT,
        STARTDT_CON = IEC104Constants.STARTDT_CON,
        STOPDT_ACT = IEC104Constants.STOPDT_ACT,
        STOPDT_CON = IEC104Constants.STOPDT_CON,
        TESTFR_ACT = IEC104Constants.TESTFR_ACT,
        TESTFR_CON = IEC104Constants.TESTFR_CON
    }

    /// <summary>
    /// Format frame
    /// </summary>
    public enum FrameFormat
    {
        I_FORMAT = 0,    // Information frame
        S_FORMAT = 1,    // Supervisory frame  
        U_FORMAT = 3     // Unnumbered frame
    }
    #endregion

    #region SUPPORTING CLASSES
    /// <summary>
    /// Thống kê IEC104 client
    /// </summary>
    public class IEC104ClientStatistics
    {
        public string Name { get; set; }
        public string IpAddress { get; set; }
        public int Port { get; set; }
        public IEC104ConnectionState ConnectionState { get; set; }
        public bool IsConnected { get; set; }
        public bool IsDataTransferActive { get; set; }
        public ushort CommonAddress { get; set; }
        public string LastError { get; set; }

        // Sequence numbers
        public ushort SendSequenceNumber { get; set; }
        public ushort ReceiveSequenceNumber { get; set; }
        public int UnacknowledgedISent { get; set; }
        public int UnacknowledgedIReceived { get; set; }

        // Counters
        public long ISentCount { get; set; }
        public long IReceivedCount { get; set; }
        public long SSentCount { get; set; }
        public long USentCount { get; set; }

        // Parameters
        public ushort ParameterK { get; set; }
        public ushort ParameterW { get; set; }
        public ushort TimeoutT0 { get; set; }
        public ushort TimeoutT1 { get; set; }
        public ushort TimeoutT2 { get; set; }
        public ushort TimeoutT3 { get; set; }

        // Times
        public DateTime LastSentTime { get; set; }
        public DateTime LastReceivedTime { get; set; }

        // Socket statistics
        public SocketStatistics SocketStatistics { get; set; }

        public override string ToString()
        {
            return $"IEC104Client [{Name}] {IpAddress}:{Port} | " +
                   $"State: {ConnectionState} | " +
                   $"I-Frames: {ISentCount}/{IReceivedCount} | " +
                   $"UnAck: {UnacknowledgedISent}/{UnacknowledgedIReceived} | " +
                   $"Params: K={ParameterK}, W={ParameterW} | " +
                   $"Timeouts: T1={TimeoutT1}s, T2={TimeoutT2}s, T3={TimeoutT3}s";
        }
    }
    #endregion
}