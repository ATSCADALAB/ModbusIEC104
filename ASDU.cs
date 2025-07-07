using System;
using System.Collections.Generic;
using System.IO;
using ModbusIEC104.Common;

namespace ModbusIEC104
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

        /// <summary>Lỗi parse nếu có</summary>
        public string ParseError { get; private set; }

        /// <summary>Độ dài ASDU (bytes)</summary>
        public int Length => CalculateLength();
        #endregion

        #region CONSTRUCTORS
        /// <summary>
        /// Constructor mặc định
        /// </summary>
        public ASDU()
        {
            InformationObjects = new List<InformationObject>();
            IsValid = true;
        }

        /// <summary>
        /// Constructor từ byte array
        /// </summary>
        /// <param name="data">Dữ liệu ASDU</param>
        public ASDU(byte[] data) : this()
        {
            ParseASADU(data);
        }

        /// <summary>
        /// Constructor với các tham số cơ bản
        /// </summary>
        /// <param name="typeId">Type ID</param>
        /// <param name="cot">Cause of Transmission</param>
        /// <param name="commonAddress">Common Address</param>
        public ASDU(byte typeId, byte cot, ushort commonAddress) : this()
        {
            TypeID = typeId;
            CauseOfTransmission = cot;
            CommonAddress = commonAddress;
            SequenceBit = false;
            TestBit = false;
            NegativeBit = false;
            OriginatorAddress = 0;
        }
        #endregion

        #region PARSING METHODS
        /// <summary>
        /// Parse ASDU từ byte array
        /// </summary>
        /// <param name="data">Dữ liệu ASDU</param>
        private void ParseASADU(byte[] data)
        {
            try
            {
                if (data == null || data.Length < 6)
                {
                    IsValid = false;
                    ParseError = "ASDU data too short (minimum 6 bytes required)";
                    return;
                }

                RawData = new byte[data.Length];
                Array.Copy(data, RawData, data.Length);

                using (var stream = new MemoryStream(data))
                using (var reader = new BinaryReader(stream))
                {
                    // Byte 0: Type Identification
                    TypeID = reader.ReadByte();

                    // Byte 1: Variable Structure Qualifier
                    var vsq = reader.ReadByte();
                    SequenceBit = (vsq & 0x80) != 0;
                    NumberOfElements = (byte)(vsq & 0x7F);

                    // Byte 2: Cause of Transmission
                    var cotByte = reader.ReadByte();
                    TestBit = (cotByte & 0x80) != 0;
                    NegativeBit = (cotByte & 0x40) != 0;
                    CauseOfTransmission = (byte)(cotByte & 0x3F);

                    // Byte 3: Originator Address
                    OriginatorAddress = reader.ReadByte();

                    // Bytes 4-5: Common Address (Little Endian)
                    CommonAddress = reader.ReadUInt16();

                    // Validate header
                    if (!ValidateHeader())
                    {
                        IsValid = false;
                        return;
                    }

                    // Parse Information Objects
                    ParseInformationObjects(reader);
                }
            }
            catch (Exception ex)
            {
                IsValid = false;
                ParseError = $"ASDU parsing error: {ex.Message}";
            }
        }

        /// <summary>
        /// Validate ASDU header
        /// </summary>
        /// <returns>True nếu header hợp lệ</returns>
        private bool ValidateHeader()
        {
            if (!IEC104Constants.IsValidTypeID(TypeID))
            {
                ParseError = $"Invalid TypeID: {TypeID}";
                return false;
            }

            if (!IEC104Constants.IsValidCOT(CauseOfTransmission))
            {
                ParseError = $"Invalid COT: {CauseOfTransmission}";
                return false;
            }

            if (!IEC104Constants.IsValidCommonAddress(CommonAddress))
            {
                ParseError = $"Invalid Common Address: {CommonAddress}";
                return false;
            }

            if (NumberOfElements == 0 || NumberOfElements > IEC104Constants.MAX_INFO_OBJECTS_PER_ASDU)
            {
                ParseError = $"Invalid number of elements: {NumberOfElements}";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Parse Information Objects từ stream
        /// </summary>
        /// <param name="reader">Binary reader</param>
        private void ParseInformationObjects(BinaryReader reader)
        {
            InformationObjects.Clear();

            try
            {
                for (int i = 0; i < NumberOfElements; i++)
                {
                    var infoObject = ParseSingleInformationObject(reader, i == 0);
                    if (infoObject != null)
                    {
                        InformationObjects.Add(infoObject);
                    }
                    else
                    {
                        IsValid = false;
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                IsValid = false;
                ParseError = $"Error parsing information objects: {ex.Message}";
            }
        }

        /// <summary>
        /// Parse một Information Object
        /// </summary>
        /// <param name="reader">Binary reader</param>
        /// <param name="isFirst">Có phải object đầu tiên không</param>
        /// <returns>Information Object hoặc null</returns>
        private InformationObject ParseSingleInformationObject(BinaryReader reader, bool isFirst)
        {
            try
            {
                var infoObject = new InformationObject();

                // IOA (3 bytes) - chỉ có ở object đầu tiên nếu SequenceBit = true
                if (isFirst || !SequenceBit)
                {
                    if (reader.BaseStream.Position + 3 > reader.BaseStream.Length)
                        return null;

                    // IOA is 3 bytes, Little Endian
                    var ioaBytes = reader.ReadBytes(3);
                    infoObject.ObjectAddress = (uint)(ioaBytes[0] | (ioaBytes[1] << 8) | (ioaBytes[2] << 16));
                }
                else
                {
                    // Với sequence bit, IOA được tính từ object đầu tiên
                    var firstIOA = InformationObjects.Count > 0 ? InformationObjects[0].ObjectAddress : 0;
                    infoObject.ObjectAddress = firstIOA + (uint)InformationObjects.Count;
                }

                // Parse value dựa trên TypeID
                if (!ParseInformationObjectValue(reader, infoObject))
                    return null;

                infoObject.TypeId = TypeID;
                return infoObject;
            }
            catch (Exception ex)
            {
                ParseError = $"Error parsing information object: {ex.Message}";
                return null;
            }
        }

        /// <summary>
        /// Parse value của Information Object dựa trên TypeID
        /// </summary>
        /// <param name="reader">Binary reader</param>
        /// <param name="infoObject">Information object</param>
        /// <returns>True nếu thành công</returns>
        private bool ParseInformationObjectValue(BinaryReader reader, InformationObject infoObject)
        {
            try
            {
                switch (TypeID)
                {
                    // Single-point information
                    case IEC104Constants.M_SP_NA_1:
                        return ParseSinglePoint(reader, infoObject, false);

                    case IEC104Constants.M_SP_TB_1:
                        return ParseSinglePoint(reader, infoObject, true);

                    // Double-point information
                    case IEC104Constants.M_DP_NA_1:
                        return ParseDoublePoint(reader, infoObject, false);

                    case IEC104Constants.M_DP_TB_1:
                        return ParseDoublePoint(reader, infoObject, true);

                    // Step position information
                    case IEC104Constants.M_ST_NA_1:
                        return ParseStepPosition(reader, infoObject, false);

                    case IEC104Constants.M_ST_TB_1:
                        return ParseStepPosition(reader, infoObject, true);

                    // Bitstring information
                    case IEC104Constants.M_BO_NA_1:
                        return ParseBitstring32(reader, infoObject, false);

                    case IEC104Constants.M_BO_TB_1:
                        return ParseBitstring32(reader, infoObject, true);

                    // Measured value, normalized
                    case IEC104Constants.M_ME_NA_1:
                        return ParseNormalizedValue(reader, infoObject, false);

                    case IEC104Constants.M_ME_TD_1:
                        return ParseNormalizedValue(reader, infoObject, true);

                    // Measured value, scaled
                    case IEC104Constants.M_ME_NB_1:
                        return ParseScaledValue(reader, infoObject, false);

                    case IEC104Constants.M_ME_TE_1:
                        return ParseScaledValue(reader, infoObject, true);

                    // Measured value, float
                    case IEC104Constants.M_ME_NC_1:
                        return ParseFloatValue(reader, infoObject, false);

                    case IEC104Constants.M_ME_TF_1:
                        return ParseFloatValue(reader, infoObject, true);

                    // Integrated totals
                    case IEC104Constants.M_IT_NA_1:
                        return ParseIntegratedTotals(reader, infoObject, false);

                    case IEC104Constants.M_IT_TB_1:
                        return ParseIntegratedTotals(reader, infoObject, true);

                    // Commands
                    case IEC104Constants.C_SC_NA_1:
                        return ParseSingleCommand(reader, infoObject);

                    case IEC104Constants.C_DC_NA_1:
                        return ParseDoubleCommand(reader, infoObject);

                    case IEC104Constants.C_RC_NA_1:
                        return ParseRegulatingStepCommand(reader, infoObject);

                    case IEC104Constants.C_SE_NA_1:
                        return ParseSetpointCommandNormalized(reader, infoObject);

                    case IEC104Constants.C_SE_NB_1:
                        return ParseSetpointCommandScaled(reader, infoObject);

                    case IEC104Constants.C_SE_NC_1:
                        return ParseSetpointCommandFloat(reader, infoObject);

                    case IEC104Constants.C_BO_NA_1:
                        return ParseBitstringCommand(reader, infoObject);

                    // System commands
                    case IEC104Constants.C_IC_NA_1:
                        return ParseInterrogationCommand(reader, infoObject);

                    case IEC104Constants.C_CI_NA_1:
                        return ParseCounterInterrogationCommand(reader, infoObject);

                    case IEC104Constants.C_RD_NA_1:
                        return ParseReadCommand(reader, infoObject);

                    case IEC104Constants.C_CS_NA_1:
                        return ParseClockSynchronizationCommand(reader, infoObject);

                    case IEC104Constants.C_TS_NA_1:
                        return ParseTestCommand(reader, infoObject);

                    case IEC104Constants.C_RP_NA_1:
                        return ParseResetProcessCommand(reader, infoObject);

                    case IEC104Constants.C_CD_NA_1:
                        return ParseDelayAcquisitionCommand(reader, infoObject);

                    default:
                        ParseError = $"Unsupported TypeID: {TypeID}";
                        return false;
                }
            }
            catch (Exception ex)
            {
                ParseError = $"Error parsing value for TypeID {TypeID}: {ex.Message}";
                return false;
            }
        }
        #endregion

        #region VALUE PARSING METHODS
        /// <summary>
        /// Parse Single Point
        /// </summary>
        private bool ParseSinglePoint(BinaryReader reader, InformationObject infoObject, bool withTimeTag)
        {
            if (reader.BaseStream.Position + 1 > reader.BaseStream.Length)
                return false;

            var siq = reader.ReadByte();
            infoObject.Value = (siq & 0x01) != 0; // SPI bit
            infoObject.Quality = (byte)(siq & 0xF0); // Quality bits

            if (withTimeTag)
            {
                infoObject.TimeStamp = ParseCP56Time2a(reader);
            }

            return true;
        }

        /// <summary>
        /// Parse Double Point
        /// </summary>
        private bool ParseDoublePoint(BinaryReader reader, InformationObject infoObject, bool withTimeTag)
        {
            if (reader.BaseStream.Position + 1 > reader.BaseStream.Length)
                return false;

            var diq = reader.ReadByte();
            infoObject.Value = (DoublePointState)(diq & 0x03); // DPI bits
            infoObject.Quality = (byte)(diq & 0xF0); // Quality bits

            if (withTimeTag)
            {
                infoObject.TimeStamp = ParseCP56Time2a(reader);
            }

            return true;
        }

        /// <summary>
        /// Parse Step Position
        /// </summary>
        private bool ParseStepPosition(BinaryReader reader, InformationObject infoObject, bool withTimeTag)
        {
            if (reader.BaseStream.Position + 1 > reader.BaseStream.Length)
                return false;

            var vti = reader.ReadByte();
            infoObject.Value = (sbyte)(vti & 0x7F); // Value (7 bits, signed)
            infoObject.Quality = (byte)(vti & 0x80); // Transient bit

            if (withTimeTag)
            {
                infoObject.TimeStamp = ParseCP56Time2a(reader);
            }

            return true;
        }

        /// <summary>
        /// Parse Bitstring 32
        /// </summary>
        private bool ParseBitstring32(BinaryReader reader, InformationObject infoObject, bool withTimeTag)
        {
            if (reader.BaseStream.Position + 5 > reader.BaseStream.Length)
                return false;

            infoObject.Value = reader.ReadUInt32(); // 32-bit value
            infoObject.Quality = reader.ReadByte(); // Quality

            if (withTimeTag)
            {
                infoObject.TimeStamp = ParseCP56Time2a(reader);
            }

            return true;
        }

        /// <summary>
        /// Parse Normalized Value
        /// </summary>
        private bool ParseNormalizedValue(BinaryReader reader, InformationObject infoObject, bool withTimeTag)
        {
            if (reader.BaseStream.Position + 3 > reader.BaseStream.Length)
                return false;

            var rawValue = reader.ReadInt16();
            infoObject.Value = rawValue / 32768.0f; // Convert to -1.0 to +1.0 range
            infoObject.Quality = reader.ReadByte();

            if (withTimeTag)
            {
                infoObject.TimeStamp = ParseCP56Time2a(reader);
            }

            return true;
        }

        /// <summary>
        /// Parse Scaled Value
        /// </summary>
        private bool ParseScaledValue(BinaryReader reader, InformationObject infoObject, bool withTimeTag)
        {
            if (reader.BaseStream.Position + 3 > reader.BaseStream.Length)
                return false;

            infoObject.Value = reader.ReadInt16(); // Scaled value
            infoObject.Quality = reader.ReadByte();

            if (withTimeTag)
            {
                infoObject.TimeStamp = ParseCP56Time2a(reader);
            }

            return true;
        }

        /// <summary>
        /// Parse Float Value
        /// </summary>
        private bool ParseFloatValue(BinaryReader reader, InformationObject infoObject, bool withTimeTag)
        {
            if (reader.BaseStream.Position + 5 > reader.BaseStream.Length)
                return false;

            infoObject.Value = reader.ReadSingle(); // IEEE 754 float
            infoObject.Quality = reader.ReadByte();

            if (withTimeTag)
            {
                infoObject.TimeStamp = ParseCP56Time2a(reader);
            }

            return true;
        }

        /// <summary>
        /// Parse Integrated Totals
        /// </summary>
        private bool ParseIntegratedTotals(BinaryReader reader, InformationObject infoObject, bool withTimeTag)
        {
            if (reader.BaseStream.Position + 5 > reader.BaseStream.Length)
                return false;

            var bcr = reader.ReadBytes(5);
            infoObject.Value = BitConverter.ToInt32(bcr, 0); // Counter value (32 bits)
            infoObject.Quality = bcr[4]; // Sequence notation + quality

            if (withTimeTag)
            {
                infoObject.TimeStamp = ParseCP56Time2a(reader);
            }

            return true;
        }

        /// <summary>
        /// Parse Single Command
        /// </summary>
        private bool ParseSingleCommand(BinaryReader reader, InformationObject infoObject)
        {
            if (reader.BaseStream.Position + 1 > reader.BaseStream.Length)
                return false;

            var sco = reader.ReadByte();
            infoObject.Value = (sco & 0x01) != 0; // Command state
            infoObject.Quality = (byte)(sco & 0xFC); // Qualifier and S/E bit

            return true;
        }

        /// <summary>
        /// Parse Double Command
        /// </summary>
        private bool ParseDoubleCommand(BinaryReader reader, InformationObject infoObject)
        {
            if (reader.BaseStream.Position + 1 > reader.BaseStream.Length)
                return false;

            var dco = reader.ReadByte();
            infoObject.Value = (DoublePointState)(dco & 0x03); // Command state
            infoObject.Quality = (byte)(dco & 0xFC); // Qualifier and S/E bit

            return true;
        }

        /// <summary>
        /// Parse Regulating Step Command
        /// </summary>
        private bool ParseRegulatingStepCommand(BinaryReader reader, InformationObject infoObject)
        {
            if (reader.BaseStream.Position + 1 > reader.BaseStream.Length)
                return false;

            var rco = reader.ReadByte();
            infoObject.Value = (sbyte)((rco & 0x03) == 1 ? -1 : (rco & 0x03) == 2 ? 1 : 0); // Step direction
            infoObject.Quality = (byte)(rco & 0xFC); // Qualifier and S/E bit

            return true;
        }

        /// <summary>
        /// Parse Setpoint Command Normalized
        /// </summary>
        private bool ParseSetpointCommandNormalized(BinaryReader reader, InformationObject infoObject)
        {
            if (reader.BaseStream.Position + 3 > reader.BaseStream.Length)
                return false;

            var rawValue = reader.ReadInt16();
            infoObject.Value = rawValue / 32768.0f; // Convert to normalized value
            infoObject.Quality = reader.ReadByte(); // Qualifier

            return true;
        }

        /// <summary>
        /// Parse Setpoint Command Scaled
        /// </summary>
        private bool ParseSetpointCommandScaled(BinaryReader reader, InformationObject infoObject)
        {
            if (reader.BaseStream.Position + 3 > reader.BaseStream.Length)
                return false;

            infoObject.Value = reader.ReadInt16(); // Scaled value
            infoObject.Quality = reader.ReadByte(); // Qualifier

            return true;
        }

        /// <summary>
        /// Parse Setpoint Command Float
        /// </summary>
        private bool ParseSetpointCommandFloat(BinaryReader reader, InformationObject infoObject)
        {
            if (reader.BaseStream.Position + 5 > reader.BaseStream.Length)
                return false;

            infoObject.Value = reader.ReadSingle(); // Float value
            infoObject.Quality = reader.ReadByte(); // Qualifier

            return true;
        }

        /// <summary>
        /// Parse Bitstring Command
        /// </summary>
        private bool ParseBitstringCommand(BinaryReader reader, InformationObject infoObject)
        {
            if (reader.BaseStream.Position + 4 > reader.BaseStream.Length)
                return false;

            infoObject.Value = reader.ReadUInt32(); // 32-bit bitstring
            infoObject.Quality = 0; // No quality for commands

            return true;
        }

        /// <summary>
        /// Parse Interrogation Command
        /// </summary>
        private bool ParseInterrogationCommand(BinaryReader reader, InformationObject infoObject)
        {
            if (reader.BaseStream.Position + 1 > reader.BaseStream.Length)
                return false;

            infoObject.Value = reader.ReadByte(); // Qualifier of interrogation
            infoObject.Quality = 0;

            return true;
        }

        /// <summary>
        /// Parse Counter Interrogation Command
        /// </summary>
        private bool ParseCounterInterrogationCommand(BinaryReader reader, InformationObject infoObject)
        {
            if (reader.BaseStream.Position + 1 > reader.BaseStream.Length)
                return false;

            infoObject.Value = reader.ReadByte(); // Qualifier of counter interrogation
            infoObject.Quality = 0;

            return true;
        }

        /// <summary>
        /// Parse Read Command
        /// </summary>
        private bool ParseReadCommand(BinaryReader reader, InformationObject infoObject)
        {
            // Read command has no additional data
            infoObject.Value = null;
            infoObject.Quality = 0;
            return true;
        }

        /// <summary>
        /// Parse Clock Synchronization Command
        /// </summary>
        private bool ParseClockSynchronizationCommand(BinaryReader reader, InformationObject infoObject)
        {
            infoObject.Value = ParseCP56Time2a(reader);
            infoObject.Quality = 0;
            return infoObject.Value != null;
        }

        /// <summary>
        /// Parse Test Command
        /// </summary>
        private bool ParseTestCommand(BinaryReader reader, InformationObject infoObject)
        {
            if (reader.BaseStream.Position + 2 > reader.BaseStream.Length)
                return false;

            infoObject.Value = reader.ReadUInt16(); // Fixed test pattern
            infoObject.Quality = 0;

            return true;
        }

        /// <summary>
        /// Parse Reset Process Command
        /// </summary>
        private bool ParseResetProcessCommand(BinaryReader reader, InformationObject infoObject)
        {
            if (reader.BaseStream.Position + 1 > reader.BaseStream.Length)
                return false;

            infoObject.Value = reader.ReadByte(); // Qualifier of reset process command
            infoObject.Quality = 0;

            return true;
        }

        /// <summary>
        /// Parse Delay Acquisition Command
        /// </summary>
        private bool ParseDelayAcquisitionCommand(BinaryReader reader, InformationObject infoObject)
        {
            if (reader.BaseStream.Position + 2 > reader.BaseStream.Length)
                return false;

            infoObject.Value = reader.ReadUInt16(); // Delay time in milliseconds
            infoObject.Quality = 0;

            return true;
        }

        /// <summary>
        /// Parse CP56Time2a timestamp
        /// </summary>
        private DateTime? ParseCP56Time2a(BinaryReader reader)
        {
            try
            {
                if (reader.BaseStream.Position + 7 > reader.BaseStream.Length)
                    return null;

                var timeBytes = reader.ReadBytes(7);
                return Converter.ConvertCP56Time2a(timeBytes);
            }
            catch
            {
                return null;
            }
        }
        #endregion

        #region SERIALIZATION METHODS
        /// <summary>
        /// Chuyển ASDU thành byte array
        /// </summary>
        /// <returns>Byte array</returns>
        public byte[] ToByteArray()
        {
            try
            {
                using (var stream = new MemoryStream())
                using (var writer = new BinaryWriter(stream))
                {
                    // Update number of elements
                    NumberOfElements = (byte)Math.Min(InformationObjects.Count, IEC104Constants.MAX_INFO_OBJECTS_PER_ASDU);

                    // Write header
                    WriteHeader(writer);

                    // Write information objects
                    WriteInformationObjects(writer);

                    return stream.ToArray();
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error serializing ASDU: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Write ASDU header
        /// </summary>
        private void WriteHeader(BinaryWriter writer)
        {
            // Byte 0: Type Identification
            writer.Write(TypeID);

            // Byte 1: Variable Structure Qualifier
            var vsq = (byte)(NumberOfElements & 0x7F);
            if (SequenceBit)
                vsq |= 0x80;
            writer.Write(vsq);

            // Byte 2: Cause of Transmission
            var cotByte = (byte)(CauseOfTransmission & 0x3F);
            if (NegativeBit)
                cotByte |= 0x40;
            if (TestBit)
                cotByte |= 0x80;
            writer.Write(cotByte);

            // Byte 3: Originator Address
            writer.Write(OriginatorAddress);

            // Bytes 4-5: Common Address (Little Endian)
            writer.Write(CommonAddress);
        }

        /// <summary>
        /// Write information objects
        /// </summary>
        private void WriteInformationObjects(BinaryWriter writer)
        {
            for (int i = 0; i < Math.Min(InformationObjects.Count, NumberOfElements); i++)
            {
                var infoObject = InformationObjects[i];
                WriteInformationObject(writer, infoObject, i == 0);
            }
        }

        /// <summary>
        /// Write một information object
        /// </summary>
        private void WriteInformationObject(BinaryWriter writer, InformationObject infoObject, bool isFirst)
        {
            // Write IOA (chỉ ở object đầu tiên nếu SequenceBit = true)
            if (isFirst || !SequenceBit)
            {
                var ioaBytes = new byte[3];
                ioaBytes[0] = (byte)(infoObject.ObjectAddress & 0xFF);
                ioaBytes[1] = (byte)((infoObject.ObjectAddress >> 8) & 0xFF);
                ioaBytes[2] = (byte)((infoObject.ObjectAddress >> 16) & 0xFF);
                writer.Write(ioaBytes);
            }

            // Write value dựa trên TypeID
            WriteInformationObjectValue(writer, infoObject);
        }

        /// <summary>
        /// Write value của information object
        /// </summary>
        private void WriteInformationObjectValue(BinaryWriter writer, InformationObject infoObject)
        {
            switch (TypeID)
            {
                case IEC104Constants.M_SP_NA_1:
                    WriteSinglePoint(writer, infoObject, false);
                    break;

                case IEC104Constants.M_SP_TB_1:
                    WriteSinglePoint(writer, infoObject, true);
                    break;

                case IEC104Constants.M_DP_NA_1:
                    WriteDoublePoint(writer, infoObject, false);
                    break;

                case IEC104Constants.M_DP_TB_1:
                    WriteDoublePoint(writer, infoObject, true);
                    break;

                case IEC104Constants.M_ME_NC_1:
                    WriteFloatValue(writer, infoObject, false);
                    break;

                case IEC104Constants.M_ME_TF_1:
                    WriteFloatValue(writer, infoObject, true);
                    break;

                case IEC104Constants.C_SC_NA_1:
                    WriteSingleCommand(writer, infoObject);
                    break;

                case IEC104Constants.C_IC_NA_1:
                    WriteInterrogationCommand(writer, infoObject);
                    break;

                // Add more cases as needed
                default:
                    WriteGenericValue(writer, infoObject);
                    break;
            }
        }

        /// <summary>
        /// Write Single Point value
        /// </summary>
        private void WriteSinglePoint(BinaryWriter writer, InformationObject infoObject, bool withTimeTag)
        {
            var siq = (byte)(infoObject.Quality & 0xF0);
            if (Convert.ToBoolean(infoObject.Value))
                siq |= 0x01;
            writer.Write(siq);

            if (withTimeTag)
            {
                WriteCP56Time2a(writer, infoObject.TimeStamp);
            }
        }

        /// <summary>
        /// Write Double Point value
        /// </summary>
        private void WriteDoublePoint(BinaryWriter writer, InformationObject infoObject, bool withTimeTag)
        {
            var diq = (byte)(infoObject.Quality & 0xF0);
            if (infoObject.Value is DoublePointState state)
            {
                diq |= (byte)((int)state & 0x03);
            }
            writer.Write(diq);

            if (withTimeTag)
            {
                WriteCP56Time2a(writer, infoObject.TimeStamp);
            }
        }

        /// <summary>
        /// Write Float value
        /// </summary>
        private void WriteFloatValue(BinaryWriter writer, InformationObject infoObject, bool withTimeTag)
        {
            if (infoObject.Value is float floatValue)
            {
                writer.Write(floatValue);
            }
            else
            {
                writer.Write(Convert.ToSingle(infoObject.Value));
            }
            writer.Write(infoObject.Quality);

            if (withTimeTag)
            {
                WriteCP56Time2a(writer, infoObject.TimeStamp);
            }
        }

        /// <summary>
        /// Write Single Command
        /// </summary>
        private void WriteSingleCommand(BinaryWriter writer, InformationObject infoObject)
        {
            var sco = infoObject.Quality;
            if (Convert.ToBoolean(infoObject.Value))
                sco |= 0x01;
            writer.Write(sco);
        }

        /// <summary>
        /// Write Interrogation Command
        /// </summary>
        private void WriteInterrogationCommand(BinaryWriter writer, InformationObject infoObject)
        {
            if (infoObject.Value is byte qualifier)
            {
                writer.Write(qualifier);
            }
            else
            {
                writer.Write((byte)20); // Default: station interrogation
            }
        }

        /// <summary>
        /// Write generic value (fallback)
        /// </summary>
        private void WriteGenericValue(BinaryWriter writer, InformationObject infoObject)
        {
            // Basic implementation for unknown types
            if (infoObject.Value is byte byteValue)
            {
                writer.Write(byteValue);
            }
            else if (infoObject.Value is short shortValue)
            {
                writer.Write(shortValue);
            }
            else if (infoObject.Value is int intValue)
            {
                writer.Write(intValue);
            }
            else if (infoObject.Value is float floatValue)
            {
                writer.Write(floatValue);
            }
            else
            {
                writer.Write((byte)0); // Default
            }

            writer.Write(infoObject.Quality);
        }

        /// <summary>
        /// Write CP56Time2a timestamp
        /// </summary>
        private void WriteCP56Time2a(BinaryWriter writer, DateTime timeStamp)
        {
            var timeBytes = Converter.ConvertToCP56Time2a(timeStamp);
            writer.Write(timeBytes);
        }
        #endregion

        #region UTILITY METHODS
        /// <summary>
        /// Tính độ dài ASDU
        /// </summary>
        private int CalculateLength()
        {
            int length = 6; // Header length

            foreach (var infoObject in InformationObjects)
            {
                // IOA (3 bytes) - chỉ có ở object đầu tiên nếu SequenceBit = true
                if (InformationObjects.IndexOf(infoObject) == 0 || !SequenceBit)
                {
                    length += 3;
                }

                // Value length dựa trên TypeID
                length += IEC104Constants.GetDataSizeFromTypeID(TypeID);
            }

            return length;
        }

        /// <summary>
        /// Thêm Information Object
        /// </summary>
        /// <param name="infoObject">Information Object</param>
        public void AddInformationObject(InformationObject infoObject)
        {
            if (infoObject == null)
                throw new ArgumentNullException(nameof(infoObject));

            if (InformationObjects.Count >= IEC104Constants.MAX_INFO_OBJECTS_PER_ASDU)
                throw new InvalidOperationException($"Maximum number of information objects ({IEC104Constants.MAX_INFO_OBJECTS_PER_ASDU}) exceeded");

            InformationObjects.Add(infoObject);
            NumberOfElements = (byte)InformationObjects.Count;
        }

        /// <summary>
        /// Xóa tất cả Information Objects
        /// </summary>
        public void ClearInformationObjects()
        {
            InformationObjects.Clear();
            NumberOfElements = 0;
        }

        /// <summary>
        /// Validate ASDU
        /// </summary>
        /// <returns>True nếu hợp lệ</returns>
        public bool Validate()
        {
            if (!IEC104Constants.IsValidTypeID(TypeID))
                return false;

            if (!IEC104Constants.IsValidCOT(CauseOfTransmission))
                return false;

            if (!IEC104Constants.IsValidCommonAddress(CommonAddress))
                return false;

            if (NumberOfElements != InformationObjects.Count)
                return false;

            if (NumberOfElements == 0 || NumberOfElements > IEC104Constants.MAX_INFO_OBJECTS_PER_ASDU)
                return false;

            // Validate each information object
            foreach (var infoObject in InformationObjects)
            {
                if (!IEC104Constants.IsValidIOA(infoObject.ObjectAddress))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Lấy thông tin chi tiết ASDU
        /// </summary>
        /// <returns>Thông tin chi tiết</returns>
        public string GetDetailedInfo()
        {
            var info = $"ASDU Information:\n";
            info += $"  TypeID: {TypeID} ({IEC104Constants.GetTypeIDName(TypeID)})\n";
            info += $"  Sequence Bit: {SequenceBit}\n";
            info += $"  Number of Elements: {NumberOfElements}\n";
            info += $"  Test Bit: {TestBit}\n";
            info += $"  Negative Bit: {NegativeBit}\n";
            info += $"  COT: {CauseOfTransmission} ({IEC104Constants.GetCOTName(CauseOfTransmission)})\n";
            info += $"  Originator Address: {OriginatorAddress}\n";
            info += $"  Common Address: {CommonAddress}\n";
            info += $"  Is Valid: {IsValid}\n";
            info += $"  Length: {Length} bytes\n";

            if (!string.IsNullOrEmpty(ParseError))
            {
                info += $"  Parse Error: {ParseError}\n";
            }

            info += $"  Information Objects ({InformationObjects.Count}):\n";
            for (int i = 0; i < InformationObjects.Count; i++)
            {
                var obj = InformationObjects[i];
                info += $"    [{i}] IOA: {obj.ObjectAddress}, Value: {obj.Value}, Quality: 0x{obj.Quality:X2}";
                if (obj.TimeStamp != DateTime.MinValue)
                {
                    info += $", Time: {obj.TimeStamp:yyyy-MM-dd HH:mm:ss.fff}";
                }
                info += "\n";
            }

            return info;
        }

        /// <summary>
        /// Clone ASDU
        /// </summary>
        /// <returns>Bản copy của ASDU</returns>
        public ASDU Clone()
        {
            var clone = new ASDU
            {
                TypeID = this.TypeID,
                SequenceBit = this.SequenceBit,
                NumberOfElements = this.NumberOfElements,
                TestBit = this.TestBit,
                NegativeBit = this.NegativeBit,
                CauseOfTransmission = this.CauseOfTransmission,
                OriginatorAddress = this.OriginatorAddress,
                CommonAddress = this.CommonAddress,
                IsValid = this.IsValid,
                ParseError = this.ParseError
            };

            foreach (var infoObject in InformationObjects)
            {
                clone.InformationObjects.Add(infoObject.Clone());
            }

            return clone;
        }
        #endregion

        #region STATIC FACTORY METHODS
        /// <summary>
        /// Tạo ASDU cho General Interrogation
        /// </summary>
        /// <param name="commonAddress">Common Address</param>
        /// <param name="qualifier">Qualifier (default: station interrogation)</param>
        /// <returns>ASDU</returns>
        public static ASDU CreateGeneralInterrogation(ushort commonAddress, byte qualifier = IEC104Constants.QOI_STATION_INTERROGATION)
        {
            var asdu = new ASDU(IEC104Constants.C_IC_NA_1, IEC104Constants.COT_ACTIVATION, commonAddress);
            
            var infoObject = new InformationObject
            {
                ObjectAddress = 0, // IOA = 0 for station interrogation
                Value = qualifier,
                Quality = 0
            };

            asdu.AddInformationObject(infoObject);
            return asdu;
        }

        /// <summary>
        /// Tạo ASDU cho Counter Interrogation
        /// </summary>
        /// <param name="commonAddress">Common Address</param>
        /// <param name="qualifier">Qualifier (default: general counter)</param>
        /// <returns>ASDU</returns>
        public static ASDU CreateCounterInterrogation(ushort commonAddress, byte qualifier = IEC104Constants.QCC_GENERAL_REQUEST_COUNTER)
        {
            var asdu = new ASDU(IEC104Constants.C_CI_NA_1, IEC104Constants.COT_ACTIVATION, commonAddress);
            
            var infoObject = new InformationObject
            {
                ObjectAddress = 0, // IOA = 0 for general counter interrogation
                Value = qualifier,
                Quality = 0
            };

            asdu.AddInformationObject(infoObject);
            return asdu;
        }

        /// <summary>
        /// Tạo ASDU cho Single Command
        /// </summary>
        /// <param name="commonAddress">Common Address</param>
        /// <param name="ioa">Information Object Address</param>
        /// <param name="commandState">Command state (On/Off)</param>
        /// <param name="selectBeforeOperate">Select before operate</param>
        /// <returns>ASDU</returns>
        public static ASDU CreateSingleCommand(ushort commonAddress, uint ioa, bool commandState, bool selectBeforeOperate = false)
        {
            var cot = selectBeforeOperate ? IEC104Constants.COT_ACTIVATION : IEC104Constants.COT_ACTIVATION;
            var asdu = new ASDU(IEC104Constants.C_SC_NA_1, cot, commonAddress);
            
            var qualifier = selectBeforeOperate ? IEC104Constants.SELECT_COMMAND : IEC104Constants.EXECUTE_COMMAND;
            
            var infoObject = new InformationObject
            {
                ObjectAddress = ioa,
                Value = commandState,
                Quality = qualifier
            };

            asdu.AddInformationObject(infoObject);
            return asdu;
        }

        /// <summary>
        /// Tạo ASDU cho Float Measurement
        /// </summary>
        /// <param name="commonAddress">Common Address</param>
        /// <param name="ioa">Information Object Address</param>
        /// <param name="value">Float value</param>
        /// <param name="quality">Quality</param>
        /// <param name="withTimeTag">Include time tag</param>
        /// <returns>ASDU</returns>
        public static ASDU CreateFloatMeasurement(ushort commonAddress, uint ioa, float value, byte quality = IEC104Constants.QDS_GOOD, bool withTimeTag = false)
        {
            var typeId = withTimeTag ? IEC104Constants.M_ME_TF_1 : IEC104Constants.M_ME_NC_1;
            var asdu = new ASDU(typeId, IEC104Constants.COT_SPONTANEOUS, commonAddress);
            
            var infoObject = new InformationObject
            {
                ObjectAddress = ioa,
                Value = value,
                Quality = quality,
                TimeStamp = withTimeTag ? DateTime.Now : DateTime.MinValue
            };

            asdu.AddInformationObject(infoObject);
            return asdu;
        }
        #endregion

        #region OVERRIDE METHODS
        /// <summary>
        /// String representation
        /// </summary>
        /// <returns>String representation</returns>
        public override string ToString()
        {
            return $"ASDU[TypeID={TypeID}, COT={CauseOfTransmission}, CA={CommonAddress}, Objects={InformationObjects.Count}, Valid={IsValid}]";
        }

        /// <summary>
        /// Equals method
        /// </summary>
        /// <param name="obj">Object to compare</param>
        /// <returns>True if equal</returns>
        public override bool Equals(object obj)
        {
            if (obj is ASDU other)
            {
                return TypeID == other.TypeID &&
                       CauseOfTransmission == other.CauseOfTransmission &&
                       CommonAddress == other.CommonAddress &&
                       SequenceBit == other.SequenceBit &&
                       TestBit == other.TestBit &&
                       NegativeBit == other.NegativeBit &&
                       OriginatorAddress == other.OriginatorAddress &&
                       InformationObjects.Count == other.InformationObjects.Count;
            }
            return false;
        }

        /// <summary>
        /// Get hash code
        /// </summary>
        /// <returns>Hash code</returns>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 23 + TypeID.GetHashCode();
                hash = hash * 23 + CauseOfTransmission.GetHashCode();
                hash = hash * 23 + CommonAddress.GetHashCode();
                hash = hash * 23 + SequenceBit.GetHashCode();
                hash = hash * 23 + TestBit.GetHashCode();
                hash = hash * 23 + NegativeBit.GetHashCode();
                hash = hash * 23 + OriginatorAddress.GetHashCode();
                return hash;
            }
        }
        #endregion
    }

    #region SUPPORTING CLASSES
    /// <summary>
    /// Information Object - đại diện cho một đối tượng thông tin trong ASDU
    /// </summary>
    public class InformationObject
    {
        #region PROPERTIES
        /// <summary>Information Object Address (IOA)</summary>
        public uint ObjectAddress { get; set; }

        /// <summary>Type ID của object</summary>
        public byte TypeId { get; set; }

        /// <summary>Giá trị của object</summary>
        public object Value { get; set; }

        /// <summary>Quality descriptor</summary>
        public byte Quality { get; set; }

        /// <summary>Time stamp (nếu có)</summary>
        public DateTime TimeStamp { get; set; } = DateTime.MinValue;

        /// <summary>Kiểm tra có time stamp không</summary>
        public bool HasTimeStamp => TimeStamp != DateTime.MinValue;
        #endregion

        #region CONSTRUCTORS
        /// <summary>
        /// Constructor mặc định
        /// </summary>
        public InformationObject()
        {
        }

        /// <summary>
        /// Constructor với các tham số
        /// </summary>
        /// <param name="objectAddress">Object Address</param>
        /// <param name="value">Value</param>
        /// <param name="quality">Quality</param>
        public InformationObject(uint objectAddress, object value, byte quality = IEC104Constants.QDS_GOOD)
        {
            ObjectAddress = objectAddress;
            Value = value;
            Quality = quality;
        }
        #endregion

        #region METHODS
        /// <summary>
        /// Clone information object
        /// </summary>
        /// <returns>Bản copy</returns>
        public InformationObject Clone()
        {
            return new InformationObject
            {
                ObjectAddress = this.ObjectAddress,
                TypeId = this.TypeId,
                Value = this.Value,
                Quality = this.Quality,
                TimeStamp = this.TimeStamp
            };
        }

        /// <summary>
        /// Lấy giá trị dạng boolean
        /// </summary>
        /// <returns>Boolean value</returns>
        public bool GetBooleanValue()
        {
            return Convert.ToBoolean(Value);
        }

        /// <summary>
        /// Lấy giá trị dạng float
        /// </summary>
        /// <returns>Float value</returns>
        public float GetFloatValue()
        {
            return Convert.ToSingle(Value);
        }

        /// <summary>
        /// Lấy giá trị dạng integer
        /// </summary>
        /// <returns>Integer value</returns>
        public int GetIntegerValue()
        {
            return Convert.ToInt32(Value);
        }

        /// <summary>
        /// Kiểm tra quality có tốt không
        /// </summary>
        /// <returns>True nếu quality tốt</returns>
        public bool IsGoodQuality()
        {
            return (Quality & IEC104Constants.QDS_INVALID) == 0;
        }

        /// <summary>
        /// String representation
        /// </summary>
        /// <returns>String representation</returns>
        public override string ToString()
        {
            var timeInfo = HasTimeStamp ? $", Time: {TimeStamp:HH:mm:ss.fff}" : "";
            return $"IOA: {ObjectAddress}, Value: {Value}, Quality: 0x{Quality:X2}{timeInfo}";
        }
        #endregion
    }

    /// <summary>
    /// Converter utility class for IEC104 data types
    /// </summary>
    public static class Converter
    {
        /// <summary>
        /// Convert CP56Time2a bytes to DateTime
        /// </summary>
        /// <param name="timeBytes">7-byte time array</param>
        /// <returns>DateTime</returns>
        public static DateTime ConvertCP56Time2a(byte[] timeBytes)
        {
            if (timeBytes == null || timeBytes.Length != 7)
                throw new ArgumentException("CP56Time2a must be exactly 7 bytes");

            try
            {
                // Milliseconds (2 bytes)
                var milliseconds = timeBytes[0] | (timeBytes[1] << 8);
                
                // Minutes (6 bits) + Invalid bit (1 bit) + Reserved (1 bit)
                var minutes = timeBytes[2] & 0x3F;
                
                // Hours (5 bits) + Reserved (3 bits)
                var hours = timeBytes[3] & 0x1F;
                
                // Day of month (5 bits) + Day of week (3 bits)
                var dayOfMonth = timeBytes[4] & 0x1F;
                
                // Month (4 bits) + Reserved (4 bits)
                var month = timeBytes[5] & 0x0F;
                
                // Year (7 bits) + Reserved (1 bit)
                var year = (timeBytes[6] & 0x7F) + 2000;

                var dateTime = new DateTime(year, month, dayOfMonth, hours, minutes, 0);
                dateTime = dateTime.AddMilliseconds(milliseconds);

                return dateTime;
            }
            catch
            {
                return DateTime.MinValue;
            }
        }

        /// <summary>
        /// Convert DateTime to CP56Time2a bytes
        /// </summary>
        /// <param name="dateTime">DateTime to convert</param>
        /// <returns>7-byte time array</returns>
        public static byte[] ConvertToCP56Time2a(DateTime dateTime)
        {
            var timeBytes = new byte[7];

            // Milliseconds (2 bytes)
            var totalMilliseconds = dateTime.Second * 1000 + dateTime.Millisecond;
            timeBytes[0] = (byte)(totalMilliseconds & 0xFF);
            timeBytes[1] = (byte)((totalMilliseconds >> 8) & 0xFF);

            // Minutes (6 bits)
            timeBytes[2] = (byte)(dateTime.Minute & 0x3F);

            // Hours (5 bits)
            timeBytes[3] = (byte)(dateTime.Hour & 0x1F);

            // Day of month (5 bits) + Day of week (3 bits)
            timeBytes[4] = (byte)((dateTime.Day & 0x1F) | (((int)dateTime.DayOfWeek + 1) << 5));

            // Month (4 bits)
            timeBytes[5] = (byte)(dateTime.Month & 0x0F);

            // Year (7 bits) - year since 2000
            timeBytes[6] = (byte)((dateTime.Year - 2000) & 0x7F);

            return timeBytes;
        }
    }
    #endregion
}