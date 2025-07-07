using System;
using System.Text.RegularExpressions;

namespace ModbusIEC104.Common
{
    /// <summary>
    /// Địa chỉ IEC104 với format: COA.IOA.TypeID
    /// Common Address.Information Object Address.Type Identification
    /// </summary>
    public class IEC104Address : Address
    {
        #region FIELDS
        private ushort commonAddress = 1;
        private uint informationObjectAddress = 1;
        private byte typeIdentification = IEC104Constants.M_ME_NC_1; // Default: float measurement
        #endregion

        #region PROPERTIES
        /// <summary>Common Address (1-65534)</summary>
        public ushort CommonAddress
        {
            get => commonAddress;
            set
            {
                if (IEC104Constants.IsValidCommonAddress(value))
                    commonAddress = value;
                else
                    throw new ArgumentOutOfRangeException(nameof(CommonAddress),
                        $"Common Address must be between {IEC104Constants.MIN_COMMON_ADDRESS} and {IEC104Constants.MAX_COMMON_ADDRESS}");
            }
        }

        /// <summary>Information Object Address (1-16777215)</summary>
        public uint InformationObjectAddress
        {
            get => informationObjectAddress;
            set
            {
                if (IEC104Constants.IsValidIOA(value))
                    informationObjectAddress = value;
                else
                    throw new ArgumentOutOfRangeException(nameof(InformationObjectAddress),
                        $"IOA must be between {IEC104Constants.MIN_IOA} and {IEC104Constants.MAX_IOA}");
            }
        }

        /// <summary>Type Identification</summary>
        public byte TypeIdentification
        {
            get => typeIdentification;
            set
            {
                if (IEC104Constants.IsValidTypeID(value))
                    typeIdentification = value;
                else
                    throw new ArgumentOutOfRangeException(nameof(TypeIdentification),
                        $"Invalid TypeID: {value}");
            }
        }

        /// <summary>Địa chỉ đầy đủ dạng string COA.IOA.TypeID</summary>
        public override string FullAddress => $"{CommonAddress}.{InformationObjectAddress}.{TypeIdentification}";

        /// <summary>Loại dữ liệu IEC104</summary>
        public IEC104DataType DataType => GetDataTypeFromTypeID(TypeIdentification);

        /// <summary>Có phải là lệnh điều khiển không</summary>
        public bool IsCommandType => IsCommandTypeID(TypeIdentification);

        /// <summary>Có phải là measurement không</summary>
        public bool IsMeasurementType => IsMeasurementTypeID(TypeIdentification);

        /// <summary>Có time tag không</summary>
        public bool HasTimeTag => HasTimeTagTypeID(TypeIdentification);

        /// <summary>Kích thước dữ liệu (bytes)</summary>
        public int DataSize => GetDataSizeFromTypeID(TypeIdentification);
        #endregion

        #region CONSTRUCTORS
        /// <summary>
        /// Constructor mặc định
        /// </summary>
        public IEC104Address() : base()
        {
        }

        /// <summary>
        /// Constructor từ chuỗi địa chỉ
        /// </summary>
        /// <param name="address">Địa chỉ dạng COA.IOA.TypeID</param>
        public IEC104Address(string address) : base()
        {
            if (!string.IsNullOrEmpty(address))
            {
                ParseAddress(address);
            }
        }

        /// <summary>
        /// Constructor từ các thành phần
        /// </summary>
        /// <param name="commonAddress">Common Address</param>
        /// <param name="informationObjectAddress">Information Object Address</param>
        /// <param name="typeIdentification">Type Identification</param>
        public IEC104Address(ushort commonAddress, uint informationObjectAddress, byte typeIdentification) : base()
        {
            CommonAddress = commonAddress;
            InformationObjectAddress = informationObjectAddress;
            TypeIdentification = typeIdentification;
        }

        /// <summary>
        /// Copy constructor
        /// </summary>
        /// <param name="other">Địa chỉ khác để copy</param>
        public IEC104Address(IEC104Address other) : base()
        {
            if (other != null)
            {
                CommonAddress = other.CommonAddress;
                InformationObjectAddress = other.InformationObjectAddress;
                TypeIdentification = other.TypeIdentification;
            }
        }
        #endregion

        #region PARSING METHODS
        /// <summary>
        /// Parse địa chỉ từ string
        /// Hỗ trợ các format:
        /// - COA.IOA.TypeID (1.1000.13)
        /// - COA.IOA (1.1000) - TypeID mặc định là M_ME_NC_1 (13)
        /// - IOA.TypeID (1000.13) - COA mặc định là 1
        /// - IOA (1000) - COA mặc định là 1, TypeID mặc định là M_ME_NC_1 (13)
        /// </summary>
        /// <param name="address">Chuỗi địa chỉ</param>
        public override void ParseAddress(string address)
        {
            if (string.IsNullOrWhiteSpace(address))
                throw new ArgumentException("Address cannot be null or empty", nameof(address));

            // Remove spaces
            address = address.Trim();

            // Regular expression để parse các format khác nhau
            var patterns = new[]
            {
                @"^(\d+)\.(\d+)\.(\d+)$",    // COA.IOA.TypeID
                @"^(\d+)\.(\d+)$",           // COA.IOA hoặc IOA.TypeID
                @"^(\d+)$"                   // IOA only
            };

            foreach (var pattern in patterns)
            {
                var match = Regex.Match(address, pattern);
                if (match.Success)
                {
                    ParseMatchedAddress(match, address);
                    return;
                }
            }

            throw new ArgumentException($"Invalid IEC104 address format: {address}. " +
                                      "Expected formats: COA.IOA.TypeID, COA.IOA, IOA.TypeID, or IOA", nameof(address));
        }

        /// <summary>
        /// Parse địa chỉ đã match với regex
        /// </summary>
        /// <param name="match">Regex match</param>
        /// <param name="originalAddress">Địa chỉ gốc</param>
        private void ParseMatchedAddress(Match match, string originalAddress)
        {
            var groups = match.Groups;

            if (groups.Count == 4) // COA.IOA.TypeID
            {
                if (!ushort.TryParse(groups[1].Value, out ushort coa) ||
                    !uint.TryParse(groups[2].Value, out uint ioa) ||
                    !byte.TryParse(groups[3].Value, out byte typeId))
                {
                    throw new ArgumentException($"Invalid numeric values in address: {originalAddress}");
                }

                CommonAddress = coa;
                InformationObjectAddress = ioa;
                TypeIdentification = typeId;
            }
            else if (groups.Count == 3) // COA.IOA hoặc IOA.TypeID
            {
                if (!uint.TryParse(groups[1].Value, out uint first) ||
                    !uint.TryParse(groups[2].Value, out uint second))
                {
                    throw new ArgumentException($"Invalid numeric values in address: {originalAddress}");
                }

                // Heuristic để phân biệt COA.IOA vs IOA.TypeID
                // Nếu second <= 255 và là TypeID hợp lệ, coi là IOA.TypeID
                if (second <= 255 && IEC104Constants.IsValidTypeID((byte)second))
                {
                    // IOA.TypeID format
                    CommonAddress = 1; // Default COA
                    InformationObjectAddress = first;
                    TypeIdentification = (byte)second;
                }
                else
                {
                    // COA.IOA format
                    if (first > ushort.MaxValue)
                        throw new ArgumentException($"Common Address too large: {first}");

                    CommonAddress = (ushort)first;
                    InformationObjectAddress = second;
                    TypeIdentification = IEC104Constants.M_ME_NC_1; // Default TypeID
                }
            }
            else if (groups.Count == 2) // IOA only
            {
                if (!uint.TryParse(groups[1].Value, out uint ioa))
                {
                    throw new ArgumentException($"Invalid numeric value in address: {originalAddress}");
                }

                CommonAddress = 1; // Default COA
                InformationObjectAddress = ioa;
                TypeIdentification = IEC104Constants.M_ME_NC_1; // Default TypeID
            }
        }

        /// <summary>
        /// Kiểm tra địa chỉ có hợp lệ không
        /// </summary>
        /// <param name="address">Chuỗi địa chỉ</param>
        /// <returns>True nếu hợp lệ</returns>
        public static bool IsValidAddress(string address)
        {
            try
            {
                var testAddress = new IEC104Address(address);
                return true;
            }
            catch
            {
                return false;
            }
        }
        #endregion

        #region TYPE ID HELPER METHODS
        /// <summary>
        /// Lấy data type từ TypeID
        /// </summary>
        /// <param name="typeId">Type ID</param>
        /// <returns>Data type</returns>
        public static IEC104DataType GetDataTypeFromTypeID(byte typeId)
        {
            switch (typeId)
            {
                // Single/Double point
                case IEC104Constants.M_SP_NA_1:
                case IEC104Constants.M_SP_TB_1:
                    return IEC104DataType.SinglePoint;

                case IEC104Constants.M_DP_NA_1:
                case IEC104Constants.M_DP_TB_1:
                    return IEC104DataType.DoublePoint;

                // Step position
                case IEC104Constants.M_ST_NA_1:
                case IEC104Constants.M_ST_TB_1:
                    return IEC104DataType.StepPosition;

                // Bitstring
                case IEC104Constants.M_BO_NA_1:
                case IEC104Constants.M_BO_TB_1:
                case IEC104Constants.C_BO_NA_1:
                    return IEC104DataType.Bitstring32;

                // Normalized value
                case IEC104Constants.M_ME_NA_1:
                case IEC104Constants.M_ME_TD_1:
                case IEC104Constants.C_SE_NA_1:
                    return IEC104DataType.NormalizedValue;

                // Scaled value
                case IEC104Constants.M_ME_NB_1:
                case IEC104Constants.M_ME_TE_1:
                case IEC104Constants.C_SE_NB_1:
                    return IEC104DataType.ScaledValue;

                // Float value
                case IEC104Constants.M_ME_NC_1:
                case IEC104Constants.M_ME_TF_1:
                case IEC104Constants.C_SE_NC_1:
                    return IEC104DataType.FloatValue;

                // Integrated totals
                case IEC104Constants.M_IT_NA_1:
                case IEC104Constants.M_IT_TB_1:
                    return IEC104DataType.IntegratedTotals;

                // Commands
                case IEC104Constants.C_SC_NA_1:
                    return IEC104DataType.SingleCommand;

                case IEC104Constants.C_DC_NA_1:
                    return IEC104DataType.DoubleCommand;

                case IEC104Constants.C_RC_NA_1:
                    return IEC104DataType.RegulatingStepCommand;

                // System commands
                case IEC104Constants.C_IC_NA_1:
                    return IEC104DataType.InterrogationCommand;

                case IEC104Constants.C_CI_NA_1:
                    return IEC104DataType.CounterInterrogationCommand;

                case IEC104Constants.C_RD_NA_1:
                    return IEC104DataType.ReadCommand;

                case IEC104Constants.C_CS_NA_1:
                    return IEC104DataType.ClockSynchronizationCommand;

                case IEC104Constants.C_TS_NA_1:
                    return IEC104DataType.TestCommand;

                case IEC104Constants.C_RP_NA_1:
                    return IEC104DataType.ResetProcessCommand;

                case IEC104Constants.C_CD_NA_1:
                    return IEC104DataType.DelayAcquisitionCommand;

                default:
                    return IEC104DataType.Unknown;
            }
        }

        /// <summary>
        /// Kiểm tra TypeID có phải là command không
        /// </summary>
        /// <param name="typeId">Type ID</param>
        /// <returns>True nếu là command</returns>
        public static bool IsCommandTypeID(byte typeId)
        {
            return typeId >= 45 && typeId <= 110;
        }

        /// <summary>
        /// Kiểm tra TypeID có phải là measurement không
        /// </summary>
        /// <param name="typeId">Type ID</param>
        /// <returns>True nếu là measurement</returns>
        public static bool IsMeasurementTypeID(byte typeId)
        {
            return (typeId >= 1 && typeId <= 40) || (typeId >= 30 && typeId <= 40);
        }

        /// <summary>
        /// Kiểm tra TypeID có time tag không
        /// </summary>
        /// <param name="typeId">Type ID</param>
        /// <returns>True nếu có time tag</returns>
        public static bool HasTimeTagTypeID(byte typeId)
        {
            return typeId >= 30 && typeId <= 40;
        }

        /// <summary>
        /// Lấy kích thước dữ liệu từ TypeID
        /// </summary>
        /// <param name="typeId">Type ID</param>
        /// <returns>Kích thước (bytes)</returns>
        public static int GetDataSizeFromTypeID(byte typeId)
        {
            switch (typeId)
            {
                // 1 byte
                case IEC104Constants.M_SP_NA_1:
                case IEC104Constants.M_DP_NA_1:
                case IEC104Constants.M_ST_NA_1:
                case IEC104Constants.C_SC_NA_1:
                case IEC104Constants.C_DC_NA_1:
                case IEC104Constants.C_RC_NA_1:
                    return 1;

                // 2 bytes
                case IEC104Constants.M_ME_NA_1:
                case IEC104Constants.M_ME_NB_1:
                case IEC104Constants.C_SE_NA_1:
                case IEC104Constants.C_SE_NB_1:
                    return 2;

                // 4 bytes
                case IEC104Constants.M_BO_NA_1:
                case IEC104Constants.M_ME_NC_1:
                case IEC104Constants.C_BO_NA_1:
                case IEC104Constants.C_SE_NC_1:
                    return 4;

                // 5 bytes (4 bytes + quality)
                case IEC104Constants.M_IT_NA_1:
                    return 5;

                // Time tagged versions (+7 bytes for CP56Time2a)
                case IEC104Constants.M_SP_TB_1:
                case IEC104Constants.M_DP_TB_1:
                case IEC104Constants.M_ST_TB_1:
                    return 1 + IEC104Constants.CP56TIME2A_LENGTH;

                case IEC104Constants.M_ME_TD_1:
                case IEC104Constants.M_ME_TE_1:
                    return 2 + IEC104Constants.CP56TIME2A_LENGTH;

                case IEC104Constants.M_BO_TB_1:
                case IEC104Constants.M_ME_TF_1:
                    return 4 + IEC104Constants.CP56TIME2A_LENGTH;

                case IEC104Constants.M_IT_TB_1:
                    return 5 + IEC104Constants.CP56TIME2A_LENGTH;

                // System commands
                case IEC104Constants.C_IC_NA_1:
                case IEC104Constants.C_CI_NA_1:
                case IEC104Constants.C_RD_NA_1:
                case IEC104Constants.C_TS_NA_1:
                case IEC104Constants.C_RP_NA_1:
                case IEC104Constants.C_CD_NA_1:
                    return 1;

                case IEC104Constants.C_CS_NA_1:
                    return IEC104Constants.CP56TIME2A_LENGTH;

                default:
                    return 0;
            }
        }
        #endregion

        #region OVERRIDE METHODS
        /// <summary>
        /// So sánh hai địa chỉ
        /// </summary>
        /// <param name="obj">Đối tượng để so sánh</param>
        /// <returns>True nếu bằng nhau</returns>
        public override bool Equals(object obj)
        {
            if (obj is IEC104Address other)
            {
                return CommonAddress == other.CommonAddress &&
                       InformationObjectAddress == other.InformationObjectAddress &&
                       TypeIdentification == other.TypeIdentification;
            }
            return false;
        }

        /// <summary>
        /// Lấy hash code
        /// </summary>
        /// <returns>Hash code</returns>
        public override int GetHashCode()
        {
            unchecked // Ngăn chặn lỗi tràn số
            {
                int hash = 17;
                hash = hash * 23 + CommonAddress.GetHashCode();
                hash = hash * 23 + InformationObjectAddress.GetHashCode();
                hash = hash * 23 + TypeIdentification.GetHashCode();
                return hash;
            }
        }

        /// <summary>
        /// Chuyển thành string
        /// </summary>
        /// <returns>String representation</returns>
        public override string ToString()
        {
            return FullAddress;
        }

        /// <summary>
        /// Clone địa chỉ
        /// </summary>
        /// <returns>Bản copy của địa chỉ</returns>
        public override object Clone()
        {
            return new IEC104Address(this);
        }
        #endregion

        #region VALIDATION METHODS
        /// <summary>
        /// Validate địa chỉ
        /// </summary>
        /// <returns>True nếu hợp lệ</returns>
        public override bool IsValid()
        {
            return IEC104Constants.IsValidCommonAddress(CommonAddress) &&
                   IEC104Constants.IsValidIOA(InformationObjectAddress) &&
                   IEC104Constants.IsValidTypeID(TypeIdentification);
        }

        /// <summary>
        /// Lấy thông tin chi tiết về địa chỉ
        /// </summary>
        /// <returns>Thông tin chi tiết</returns>
        public string GetDetailedInfo()
        {
            return $"IEC104 Address: {FullAddress}\n" +
                   $"  Common Address: {CommonAddress}\n" +
                   $"  Information Object Address: {InformationObjectAddress}\n" +
                   $"  Type Identification: {TypeIdentification} ({IEC104Constants.GetTypeIDName(TypeIdentification)})\n" +
                   $"  Data Type: {DataType}\n" +
                   $"  Is Command: {IsCommandType}\n" +
                   $"  Is Measurement: {IsMeasurementType}\n" +
                   $"  Has Time Tag: {HasTimeTag}\n" +
                   $"  Data Size: {DataSize} bytes";
        }
        #endregion

        #region STATIC HELPER METHODS
        /// <summary>
        /// Tạo địa chỉ cho General Interrogation
        /// </summary>
        /// <param name="commonAddress">Common Address</param>
        /// <returns>Địa chỉ General Interrogation</returns>
        public static IEC104Address CreateGeneralInterrogationAddress(ushort commonAddress = 1)
        {
            return new IEC104Address(commonAddress, 0, IEC104Constants.C_IC_NA_1);
        }

        /// <summary>
        /// Tạo địa chỉ cho Counter Interrogation
        /// </summary>
        /// <param name="commonAddress">Common Address</param>
        /// <returns>Địa chỉ Counter Interrogation</returns>
        public static IEC104Address CreateCounterInterrogationAddress(ushort commonAddress = 1)
        {
            return new IEC104Address(commonAddress, 0, IEC104Constants.C_CI_NA_1);
        }

        /// <summary>
        /// Tạo địa chỉ measurement
        /// </summary>
        /// <param name="commonAddress">Common Address</param>
        /// <param name="ioa">Information Object Address</param>
        /// <param name="dataType">Loại dữ liệu</param>
        /// <param name="withTimeTag">Có time tag không</param>
        /// <returns>Địa chỉ measurement</returns>
        public static IEC104Address CreateMeasurementAddress(ushort commonAddress, uint ioa,
            IEC104DataType dataType, bool withTimeTag = false)
        {
            byte typeId;

            switch (dataType)
            {
                case IEC104DataType.SinglePoint:
                    typeId = withTimeTag ? IEC104Constants.M_SP_TB_1 : IEC104Constants.M_SP_NA_1;
                    break;
                case IEC104DataType.DoublePoint:
                    typeId = withTimeTag ? IEC104Constants.M_DP_TB_1 : IEC104Constants.M_DP_NA_1;
                    break;
                case IEC104DataType.StepPosition:
                    typeId = withTimeTag ? IEC104Constants.M_ST_TB_1 : IEC104Constants.M_ST_NA_1;
                    break;
                case IEC104DataType.Bitstring32:
                    typeId = withTimeTag ? IEC104Constants.M_BO_TB_1 : IEC104Constants.M_BO_NA_1;
                    break;
                case IEC104DataType.NormalizedValue:
                    typeId = withTimeTag ? IEC104Constants.M_ME_TD_1 : IEC104Constants.M_ME_NA_1;
                    break;
                case IEC104DataType.ScaledValue:
                    typeId = withTimeTag ? IEC104Constants.M_ME_TE_1 : IEC104Constants.M_ME_NB_1;
                    break;
                case IEC104DataType.FloatValue:
                    typeId = withTimeTag ? IEC104Constants.M_ME_TF_1 : IEC104Constants.M_ME_NC_1;
                    break;
                case IEC104DataType.IntegratedTotals:
                    typeId = withTimeTag ? IEC104Constants.M_IT_TB_1 : IEC104Constants.M_IT_NA_1;
                    break;
                default:
                    typeId = IEC104Constants.M_ME_NC_1;
                    break;
            }

            return new IEC104Address(commonAddress, ioa, typeId);
        }

        /// <summary>
        /// Tạo địa chỉ command
        /// </summary>
        /// <param name="commonAddress">Common Address</param>
        /// <param name="ioa">Information Object Address</param>
        /// <param name="commandType">Loại command</param>
        /// <returns>Địa chỉ command</returns>
        public static IEC104Address CreateCommandAddress(ushort commonAddress, uint ioa, IEC104DataType commandType)
        {
            byte typeId;

            switch (commandType)
            {
                case IEC104DataType.SingleCommand:
                    typeId = IEC104Constants.C_SC_NA_1;
                    break;
                case IEC104DataType.DoubleCommand:
                    typeId = IEC104Constants.C_DC_NA_1;
                    break;
                case IEC104DataType.RegulatingStepCommand:
                    typeId = IEC104Constants.C_RC_NA_1;
                    break;
                case IEC104DataType.NormalizedValue:
                    typeId = IEC104Constants.C_SE_NA_1;
                    break;
                case IEC104DataType.ScaledValue:
                    typeId = IEC104Constants.C_SE_NB_1;
                    break;
                case IEC104DataType.FloatValue:
                    typeId = IEC104Constants.C_SE_NC_1;
                    break;
                case IEC104DataType.Bitstring32:
                    typeId = IEC104Constants.C_BO_NA_1;
                    break;
                default:
                    typeId = IEC104Constants.C_SC_NA_1;
                    break;
            }

            return new IEC104Address(commonAddress, ioa, typeId);
        }
        #endregion
    }

    #region SUPPORTING ENUMS
    /// <summary>
    /// Loại dữ liệu IEC104
    /// </summary>
    public enum IEC104DataType
    {
        Unknown,

        // Process information
        SinglePoint,
        DoublePoint,
        StepPosition,
        Bitstring32,
        NormalizedValue,
        ScaledValue,
        FloatValue,
        IntegratedTotals,

        // Commands
        SingleCommand,
        DoubleCommand,
        RegulatingStepCommand,

        // System commands
        InterrogationCommand,
        CounterInterrogationCommand,
        ReadCommand,
        ClockSynchronizationCommand,
        TestCommand,
        ResetProcessCommand,
        DelayAcquisitionCommand
    }
    #endregion
}