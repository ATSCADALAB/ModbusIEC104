using System;
using System.Collections.Generic;
using System.IO;

namespace IEC104
{
    /// <summary>
    /// Application Service Data Unit - chứa dữ liệu thực tế của giao thức IEC104
    /// </summary>
    public class ASDU
    {
        #region PROPERTIES

        /// <summary>Type Identification (1 byte)</summary>
        public byte TypeID { get; set; }

        /// <summary>Sequence bit - cho biết có phải sequence of elements không</summary>
        public bool SequenceBit { get; set; }

        /// <summary>Number of elements (7 bits)</summary>
        public byte NumberOfElements { get; set; }

        /// <summary>Test bit - dùng cho test mode</summary>
        public bool TestBit { get; set; }

        /// <summary>Positive/Negative bit - false=positive, true=negative</summary>
        public bool NegativeBit { get; set; }

        /// <summary>Cause of Transmission (6 bits)</summary>
        public byte CauseOfTransmission { get; set; }

        /// <summary>Originator Address (1 byte)</summary>
        public byte OriginatorAddress { get; set; }

        /// <summary>Common Address (2 bytes)</summary>
        public ushort CommonAddress { get; set; }

        /// <summary>Information Objects</summary>
        public List<InformationObject> InformationObjects { get; set; }

        /// <summary>Kiểm tra ASDU có hợp lệ không</summary>
        public bool IsValid { get; private set; } = true;

        /// <summary>Raw data của ASDU (để debug)</summary>
        public byte[] RawData { get; private set; }

        #endregion

        #region CONSTRUCTORS

        /// <summary>
        /// Constructor mặc định
        /// </summary>
        public ASDU()
        {
            InformationObjects = new List<InformationObject>();
        }

        /// <summary>
        /// Constructor từ byte array
        /// </summary>
        /// <param name="data">Dữ liệu ASDU</param>
        public ASDU(byte[] data) : this()
        {
            ParseASADU(data);
        }

        #endregion

        #region PARSING METHODS

        /// <summary>
        /// Parse ASDU từ byte array
        /// </summary>
        /// <param name="data">Dữ liệu ASDU</param>
        public void ParseASADU(byte[] data)
        {
            try
            {
                if (data == null || data.Length < 6) // Minimum ASDU header length
                {
                    IsValid = false;
                    return;
                }

                RawData = data;

                using (var stream = new MemoryStream(data))
                using (var reader = new BinaryReader(stream))
                {
                    // Parse ASDU header (6 bytes)
                    ParseHeader(reader);

                    // Parse Information Objects
                    ParseInformationObjects(reader);
                }
            }
            catch
            {
                IsValid = false;
            }
        }

        /// <summary>
        /// Parse ASDU header
        /// </summary>
        /// <param name="reader">Binary reader</param>
        private void ParseHeader(BinaryReader reader)
        {
            // Byte 1: Type Identification
            TypeID = reader.ReadByte();

            // Byte 2: VSQ (Variable Structure Qualifier)
            byte vsq = reader.ReadByte();
            SequenceBit = (vsq & 0x80) != 0;
            NumberOfElements = (byte)(vsq & 0x7F);

            // Byte 3: COT (Cause of Transmission)
            byte cot = reader.ReadByte();
            TestBit = (cot & 0x80) != 0;
            NegativeBit = (cot & 0x40) != 0;
            CauseOfTransmission = (byte)(cot & 0x3F);

            // Byte 4: OA (Originator Address)
            OriginatorAddress = reader.ReadByte();

            // Byte 5-6: CA (Common Address) - Little Endian
            byte caLow = reader.ReadByte();
            byte caHigh = reader.ReadByte();
            CommonAddress = (ushort)(caLow | (caHigh << 8));
        }

        /// <summary>
        /// Parse Information Objects từ dữ liệu còn lại
        /// </summary>
        /// <param name="reader">Binary reader</param>
        private void ParseInformationObjects(BinaryReader reader)
        {
            InformationObjects.Clear();

            for (int i = 0; i < NumberOfElements && reader.BaseStream.Position < reader.BaseStream.Length; i++)
            {
                var infoObj = new InformationObject();

                if (SequenceBit)
                {
                    // Sequence of elements: IOA chỉ có ở element đầu tiên
                    if (i == 0)
                    {
                        infoObj.InformationObjectAddress = ReadIOA(reader);
                    }
                    else
                    {
                        // IOA tự động tăng cho các element tiếp theo
                        infoObj.InformationObjectAddress = InformationObjects[0].InformationObjectAddress + (uint)i;
                    }
                }
                else
                {
                    // Sequence of information objects: mỗi element có IOA riêng
                    infoObj.InformationObjectAddress = ReadIOA(reader);
                }

                // Parse information element data dựa trên TypeID
                infoObj.ElementData = ParseElementData(reader, TypeID);
                infoObj.TypeID = TypeID;

                InformationObjects.Add(infoObj);
            }
        }

        /// <summary>
        /// Đọc Information Object Address (3 bytes, Little Endian)
        /// </summary>
        /// <param name="reader">Binary reader</param>
        /// <returns>IOA value</returns>
        private uint ReadIOA(BinaryReader reader)
        {
            if (reader.BaseStream.Position + 3 > reader.BaseStream.Length)
                return 0;

            byte byte1 = reader.ReadByte();
            byte byte2 = reader.ReadByte();
            byte byte3 = reader.ReadByte();

            return (uint)(byte1 | (byte2 << 8) | (byte3 << 16));
        }

        /// <summary>
        /// Parse element data dựa trên Type ID
        /// </summary>
        /// <param name="reader">Binary reader</param>
        /// <param name="typeId">Type ID</param>
        /// <returns>Element data</returns>
        private byte[] ParseElementData(BinaryReader reader, byte typeId)
        {
            int elementSize = GetElementSize(typeId);

            if (elementSize <= 0 || reader.BaseStream.Position + elementSize > reader.BaseStream.Length)
                return new byte[0];

            return reader.ReadBytes(elementSize);
        }

        /// <summary>
        /// Lấy kích thước element data dựa trên Type ID
        /// </summary>
        /// <param name="typeId">Type ID</param>
        /// <returns>Kích thước element (bytes)</returns>
        private int GetElementSize(byte typeId)
        {
            switch (typeId)
            {
                // Single-point information
                case IEC104Constants.M_SP_NA_1:
                    return 1; // SIQ (1 byte)

                // Double-point information
                case IEC104Constants.M_DP_NA_1:
                    return 1; // DIQ (1 byte)

                // Step position information
                case IEC104Constants.M_ST_NA_1:
                    return 2; // VTI + QDS (1 + 1 bytes)

                // Bitstring of 32 bit
                case IEC104Constants.M_BO_NA_1:
                    return 5; // BSI + QDS (4 + 1 bytes)

                // Measured value, normalized value
                case IEC104Constants.M_ME_NA_1:
                    return 3; // NVA + QDS (2 + 1 bytes)

                // Measured value, scaled value
                case IEC104Constants.M_ME_NB_1:
                    return 3; // SVA + QDS (2 + 1 bytes)

                // Measured value, short floating point
                case IEC104Constants.M_ME_NC_1:
                    return 5; // IEEE STD 754 + QDS (4 + 1 bytes)

                // Integrated totals
                case IEC104Constants.M_IT_NA_1:
                    return 5; // BCR (5 bytes)

                // Single command
                case IEC104Constants.C_SC_NA_1:
                    return 1; // SCO (1 byte)

                // Double command
                case IEC104Constants.C_DC_NA_1:
                    return 1; // DCO (1 byte)

                // Regulating step command
                case IEC104Constants.C_RC_NA_1:
                    return 1; // RCO (1 byte)

                // Set-point command, normalized value
                case IEC104Constants.C_SE_NA_1:
                    return 3; // NVA + QOS (2 + 1 bytes)

                // Set-point command, scaled value
                case IEC104Constants.C_SE_NB_1:
                    return 3; // SVA + QOS (2 + 1 bytes)

                // Set-point command, short floating point
                case IEC104Constants.C_SE_NC_1:
                    return 5; // IEEE STD 754 + QOS (4 + 1 bytes)

                // Interrogation command
                case IEC104Constants.C_IC_NA_1:
                    return 1; // QOI (1 byte)

                // Counter interrogation command
                case IEC104Constants.C_CI_NA_1:
                    return 1; // QCC (1 byte)

                // Clock synchronization command
                case IEC104Constants.C_CS_NA_1:
                    return 7; // CP56Time2a (7 bytes)

                default:
                    return 0; // Unknown type
            }
        }

        #endregion

        #region BUILDING METHODS

        /// <summary>
        /// Chuyển ASDU thành byte array
        /// </summary>
        /// <returns>Byte array của ASDU</returns>
        public byte[] ToByteArray()
        {
            if (!IsValid || InformationObjects.Count == 0)
                return null;

            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                // Write ASDU header
                WriteHeader(writer);

                // Write Information Objects
                WriteInformationObjects(writer);

                return stream.ToArray();
            }
        }

        /// <summary>
        /// Ghi ASDU header
        /// </summary>
        /// <param name="writer">Binary writer</param>
        private void WriteHeader(BinaryWriter writer)
        {
            // Byte 1: Type Identification
            writer.Write(TypeID);

            // Byte 2: VSQ (Variable Structure Qualifier)
            byte vsq = (byte)(NumberOfElements & 0x7F);
            if (SequenceBit) vsq |= 0x80;
            writer.Write(vsq);

            // Byte 3: COT (Cause of Transmission)
            byte cot = (byte)(CauseOfTransmission & 0x3F);
            if (TestBit) cot |= 0x80;
            if (NegativeBit) cot |= 0x40;
            writer.Write(cot);

            // Byte 4: OA (Originator Address)
            writer.Write(OriginatorAddress);

            // Byte 5-6: CA (Common Address) - Little Endian
            writer.Write((byte)(CommonAddress & 0xFF));
            writer.Write((byte)((CommonAddress >> 8) & 0xFF));
        }

        /// <summary>
        /// Ghi Information Objects
        /// </summary>
        /// <param name="writer">Binary writer</param>
        private void WriteInformationObjects(BinaryWriter writer)
        {
            for (int i = 0; i < InformationObjects.Count; i++)
            {
                var infoObj = InformationObjects[i];

                if (SequenceBit)
                {
                    // Sequence of elements: chỉ ghi IOA cho element đầu tiên
                    if (i == 0)
                    {
                        WriteIOA(writer, infoObj.InformationObjectAddress);
                    }
                }
                else
                {
                    // Sequence of information objects: ghi IOA cho mỗi element
                    WriteIOA(writer, infoObj.InformationObjectAddress);
                }

                // Ghi element data
                if (infoObj.ElementData != null && infoObj.ElementData.Length > 0)
                {
                    writer.Write(infoObj.ElementData);
                }
            }
        }

        /// <summary>
        /// Ghi Information Object Address (3 bytes, Little Endian)
        /// </summary>
        /// <param name="writer">Binary writer</param>
        /// <param name="ioa">IOA value</param>
        private void WriteIOA(BinaryWriter writer, uint ioa)
        {
            writer.Write((byte)(ioa & 0xFF));
            writer.Write((byte)((ioa >> 8) & 0xFF));
            writer.Write((byte)((ioa >> 16) & 0xFF));
        }

        #endregion

        #region FACTORY METHODS

        /// <summary>
        /// Tạo ASDU từ byte array
        /// </summary>
        /// <param name="data">Dữ liệu ASDU</param>
        /// <returns>ASDU object hoặc null nếu không hợp lệ</returns>
        public static ASDU FromByteArray(byte[] data)
        {
            var asdu = new ASDU(data);
            return asdu.IsValid ? asdu : null;
        }

        /// <summary>
        /// Tạo ASDU cho General Interrogation
        /// </summary>
        /// <param name="commonAddress">Common Address</param>
        /// <param name="qualifier">Qualifier of Interrogation (mặc định: station interrogation)</param>
        /// <returns>ASDU object</returns>
        public static ASDU CreateInterrogationCommand(ushort commonAddress, byte qualifier = IEC104Constants.QOI_STATION)
        {
            var asdu = new ASDU
            {
                TypeID = IEC104Constants.C_IC_NA_1,
                SequenceBit = false,
                NumberOfElements = 1,
                TestBit = false,
                NegativeBit = false,
                CauseOfTransmission = IEC104Constants.COT_ACTIVATION,
                OriginatorAddress = 0,
                CommonAddress = commonAddress
            };

            // Thêm Information Object cho interrogation
            var infoObj = new InformationObject
            {
                InformationObjectAddress = 0, // IOA = 0 cho station interrogation
                TypeID = IEC104Constants.C_IC_NA_1,
                ElementData = new byte[] { qualifier }
            };

            asdu.InformationObjects.Add(infoObj);
            return asdu;
        }

        /// <summary>
        /// Tạo ASDU cho Single Command
        /// </summary>
        /// <param name="commonAddress">Common Address</param>
        /// <param name="ioa">Information Object Address</param>
        /// <param name="commandValue">Command value (true/false)</param>
        /// <param name="selectExecute">Select (true) hoặc Execute (false)</param>
        /// <param name="qualifier">Qualifier of command</param>
        /// <returns>ASDU object</returns>
        public static ASDU CreateSingleCommand(ushort commonAddress, uint ioa, bool commandValue,
            bool selectExecute = false, byte qualifier = IEC104Constants.QU_NO_ADDITIONAL)
        {
            var asdu = new ASDU
            {
                TypeID = IEC104Constants.C_SC_NA_1,
                SequenceBit = false,
                NumberOfElements = 1,
                TestBit = false,
                NegativeBit = false,
                CauseOfTransmission = IEC104Constants.COT_ACTIVATION,
                OriginatorAddress = 0,
                CommonAddress = commonAddress
            };

            // Tạo SCO (Single Command Object)
            byte sco = (byte)(qualifier & 0x1F); // QU: 5 bits
            if (commandValue) sco |= 0x01;       // SCS: bit 0
            if (selectExecute) sco |= 0x80;      // S/E: bit 7

            var infoObj = new InformationObject
            {
                InformationObjectAddress = ioa,
                TypeID = IEC104Constants.C_SC_NA_1,
                ElementData = new byte[] { sco }
            };

            asdu.InformationObjects.Add(infoObj);
            return asdu;
        }

        #endregion

        #region UTILITY METHODS

        /// <summary>
        /// Kiểm tra có phải negative confirmation không
        /// </summary>
        /// <returns>True nếu là negative confirmation</returns>
        public bool IsNegativeConfirmation()
        {
            return NegativeBit;
        }

        /// <summary>
        /// Kiểm tra có phải test frame không
        /// </summary>
        /// <returns>True nếu là test frame</returns>
        public bool IsTestFrame()
        {
            return TestBit;
        }

        /// <summary>
        /// Lấy thông tin ASDU dưới dạng string để debug
        /// </summary>
        /// <returns>String mô tả ASDU</returns>
        public override string ToString()
        {
            if (!IsValid)
                return "Invalid ASDU";

            return $"ASDU: TypeID={TypeID}, COT={CauseOfTransmission}, CA={CommonAddress}, " +
                   $"Objects={InformationObjects.Count}, SQ={SequenceBit}, Test={TestBit}, Neg={NegativeBit}";
        }

        #endregion
    }

    /// <summary>
    /// Information Object - đại diện cho một đối tượng thông tin trong ASDU
    /// </summary>
    public class InformationObject
    {
        /// <summary>Information Object Address (3 bytes)</summary>
        public uint InformationObjectAddress { get; set; }

        /// <summary>Type Identification</summary>
        public byte TypeID { get; set; }

        /// <summary>Element data (nội dung thực tế của object)</summary>
        public byte[] ElementData { get; set; }

        /// <summary>
        /// Constructor mặc định
        /// </summary>
        public InformationObject()
        {
            ElementData = new byte[0];
        }

        /// <summary>
        /// Lấy thông tin object dưới dạng string để debug
        /// </summary>
        /// <returns>String mô tả object</returns>
        public override string ToString()
        {
            return $"InfoObj: IOA={InformationObjectAddress}, TypeID={TypeID}, Data={ElementData?.Length ?? 0} bytes";
        }
    }
}