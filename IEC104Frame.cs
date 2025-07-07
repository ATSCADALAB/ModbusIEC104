using System;
using System.IO;

namespace IEC104
{
    /// <summary>
    /// Định nghĩa format của frame IEC104
    /// </summary>
    public enum FrameFormat : byte
    {
        /// <summary>Information frame (I-format)</summary>
        I_FORMAT = 0,
        /// <summary>Supervisory frame (S-format)</summary>
        S_FORMAT = 1,
        /// <summary>Unnumbered frame (U-format)</summary>
        U_FORMAT = 3
    }

    /// <summary>
    /// Định nghĩa các chức năng U-frame
    /// </summary>
    public enum UFrameFunction : byte
    {
        /// <summary>STARTDT activation</summary>
        STARTDT_ACT = IEC104Constants.STARTDT_ACT,
        /// <summary>STARTDT confirmation</summary>
        STARTDT_CON = IEC104Constants.STARTDT_CON,
        /// <summary>STOPDT activation</summary>
        STOPDT_ACT = IEC104Constants.STOPDT_ACT,
        /// <summary>STOPDT confirmation</summary>
        STOPDT_CON = IEC104Constants.STOPDT_CON,
        /// <summary>TESTFR activation</summary>
        TESTFR_ACT = IEC104Constants.TESTFR_ACT,
        /// <summary>TESTFR confirmation</summary>
        TESTFR_CON = IEC104Constants.TESTFR_CON
    }

    /// <summary>
    /// Lớp xử lý frame IEC104 - tương tự ModbusSocket trong việc xử lý dữ liệu thô
    /// </summary>
    public class IEC104Frame
    {
        #region PROPERTIES

        /// <summary>Start byte (luôn là 0x68)</summary>
        public byte StartByte { get; set; } = IEC104Constants.START_BYTE;

        /// <summary>Độ dài APDU (không bao gồm start byte và chính nó)</summary>
        public byte APDULength { get; set; }

        /// <summary>Format của frame (I, S, hoặc U)</summary>
        public FrameFormat Format { get; private set; }

        /// <summary>Control field (4 bytes)</summary>
        public byte[] ControlField { get; set; } = new byte[IEC104Constants.CONTROL_FIELD_LENGTH];

        /// <summary>ASDU data (chỉ có trong I-format)</summary>
        public byte[] ASDUData { get; set; }

        /// <summary>Send sequence number (cho I-format và S-format)</summary>
        public ushort SendSequenceNumber { get; set; }

        /// <summary>Receive sequence number (cho I-format và S-format)</summary>
        public ushort ReceiveSequenceNumber { get; set; }

        /// <summary>U-frame function (cho U-format)</summary>
        public UFrameFunction UFunction { get; set; }

        /// <summary>Kiểm tra frame có hợp lệ không</summary>
        public bool IsValid { get; private set; } = true;

        #endregion

        #region CONSTRUCTORS

        /// <summary>
        /// Constructor mặc định
        /// </summary>
        public IEC104Frame()
        {
        }

        /// <summary>
        /// Constructor từ byte array
        /// </summary>
        /// <param name="data">Dữ liệu frame</param>
        public IEC104Frame(byte[] data)
        {
            ParseFrame(data);
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
            var frame = new IEC104Frame
            {
                Format = FrameFormat.I_FORMAT,
                SendSequenceNumber = sendSeq,
                ReceiveSequenceNumber = recvSeq,
                ASDUData = asduData ?? new byte[0],
                APDULength = (byte)(IEC104Constants.CONTROL_FIELD_LENGTH + (asduData?.Length ?? 0))
            };

            frame.BuildControlField();
            return frame;
        }

        /// <summary>
        /// Tạo S-format frame (Supervisory frame)
        /// </summary>
        /// <param name="recvSeq">Receive sequence number</param>
        /// <returns>S-format frame</returns>
        public static IEC104Frame CreateSFrame(ushort recvSeq)
        {
            var frame = new IEC104Frame
            {
                Format = FrameFormat.S_FORMAT,
                ReceiveSequenceNumber = recvSeq,
                APDULength = IEC104Constants.CONTROL_FIELD_LENGTH
            };

            frame.BuildControlField();
            return frame;
        }

        /// <summary>
        /// Tạo U-format frame (Unnumbered frame)
        /// </summary>
        /// <param name="function">U-frame function</param>
        /// <returns>U-format frame</returns>
        public static IEC104Frame CreateUFrame(UFrameFunction function)
        {
            var frame = new IEC104Frame
            {
                Format = FrameFormat.U_FORMAT,
                UFunction = function,
                APDULength = IEC104Constants.CONTROL_FIELD_LENGTH
            };

            frame.BuildControlField();
            return frame;
        }

        #endregion

        #region PARSING METHODS

        /// <summary>
        /// Parse frame từ byte array
        /// </summary>
        /// <param name="data">Dữ liệu frame</param>
        public void ParseFrame(byte[] data)
        {
            try
            {
                if (data == null || data.Length < IEC104Constants.MIN_FRAME_LENGTH)
                {
                    IsValid = false;
                    return;
                }

                using (var stream = new MemoryStream(data))
                using (var reader = new BinaryReader(stream))
                {
                    // Đọc start byte
                    StartByte = reader.ReadByte();
                    if (StartByte != IEC104Constants.START_BYTE)
                    {
                        IsValid = false;
                        return;
                    }

                    // Đọc APDU length
                    APDULength = reader.ReadByte();
                    if (APDULength > IEC104Constants.MAX_APDU_LENGTH)
                    {
                        IsValid = false;
                        return;
                    }

                    // Kiểm tra độ dài frame
                    if (data.Length < 2 + APDULength)
                    {
                        IsValid = false;
                        return;
                    }

                    // Đọc control field
                    ControlField = reader.ReadBytes(IEC104Constants.CONTROL_FIELD_LENGTH);

                    // Parse control field để xác định format
                    ParseControlField();

                    // Đọc ASDU data nếu là I-format
                    if (Format == FrameFormat.I_FORMAT && APDULength > IEC104Constants.CONTROL_FIELD_LENGTH)
                    {
                        int asduLength = APDULength - IEC104Constants.CONTROL_FIELD_LENGTH;
                        ASDUData = reader.ReadBytes(asduLength);
                    }
                }
            }
            catch
            {
                IsValid = false;
            }
        }

        /// <summary>
        /// Parse control field để xác định format và sequence numbers
        /// </summary>
        private void ParseControlField()
        {
            if (ControlField == null || ControlField.Length < IEC104Constants.CONTROL_FIELD_LENGTH)
            {
                IsValid = false;
                return;
            }

            // Byte đầu tiên của control field xác định format
            byte firstByte = ControlField[0];

            if ((firstByte & 0x01) == 0)
            {
                // I-format: bit 0 = 0
                Format = FrameFormat.I_FORMAT;

                // Send sequence number (14 bits): byte 0-1, bỏ bit LSB của byte 0
                SendSequenceNumber = (ushort)(((ControlField[1] << 8) | ControlField[0]) >> 1);

                // Receive sequence number (14 bits): byte 2-3, bỏ bit LSB của byte 2
                ReceiveSequenceNumber = (ushort)(((ControlField[3] << 8) | ControlField[2]) >> 1);
            }
            else if ((firstByte & 0x03) == 0x01)
            {
                // S-format: bit 1-0 = 01
                Format = FrameFormat.S_FORMAT;

                // Chỉ có receive sequence number
                ReceiveSequenceNumber = (ushort)(((ControlField[3] << 8) | ControlField[2]) >> 1);
            }
            else if ((firstByte & 0x03) == 0x03)
            {
                // U-format: bit 1-0 = 11
                Format = FrameFormat.U_FORMAT;

                // Xác định U-frame function từ control field
                UFunction = (UFrameFunction)firstByte;
            }
            else
            {
                IsValid = false;
            }
        }

        #endregion

        #region BUILDING METHODS

        /// <summary>
        /// Xây dựng control field dựa trên format và sequence numbers
        /// </summary>
        private void BuildControlField()
        {
            ControlField = new byte[IEC104Constants.CONTROL_FIELD_LENGTH];

            switch (Format)
            {
                case FrameFormat.I_FORMAT:
                    // I-format: Send sequence number ở byte 0-1, Receive sequence number ở byte 2-3
                    ushort sendSeq = (ushort)(SendSequenceNumber << 1); // Shift left 1 bit
                    ushort recvSeq = (ushort)(ReceiveSequenceNumber << 1); // Shift left 1 bit

                    ControlField[0] = (byte)(sendSeq & 0xFF);
                    ControlField[1] = (byte)((sendSeq >> 8) & 0xFF);
                    ControlField[2] = (byte)(recvSeq & 0xFF);
                    ControlField[3] = (byte)((recvSeq >> 8) & 0xFF);
                    break;

                case FrameFormat.S_FORMAT:
                    // S-format: byte 0-1 = 0x01, Receive sequence number ở byte 2-3
                    ControlField[0] = 0x01;
                    ControlField[1] = 0x00;

                    ushort recvSeqS = (ushort)(ReceiveSequenceNumber << 1); // Shift left 1 bit
                    ControlField[2] = (byte)(recvSeqS & 0xFF);
                    ControlField[3] = (byte)((recvSeqS >> 8) & 0xFF);
                    break;

                case FrameFormat.U_FORMAT:
                    // U-format: Function code ở byte 0, các byte khác = 0
                    ControlField[0] = (byte)UFunction;
                    ControlField[1] = 0x00;
                    ControlField[2] = 0x00;
                    ControlField[3] = 0x00;
                    break;
            }
        }

        #endregion

        #region CONVERSION METHODS

        /// <summary>
        /// Chuyển frame thành byte array để gửi
        /// </summary>
        /// <returns>Byte array của frame</returns>
        public byte[] ToByteArray()
        {
            if (!IsValid)
                return null;

            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                // Write start byte
                writer.Write(StartByte);

                // Write APDU length
                writer.Write(APDULength);

                // Write control field
                writer.Write(ControlField);

                // Write ASDU data nếu có
                if (ASDUData != null && ASDUData.Length > 0)
                {
                    writer.Write(ASDUData);
                }

                return stream.ToArray();
            }
        }

        /// <summary>
        /// Tạo frame từ byte array
        /// </summary>
        /// <param name="data">Dữ liệu frame</param>
        /// <returns>IEC104Frame object hoặc null nếu không hợp lệ</returns>
        public static IEC104Frame FromByteArray(byte[] data)
        {
            var frame = new IEC104Frame(data);
            return frame.IsValid ? frame : null;
        }

        #endregion

        #region UTILITY METHODS

        /// <summary>
        /// Kiểm tra có phải U-frame với function cụ thể không
        /// </summary>
        /// <param name="function">Function cần kiểm tra</param>
        /// <returns>True nếu đúng</returns>
        public bool IsUFrame(UFrameFunction function)
        {
            return Format == FrameFormat.U_FORMAT && UFunction == function;
        }

        /// <summary>
        /// Kiểm tra có phải I-frame không
        /// </summary>
        /// <returns>True nếu là I-frame</returns>
        public bool IsIFrame()
        {
            return Format == FrameFormat.I_FORMAT;
        }

        /// <summary>
        /// Kiểm tra có phải S-frame không
        /// </summary>
        /// <returns>True nếu là S-frame</returns>
        public bool IsSFrame()
        {
            return Format == FrameFormat.S_FORMAT;
        }

        /// <summary>
        /// Lấy thông tin frame dưới dạng string để debug
        /// </summary>
        /// <returns>String mô tả frame</returns>
        public override string ToString()
        {
            if (!IsValid)
                return "Invalid Frame";

            switch (Format)
            {
                case FrameFormat.I_FORMAT:
                    return $"I-Frame: Send={SendSequenceNumber}, Recv={ReceiveSequenceNumber}, ASDU Length={ASDUData?.Length ?? 0}";

                case FrameFormat.S_FORMAT:
                    return $"S-Frame: Recv={ReceiveSequenceNumber}";

                case FrameFormat.U_FORMAT:
                    return $"U-Frame: Function={UFunction}";

                default:
                    return "Unknown Frame";
            }
        }

        #endregion
    }
}