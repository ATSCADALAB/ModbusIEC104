using System;
using System.IO;

namespace ModbusIEC104
{
    /// <summary>
    /// IEC 60870-5-104 Frame structure
    /// </summary>
    public class IEC104Frame
    {
        #region PROPERTIES
        /// <summary>Start byte (always 0x68)</summary>
        public byte StartByte { get; set; } = IEC104Constants.START_BYTE;

        /// <summary>APDU Length (excluding start byte and length byte)</summary>
        public byte APDULength { get; set; }

        /// <summary>Frame format (I, S, or U)</summary>
        public FrameFormat Format { get; private set; }

        /// <summary>Send sequence number (for I-frames)</summary>
        public ushort SendSequenceNumber { get; set; }

        /// <summary>Receive sequence number (for I and S frames)</summary>
        public ushort ReceiveSequenceNumber { get; set; }

        /// <summary>U-frame function (for U-frames)</summary>
        public UFrameFunction UFunction { get; private set; }

        /// <summary>ASDU data (for I-frames)</summary>
        public byte[] ASDUData { get; set; }

        /// <summary>Raw frame data</summary>
        public byte[] RawData { get; private set; }

        /// <summary>Kiểm tra frame có hợp lệ không</summary>
        public bool IsValid { get; private set; } = true;

        /// <summary>Lỗi parse nếu có</summary>
        public string ParseError { get; private set; }

        /// <summary>Độ dài toàn bộ frame (bao gồm header)</summary>
        public int TotalLength => APDULength + 2; // +2 for start byte and length byte

        /// <summary>Control field (4 bytes)</summary>
        public byte[] ControlField { get; private set; } = new byte[4];
        #endregion

        #region CONSTRUCTORS
        /// <summary>
        /// Constructor mặc định
        /// </summary>
        public IEC104Frame()
        {
            IsValid = true;
        }

        /// <summary>
        /// Constructor từ byte array
        /// </summary>
        /// <param name="data">Frame data</param>
        public IEC104Frame(byte[] data) : this()
        {
            ParseFrame(data);
        }

        /// <summary>
        /// Constructor cho I-frame
        /// </summary>
        /// <param name="sendSeq">Send sequence number</param>
        /// <param name="recvSeq">Receive sequence number</param>
        /// <param name="asduData">ASDU data</param>
        private IEC104Frame(ushort sendSeq, ushort recvSeq, byte[] asduData) : this()
        {
            Format = FrameFormat.I_FORMAT;
            SendSequenceNumber = sendSeq;
            ReceiveSequenceNumber = recvSeq;
            ASDUData = asduData ?? new byte[0];
            APDULength = (byte)(IEC104Constants.CONTROL_FIELD_LENGTH + ASDUData.Length);
            BuildControlField();
        }

        /// <summary>
        /// Constructor cho S-frame
        /// </summary>
        /// <param name="recvSeq">Receive sequence number</param>
        private IEC104Frame(ushort recvSeq) : this()
        {
            Format = FrameFormat.S_FORMAT;
            ReceiveSequenceNumber = recvSeq;
            APDULength = IEC104Constants.CONTROL_FIELD_LENGTH;
            BuildControlField();
        }

        /// <summary>
        /// Constructor cho U-frame
        /// </summary>
        /// <param name="function">U-frame function</param>
        private IEC104Frame(UFrameFunction function) : this()
        {
            Format = FrameFormat.U_FORMAT;
            UFunction = function;
            APDULength = IEC104Constants.CONTROL_FIELD_LENGTH;
            BuildControlField();
        }
        #endregion

        #region STATIC FACTORY METHODS
        /// <summary>
        /// Tạo I-format frame (Information frame)
        /// </summary>
        /// <param name="sendSeq">Send sequence number</param>
        /// <param name="recvSeq">Receive sequence number</param>
        /// <param name="asduData">ASDU data</param>
        /// <returns>I-format frame</returns>
        public static IEC104Frame CreateIFrame(ushort sendSeq, ushort recvSeq, byte[] asduData)
        {
            if (asduData != null && asduData.Length > IEC104Constants.MAX_APDU_LENGTH - IEC104Constants.CONTROL_FIELD_LENGTH)
            {
                throw new ArgumentException($"ASDU data too large. Maximum allowed: {IEC104Constants.MAX_APDU_LENGTH - IEC104Constants.CONTROL_FIELD_LENGTH} bytes");
            }

            if (sendSeq > IEC104Constants.MAX_SEQUENCE_NUMBER || recvSeq > IEC104Constants.MAX_SEQUENCE_NUMBER)
            {
                throw new ArgumentException("Sequence numbers must not exceed maximum value");
            }

            return new IEC104Frame(sendSeq, recvSeq, asduData);
        }

        /// <summary>
        /// Tạo S-format frame (Supervisory frame)
        /// </summary>
        /// <param name="recvSeq">Receive sequence number</param>
        /// <returns>S-format frame</returns>
        public static IEC104Frame CreateSFrame(ushort recvSeq)
        {
            if (recvSeq > IEC104Constants.MAX_SEQUENCE_NUMBER)
            {
                throw new ArgumentException("Sequence number must not exceed maximum value");
            }

            return new IEC104Frame(recvSeq);
        }

        /// <summary>
        /// Tạo U-format frame (Unnumbered frame)
        /// </summary>
        /// <param name="function">U-frame function</param>
        /// <returns>U-format frame</returns>
        public static IEC104Frame CreateUFrame(UFrameFunction function)
        {
            if (!Enum.IsDefined(typeof(UFrameFunction), function))
            {
                throw new ArgumentException("Invalid U-frame function");
            }

            return new IEC104Frame(function);
        }
        #endregion

        #region PARSING METHODS
        /// <summary>
        /// Parse frame từ byte array
        /// </summary>
        /// <param name="data">Frame data</param>
        private void ParseFrame(byte[] data)
        {
            try
            {
                if (data == null || data.Length < IEC104Constants.MIN_FRAME_LENGTH)
                {
                    IsValid = false;
                    ParseError = $"Frame too short. Minimum length: {IEC104Constants.MIN_FRAME_LENGTH} bytes";
                    return;
                }

                RawData = new byte[data.Length];
                Array.Copy(data, RawData, data.Length);

                using (var stream = new MemoryStream(data))
                using (var reader = new BinaryReader(stream))
                {
                    // Parse header
                    ParseHeader(reader);

                    if (!IsValid) return;

                    // Parse control field
                    ParseControlField(reader);

                    if (!IsValid) return;

                    // Parse ASDU data (for I-frames)
                    if (Format == FrameFormat.I_FORMAT)
                    {
                        ParseASDUData(reader);
                    }
                }
            }
            catch (Exception ex)
            {
                IsValid = false;
                ParseError = $"Frame parsing error: {ex.Message}";
            }
        }

        /// <summary>
        /// Parse frame header (start byte + length)
        /// </summary>
        /// <param name="reader">Binary reader</param>
        private void ParseHeader(BinaryReader reader)
        {
            // Start byte
            StartByte = reader.ReadByte();
            if (StartByte != IEC104Constants.START_BYTE)
            {
                IsValid = false;
                ParseError = $"Invalid start byte: 0x{StartByte:X2}, expected: 0x{IEC104Constants.START_BYTE:X2}";
                return;
            }

            // APDU length
            APDULength = reader.ReadByte();
            if (APDULength < IEC104Constants.CONTROL_FIELD_LENGTH)
            {
                IsValid = false;
                ParseError = $"APDU length too small: {APDULength}, minimum: {IEC104Constants.CONTROL_FIELD_LENGTH}";
                return;
            }

            // Verify total frame length
            if (reader.BaseStream.Length != APDULength + 2)
            {
                IsValid = false;
                ParseError = $"Frame length mismatch. Expected: {APDULength + 2}, actual: {reader.BaseStream.Length}";
                return;
            }
        }

        /// <summary>
        /// Parse control field (4 bytes)
        /// </summary>
        /// <param name="reader">Binary reader</param>
        private void ParseControlField(BinaryReader reader)
        {
            if (reader.BaseStream.Position + 4 > reader.BaseStream.Length)
            {
                IsValid = false;
                ParseError = "Not enough data for control field";
                return;
            }

            ControlField = reader.ReadBytes(4);

            // Determine frame format from first control byte
            var controlByte1 = ControlField[0];

            if ((controlByte1 & IEC104Constants.I_FORMAT_MASK) == 0)
            {
                // I-format frame
                Format = FrameFormat.I_FORMAT;
                ParseIFrameControlField();
            }
            else if ((controlByte1 & IEC104Constants.S_FORMAT_MASK) == IEC104Constants.S_FORMAT_VALUE)
            {
                // S-format frame
                Format = FrameFormat.S_FORMAT;
                ParseSFrameControlField();
            }
            else if ((controlByte1 & IEC104Constants.U_FORMAT_MASK) == IEC104Constants.U_FORMAT_VALUE)
            {
                // U-format frame
                Format = FrameFormat.U_FORMAT;
                ParseUFrameControlField();
            }
            else
            {
                IsValid = false;
                ParseError = $"Unknown frame format: 0x{controlByte1:X2}";
            }
        }

        /// <summary>
        /// Parse I-frame control field
        /// </summary>
        private void ParseIFrameControlField()
        {
            // Send sequence number (bits 1-15 of bytes 0-1)
            SendSequenceNumber = (ushort)(((ControlField[1] << 8) | ControlField[0]) >> 1);

            // Receive sequence number (bits 1-15 of bytes 2-3)
            ReceiveSequenceNumber = (ushort)(((ControlField[3] << 8) | ControlField[2]) >> 1);

            // Validate sequence numbers
            if (SendSequenceNumber > IEC104Constants.MAX_SEQUENCE_NUMBER)
            {
                IsValid = false;
                ParseError = $"Invalid send sequence number: {SendSequenceNumber}";
            }

            if (ReceiveSequenceNumber > IEC104Constants.MAX_SEQUENCE_NUMBER)
            {
                IsValid = false;
                ParseError = $"Invalid receive sequence number: {ReceiveSequenceNumber}";
            }
        }

        /// <summary>
        /// Parse S-frame control field
        /// </summary>
        private void ParseSFrameControlField()
        {
            // Bytes 0-1 should be 0x01, 0x00
            if (ControlField[0] != 0x01 || ControlField[1] != 0x00)
            {
                IsValid = false;
                ParseError = "Invalid S-frame control field format";
                return;
            }

            // Receive sequence number (bits 1-15 of bytes 2-3)
            ReceiveSequenceNumber = (ushort)(((ControlField[3] << 8) | ControlField[2]) >> 1);

            if (ReceiveSequenceNumber > IEC104Constants.MAX_SEQUENCE_NUMBER)
            {
                IsValid = false;
                ParseError = $"Invalid receive sequence number: {ReceiveSequenceNumber}";
            }
        }

        /// <summary>
        /// Parse U-frame control field
        /// </summary>
        private void ParseUFrameControlField()
        {
            var functionByte = ControlField[0];

            // Determine U-frame function
            switch (functionByte)
            {
                case IEC104Constants.STARTDT_ACT:
                    UFunction = UFrameFunction.STARTDT_ACT;
                    break;
                case IEC104Constants.STARTDT_CON:
                    UFunction = UFrameFunction.STARTDT_CON;
                    break;
                case IEC104Constants.STOPDT_ACT:
                    UFunction = UFrameFunction.STOPDT_ACT;
                    break;
                case IEC104Constants.STOPDT_CON:
                    UFunction = UFrameFunction.STOPDT_CON;
                    break;
                case IEC104Constants.TESTFR_ACT:
                    UFunction = UFrameFunction.TESTFR_ACT;
                    break;
                case IEC104Constants.TESTFR_CON:
                    UFunction = UFrameFunction.TESTFR_CON;
                    break;
                default:
                    IsValid = false;
                    ParseError = $"Invalid U-frame function: 0x{functionByte:X2}";
                    return;
            }

            // Bytes 1-3 should be 0x00
            if (ControlField[1] != 0x00 || ControlField[2] != 0x00 || ControlField[3] != 0x00)
            {
                IsValid = false;
                ParseError = "Invalid U-frame control field format";
            }
        }

        /// <summary>
        /// Parse ASDU data (for I-frames)
        /// </summary>
        /// <param name="reader">Binary reader</param>
        private void ParseASDUData(BinaryReader reader)
        {
            var asduLength = APDULength - IEC104Constants.CONTROL_FIELD_LENGTH;
            if (asduLength > 0)
            {
                if (reader.BaseStream.Position + asduLength > reader.BaseStream.Length)
                {
                    IsValid = false;
                    ParseError = "Not enough data for ASDU";
                    return;
                }

                ASDUData = reader.ReadBytes(asduLength);
            }
            else
            {
                ASDUData = new byte[0];
            }
        }
        #endregion

        #region CONTROL FIELD BUILDING
        /// <summary>
        /// Build control field từ properties
        /// </summary>
        private void BuildControlField()
        {
            ControlField = new byte[4];

            switch (Format)
            {
                case FrameFormat.I_FORMAT:
                    BuildIFrameControlField();
                    break;
                case FrameFormat.S_FORMAT:
                    BuildSFrameControlField();
                    break;
                case FrameFormat.U_FORMAT:
                    BuildUFrameControlField();
                    break;
            }
        }

        /// <summary>
        /// Build I-frame control field
        /// </summary>
        private void BuildIFrameControlField()
        {
            // Send sequence number (bits 1-15 of bytes 0-1)
            var sendSeqShifted = (ushort)(SendSequenceNumber << 1);
            ControlField[0] = (byte)(sendSeqShifted & 0xFF);
            ControlField[1] = (byte)((sendSeqShifted >> 8) & 0xFF);

            // Receive sequence number (bits 1-15 of bytes 2-3)
            var recvSeqShifted = (ushort)(ReceiveSequenceNumber << 1);
            ControlField[2] = (byte)(recvSeqShifted & 0xFF);
            ControlField[3] = (byte)((recvSeqShifted >> 8) & 0xFF);
        }

        /// <summary>
        /// Build S-frame control field
        /// </summary>
        private void BuildSFrameControlField()
        {
            // Bytes 0-1: S-frame identifier
            ControlField[0] = 0x01;
            ControlField[1] = 0x00;

            // Receive sequence number (bits 1-15 of bytes 2-3)
            var recvSeqShifted = (ushort)(ReceiveSequenceNumber << 1);
            ControlField[2] = (byte)(recvSeqShifted & 0xFF);
            ControlField[3] = (byte)((recvSeqShifted >> 8) & 0xFF);
        }

        /// <summary>
        /// Build U-frame control field
        /// </summary>
        private void BuildUFrameControlField()
        {
            // Function byte
            ControlField[0] = (byte)UFunction;

            // Bytes 1-3: zero
            ControlField[1] = 0x00;
            ControlField[2] = 0x00;
            ControlField[3] = 0x00;
        }
        #endregion

        #region SERIALIZATION METHODS
        /// <summary>
        /// Chuyển frame thành byte array
        /// </summary>
        /// <returns>Byte array</returns>
        public byte[] ToByteArray()
        {
            try
            {
                using (var stream = new MemoryStream())
                using (var writer = new BinaryWriter(stream))
                {
                    // Write header
                    writer.Write(StartByte);
                    writer.Write(APDULength);

                    // Write control field
                    writer.Write(ControlField);

                    // Write ASDU data (for I-frames)
                    if (Format == FrameFormat.I_FORMAT && ASDUData != null)
                    {
                        writer.Write(ASDUData);
                    }

                    return stream.ToArray();
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error serializing frame: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Validate frame before serialization
        /// </summary>
        /// <returns>True nếu hợp lệ</returns>
        public bool Validate()
        {
            if (StartByte != IEC104Constants.START_BYTE)
                return false;

            if (APDULength > IEC104Constants.MAX_APDU_LENGTH)
                return false;

            if (APDULength < IEC104Constants.CONTROL_FIELD_LENGTH)
                return false;

            if (ControlField == null || ControlField.Length != 4)
                return false;

            switch (Format)
            {
                case FrameFormat.I_FORMAT:
                    if (SendSequenceNumber > IEC104Constants.MAX_SEQUENCE_NUMBER)
                        return false;
                    if (ReceiveSequenceNumber > IEC104Constants.MAX_SEQUENCE_NUMBER)
                        return false;
                    if (ASDUData != null && ASDUData.Length + IEC104Constants.CONTROL_FIELD_LENGTH != APDULength)
                        return false;
                    break;

                case FrameFormat.S_FORMAT:
                    if (ReceiveSequenceNumber > IEC104Constants.MAX_SEQUENCE_NUMBER)
                        return false;
                    if (APDULength != IEC104Constants.CONTROL_FIELD_LENGTH)
                        return false;
                    break;

                case FrameFormat.U_FORMAT:
                    if (!Enum.IsDefined(typeof(UFrameFunction), UFunction))
                        return false;
                    if (APDULength != IEC104Constants.CONTROL_FIELD_LENGTH)
                        return false;
                    break;

                default:
                    return false;
            }

            return true;
        }
        #endregion

        #region UTILITY METHODS
        /// <summary>
        /// Lấy U-frame function
        /// </summary>
        /// <returns>U-frame function</returns>
        public UFrameFunction GetUFrameFunction()
        {
            if (Format != FrameFormat.U_FORMAT)
                throw new InvalidOperationException("Not a U-format frame");

            return UFunction;
        }

        /// <summary>
        /// Kiểm tra có phải STARTDT ACT không
        /// </summary>
        /// <returns>True nếu là STARTDT ACT</returns>
        public bool IsStartDtAct()
        {
            return Format == FrameFormat.U_FORMAT && UFunction == UFrameFunction.STARTDT_ACT;
        }

        /// <summary>
        /// Kiểm tra có phải STARTDT CON không
        /// </summary>
        /// <returns>True nếu là STARTDT CON</returns>
        public bool IsStartDtCon()
        {
            return Format == FrameFormat.U_FORMAT && UFunction == UFrameFunction.STARTDT_CON;
        }

        /// <summary>
        /// Kiểm tra có phải STOPDT ACT không
        /// </summary>
        /// <returns>True nếu là STOPDT ACT</returns>
        public bool IsStopDtAct()
        {
            return Format == FrameFormat.U_FORMAT && UFunction == UFrameFunction.STOPDT_ACT;
        }

        /// <summary>
        /// Kiểm tra có phải STOPDT CON không
        /// </summary>
        /// <returns>True nếu là STOPDT CON</returns>
        public bool IsStopDtCon()
        {
            return Format == FrameFormat.U_FORMAT && UFunction == UFrameFunction.STOPDT_CON;
        }

        /// <summary>
        /// Kiểm tra có phải TESTFR ACT không
        /// </summary>
        /// <returns>True nếu là TESTFR ACT</returns>
        public bool IsTestFrAct()
        {
            return Format == FrameFormat.U_FORMAT && UFunction == UFrameFunction.TESTFR_ACT;
        }

        /// <summary>
        /// Kiểm tra có phải TESTFR CON không
        /// </summary>
        /// <returns>True nếu là TESTFR CON</returns>
        public bool IsTestFrCon()
        {
            return Format == FrameFormat.U_FORMAT && UFunction == UFrameFunction.TESTFR_CON;
        }

        /// <summary>
        /// Lấy ASDU từ I-frame
        /// </summary>
        /// <returns>ASDU object hoặc null</returns>
        public ASDU GetASADU()
        {
            if (Format != FrameFormat.I_FORMAT || ASDUData == null || ASDUData.Length == 0)
                return null;

            try
            {
                return new ASDU(ASDUData);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Lấy thông tin chi tiết frame
        /// </summary>
        /// <returns>Thông tin chi tiết</returns>
        public string GetDetailedInfo()
        {
            var info = $"IEC104 Frame Information:\n";
            info += $"  Start Byte: 0x{StartByte:X2}\n";
            info += $"  APDU Length: {APDULength}\n";
            info += $"  Total Length: {TotalLength}\n";
            info += $"  Format: {Format}\n";
            info += $"  Is Valid: {IsValid}\n";

            if (!string.IsNullOrEmpty(ParseError))
            {
                info += $"  Parse Error: {ParseError}\n";
            }

            info += $"  Control Field: {BitConverter.ToString(ControlField)}\n";

            switch (Format)
            {
                case FrameFormat.I_FORMAT:
                    info += $"  Send Sequence: {SendSequenceNumber}\n";
                    info += $"  Receive Sequence: {ReceiveSequenceNumber}\n";
                    info += $"  ASDU Length: {ASDUData?.Length ?? 0} bytes\n";
                    break;

                case FrameFormat.S_FORMAT:
                    info += $"  Receive Sequence: {ReceiveSequenceNumber}\n";
                    break;

                case FrameFormat.U_FORMAT:
                    info += $"  Function: {UFunction} (0x{(byte)UFunction:X2})\n";
                    break;
            }

            if (RawData != null)
            {
                info += $"  Raw Data: {BitConverter.ToString(RawData)}\n";
            }

            return info;
        }

        /// <summary>
        /// Clone frame
        /// </summary>
        /// <returns>Bản copy của frame</returns>
        public IEC104Frame Clone()
        {
            var clone = new IEC104Frame
            {
                StartByte = this.StartByte,
                APDULength = this.APDULength,
                Format = this.Format,
                SendSequenceNumber = this.SendSequenceNumber,
                ReceiveSequenceNumber = this.ReceiveSequenceNumber,
                UFunction = this.UFunction,
                IsValid = this.IsValid,
                ParseError = this.ParseError
            };

            if (this.ControlField != null)
            {
                clone.ControlField = new byte[this.ControlField.Length];
                Array.Copy(this.ControlField, clone.ControlField, this.ControlField.Length);
            }

            if (this.ASDUData != null)
            {
                clone.ASDUData = new byte[this.ASDUData.Length];
                Array.Copy(this.ASDUData, clone.ASDUData, this.ASDUData.Length);
            }

            if (this.RawData != null)
            {
                clone.RawData = new byte[this.RawData.Length];
                Array.Copy(this.RawData, clone.RawData, this.RawData.Length);
            }

            return clone;
        }
        #endregion

        #region STATIC HELPER METHODS
        /// <summary>
        /// Kiểm tra byte array có phải là frame hợp lệ không
        /// </summary>
        /// <param name="data">Data để kiểm tra</param>
        /// <returns>True nếu có thể là frame hợp lệ</returns>
        public static bool IsValidFrameData(byte[] data)
        {
            if (data == null || data.Length < IEC104Constants.MIN_FRAME_LENGTH)
                return false;

            // Kiểm tra start byte
            if (data[0] != IEC104Constants.START_BYTE)
                return false;

            // Kiểm tra length
            var apduLength = data[1];
            if (apduLength > IEC104Constants.MAX_APDU_LENGTH)
                return false;

            if (apduLength < IEC104Constants.CONTROL_FIELD_LENGTH)
                return false;

            // Kiểm tra total length
            if (data.Length != apduLength + 2)
                return false;

            return true;
        }

        /// <summary>
        /// Lấy frame format từ control byte
        /// </summary>
        /// <param name="controlByte">Control byte đầu tiên</param>
        /// <returns>Frame format</returns>
        public static FrameFormat GetFrameFormat(byte controlByte)
        {
            if ((controlByte & IEC104Constants.I_FORMAT_MASK) == 0)
                return FrameFormat.I_FORMAT;
            else if ((controlByte & IEC104Constants.S_FORMAT_MASK) == IEC104Constants.S_FORMAT_VALUE)
                return FrameFormat.S_FORMAT;
            else if ((controlByte & IEC104Constants.U_FORMAT_MASK) == IEC104Constants.U_FORMAT_VALUE)
                return FrameFormat.U_FORMAT;
            else
                throw new ArgumentException($"Unknown frame format: 0x{controlByte:X2}");
        }

        /// <summary>
        /// Tạo response frame cho U-frame request
        /// </summary>
        /// <param name="requestFrame">Request frame</param>
        /// <returns>Response frame hoặc null</returns>
        public static IEC104Frame CreateResponseFrame(IEC104Frame requestFrame)
        {
            if (requestFrame == null || requestFrame.Format != FrameFormat.U_FORMAT)
                return null;

            switch (requestFrame.UFunction)
            {
                case UFrameFunction.STARTDT_ACT:
                    return CreateUFrame(UFrameFunction.STARTDT_CON);

                case UFrameFunction.STOPDT_ACT:
                    return CreateUFrame(UFrameFunction.STOPDT_CON);

                case UFrameFunction.TESTFR_ACT:
                    return CreateUFrame(UFrameFunction.TESTFR_CON);

                default:
                    return null; // CON frames don't need responses
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
            switch (Format)
            {
                case FrameFormat.I_FORMAT:
                    return $"I-Frame[SendSeq={SendSequenceNumber}, RecvSeq={ReceiveSequenceNumber}, ASDU={ASDUData?.Length ?? 0}bytes]";

                case FrameFormat.S_FORMAT:
                    return $"S-Frame[RecvSeq={ReceiveSequenceNumber}]";

                case FrameFormat.U_FORMAT:
                    return $"U-Frame[{UFunction}]";

                default:
                    return $"Unknown-Frame[Format={Format}]";
            }
        }

        /// <summary>
        /// Equals method
        /// </summary>
        /// <param name="obj">Object to compare</param>
        /// <returns>True if equal</returns>
        public override bool Equals(object obj)
        {
            if (obj is IEC104Frame other)
            {
                if (Format != other.Format)
                    return false;

                switch (Format)
                {
                    case FrameFormat.I_FORMAT:
                        return SendSequenceNumber == other.SendSequenceNumber &&
                               ReceiveSequenceNumber == other.ReceiveSequenceNumber &&
                               CompareByteArrays(ASDUData, other.ASDUData);

                    case FrameFormat.S_FORMAT:
                        return ReceiveSequenceNumber == other.ReceiveSequenceNumber;

                    case FrameFormat.U_FORMAT:
                        return UFunction == other.UFunction;

                    default:
                        return false;
                }
            }
            return false;
        }

        /// <summary>
        /// Get hash code
        /// </summary>
        /// <returns>Hash code</returns>
        public override int GetHashCode()
        {
            switch (Format)
            {
                case FrameFormat.I_FORMAT:
                    return HashCode.Combine(Format, SendSequenceNumber, ReceiveSequenceNumber);

                case FrameFormat.S_FORMAT:
                    return HashCode.Combine(Format, ReceiveSequenceNumber);

                case FrameFormat.U_FORMAT:
                    return HashCode.Combine(Format, UFunction);

                default:
                    return HashCode.Combine(Format);
            }
        }

        /// <summary>
        /// Compare byte arrays
        /// </summary>
        /// <param name="array1">First array</param>
        /// <param name="array2">Second array</param>
        /// <returns>True if equal</returns>
        private bool CompareByteArrays(byte[] array1, byte[] array2)
        {
            if (array1 == null && array2 == null)
                return true;

            if (array1 == null || array2 == null)
                return false;

            if (array1.Length != array2.Length)
                return false;

            for (int i = 0; i < array1.Length; i++)
            {
                if (array1[i] != array2[i])
                    return false;
            }

            return true;
        }
        #endregion
    }
}