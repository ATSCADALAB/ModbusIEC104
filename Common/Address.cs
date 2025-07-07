namespace IEC104
{
    /// <summary>
    /// Access Right cho IEC104
    /// </summary>
    public enum AccessRight
    {
        /// <summary>Chỉ đọc</summary>
        ReadOnly,
        /// <summary>Đọc và ghi</summary>
        ReadWrite
    }

    /// <summary>
    /// IEC104 Address - hoàn toàn độc lập, không extend từ ModbusTCP
    /// Format: CA.IOA.TypeID[.ElementIndex]
    /// </summary>
    public class IEC104Address
    {
        #region PROPERTIES

        /// <summary>Common Address (2 bytes) - địa chỉ trạm (1-65535)</summary>
        public ushort CommonAddress { get; set; } = 1;

        /// <summary>Information Object Address (3 bytes) - địa chỉ đối tượng thông tin (1-16777215)</summary>
        public uint InformationObjectAddress { get; set; }

        /// <summary>Type Identification - loại dữ liệu IEC104 (1-127)</summary>
        public byte TypeID { get; set; }

        /// <summary>Element Index - cho sequence of elements (0-255)</summary>
        public int ElementIndex { get; set; } = 0;

        /// <summary>IEC104 Data Type enum</summary>
        public IEC104DataType IEC104DataType { get; set; }

        /// <summary>Access Right dựa trên TypeID</summary>
        public AccessRight AccessRight
        {
            get
            {
                if (IsMonitoringType(TypeID))
                    return AccessRight.ReadOnly;
                else if (IsControlType(TypeID) || IsSystemType(TypeID))
                    return AccessRight.ReadWrite;
                else
                    return AccessRight.ReadOnly;
            }
        }

        /// <summary>Kiểm tra có phải discrete type (Single/Double Point) không</summary>
        public bool IsDiscrete
        {
            get
            {
                return TypeID == IEC104Constants.M_SP_NA_1 ||  // Single Point
                       TypeID == IEC104Constants.M_DP_NA_1 ||  // Double Point
                       TypeID == IEC104Constants.C_SC_NA_1 ||  // Single Command
                       TypeID == IEC104Constants.C_DC_NA_1;    // Double Command
            }
        }

        /// <summary>Kích thước element theo bytes</summary>
        public int ElementSize
        {
            get => GetIEC104ElementSize(TypeID);
        }

        /// <summary>Kiểm tra address có hợp lệ không</summary>
        public bool IsValid
        {
            get
            {
                return CommonAddress > 0 && CommonAddress <= 65535 &&
                       InformationObjectAddress > 0 && InformationObjectAddress <= 16777215 &&
                       TypeID > 0 && TypeID <= 127 &&
                       ElementIndex >= 0 && ElementIndex <= 255 &&
                       IsValidTypeID(TypeID);
            }
        }

        #endregion

        #region CONSTRUCTORS

        /// <summary>
        /// Constructor mặc định
        /// </summary>
        public IEC104Address()
        {
        }

        /// <summary>
        /// Constructor với các tham số cơ bản
        /// </summary>
        /// <param name="commonAddress">Common Address (1-65535)</param>
        /// <param name="ioa">Information Object Address (1-16777215)</param>
        /// <param name="typeId">Type Identification (1-127)</param>
        public IEC104Address(ushort commonAddress, uint ioa, byte typeId)
        {
            CommonAddress = commonAddress;
            InformationObjectAddress = ioa;
            TypeID = typeId;
            IEC104DataType = (IEC104DataType)typeId;
        }

        /// <summary>
        /// Constructor đầy đủ
        /// </summary>
        /// <param name="commonAddress">Common Address</param>
        /// <param name="ioa">Information Object Address</param>
        /// <param name="typeId">Type Identification</param>
        /// <param name="elementIndex">Element Index</param>
        public IEC104Address(ushort commonAddress, uint ioa, byte typeId, int elementIndex)
            : this(commonAddress, ioa, typeId)
        {
            ElementIndex = elementIndex;
        }

        #endregion

        #region STATIC HELPER METHODS

        /// <summary>
        /// Kiểm tra TypeID có phải là Monitoring type không (Read Only)
        /// </summary>
        /// <param name="typeId">Type ID</param>
        /// <returns>True nếu là monitoring type</returns>
        public static bool IsMonitoringType(byte typeId)
        {
            return typeId >= 1 && typeId <= 44; // M_xx_xx types
        }

        /// <summary>
        /// Kiểm tra TypeID có phải là Control type không (Read/Write)
        /// </summary>
        /// <param name="typeId">Type ID</param>
        /// <returns>True nếu là control type</returns>
        public static bool IsControlType(byte typeId)
        {
            return typeId >= 45 && typeId <= 99; // C_xx_xx types
        }

        /// <summary>
        /// Kiểm tra TypeID có phải là System command không
        /// </summary>
        /// <param name="typeId">Type ID</param>
        /// <returns>True nếu là system command</returns>
        public static bool IsSystemType(byte typeId)
        {
            return typeId >= 100 && typeId <= 127; // System commands
        }

        /// <summary>
        /// Lấy kích thước element cho IEC104 TypeID
        /// </summary>
        /// <param name="typeId">Type ID</param>
        /// <returns>Kích thước element (bytes)</returns>
        public static int GetIEC104ElementSize(byte typeId)
        {
            switch (typeId)
            {
                // 1 byte elements
                case IEC104Constants.M_SP_NA_1:    // SIQ
                case IEC104Constants.M_DP_NA_1:    // DIQ
                case IEC104Constants.C_SC_NA_1:    // SCO
                case IEC104Constants.C_DC_NA_1:    // DCO
                case IEC104Constants.C_RC_NA_1:    // RCO
                case IEC104Constants.C_IC_NA_1:    // QOI
                case IEC104Constants.C_CI_NA_1:    // QCC
                    return 1;

                // 2 byte elements  
                case IEC104Constants.M_ST_NA_1:    // VTI + QDS
                    return 2;

                // 3 byte elements
                case IEC104Constants.M_ME_NA_1:    // NVA + QDS
                case IEC104Constants.M_ME_NB_1:    // SVA + QDS
                case IEC104Constants.C_SE_NA_1:    // NVA + QOS
                case IEC104Constants.C_SE_NB_1:    // SVA + QOS
                    return 3;

                // 5 byte elements
                case IEC104Constants.M_BO_NA_1:    // BSI + QDS
                case IEC104Constants.M_ME_NC_1:    // IEEE 754 + QDS
                case IEC104Constants.M_IT_NA_1:    // BCR
                case IEC104Constants.C_SE_NC_1:    // IEEE 754 + QOS
                    return 5;

                // 7 byte elements
                case IEC104Constants.C_CS_NA_1:    // CP56Time2a
                    return 7;

                default:
                    return 1; // Default size
            }
        }

        /// <summary>
        /// Kiểm tra TypeID có được hỗ trợ không
        /// </summary>
        /// <param name="typeId">Type ID</param>
        /// <returns>True nếu được hỗ trợ</returns>
        public static bool IsValidTypeID(byte typeId)
        {
            // Danh sách TypeID được hỗ trợ
            var supportedTypes = new byte[]
            {
                // Monitoring types
                IEC104Constants.M_SP_NA_1, IEC104Constants.M_DP_NA_1, IEC104Constants.M_ST_NA_1,
                IEC104Constants.M_BO_NA_1, IEC104Constants.M_ME_NA_1, IEC104Constants.M_ME_NB_1,
                IEC104Constants.M_ME_NC_1, IEC104Constants.M_IT_NA_1,
                
                // Control types
                IEC104Constants.C_SC_NA_1, IEC104Constants.C_DC_NA_1, IEC104Constants.C_RC_NA_1,
                IEC104Constants.C_SE_NA_1, IEC104Constants.C_SE_NB_1, IEC104Constants.C_SE_NC_1,
                
                // System types
                IEC104Constants.C_IC_NA_1, IEC104Constants.C_CI_NA_1, IEC104Constants.C_RD_NA_1,
                IEC104Constants.C_CS_NA_1, IEC104Constants.C_RP_NA_1
            };

            return Array.IndexOf(supportedTypes, typeId) >= 0;
        }

        #endregion

        #region ADDRESS PARSING

        /// <summary>
        /// Parse địa chỉ IEC104 từ string
        /// Format: CA.IOA.TypeID hoặc CA.IOA.TypeID.ElementIndex
        /// </summary>
        /// <param name="addressString">String địa chỉ</param>
        /// <param name="description">Mô tả lỗi nếu có</param>
        /// <returns>IEC104Address object hoặc null nếu không hợp lệ</returns>
        public static IEC104Address Parse(string addressString, out string description)
        {
            description = "";

            if (string.IsNullOrWhiteSpace(addressString))
            {
                description = "Address string cannot be empty";
                return null;
            }

            var parts = addressString.Split('.');
            if (parts.Length < 3 || parts.Length > 4)
            {
                description = "Format: CA.IOA.TypeID hoặc CA.IOA.TypeID.ElementIndex";
                return null;
            }

            try
            {
                // Parse Common Address (1-65535)
                if (!ushort.TryParse(parts[0], out ushort commonAddress) || commonAddress == 0)
                {
                    description = "Common Address phải từ 1-65535";
                    return null;
                }

                // Parse Information Object Address (1-16777215)
                if (!uint.TryParse(parts[1], out uint ioa) || ioa == 0 || ioa > 16777215)
                {
                    description = "Information Object Address phải từ 1-16777215";
                    return null;
                }

                // Parse Type ID (1-127)
                if (!byte.TryParse(parts[2], out byte typeId) || typeId == 0 || typeId > 127)
                {
                    description = "Type ID phải từ 1-127";
                    return null;
                }

                // Parse Element Index nếu có (0-255)
                int elementIndex = 0;
                if (parts.Length == 4)
                {
                    if (!int.TryParse(parts[3], out elementIndex) || elementIndex < 0 || elementIndex > 255)
                    {
                        description = "Element Index phải từ 0-255";
                        return null;
                    }
                }

                // Tạo address object
                var address = new IEC104Address(commonAddress, ioa, typeId, elementIndex)
                {
                    IEC104DataType = (IEC104DataType)typeId
                };

                // Validate TypeID
                if (!IsValidTypeID(typeId))
                {
                    description = $"Type ID {typeId} không được hỗ trợ";
                    return null;
                }

                description = GetTypeDescription(typeId);
                return address;
            }
            catch
            {
                description = "Lỗi khi parse address string";
                return null;
            }
        }

        /// <summary>
        /// Lấy mô tả cho TypeID
        /// </summary>
        /// <param name="typeId">Type ID</param>
        /// <returns>Mô tả TypeID</returns>
        public static string GetTypeDescription(byte typeId)
        {
            switch (typeId)
            {
                case IEC104Constants.M_SP_NA_1:
                    return "Single-point information (Read Only) - Thông tin một điểm";
                case IEC104Constants.M_DP_NA_1:
                    return "Double-point information (Read Only) - Thông tin hai điểm";
                case IEC104Constants.M_ST_NA_1:
                    return "Step position information (Read Only) - Thông tin vị trí bước";
                case IEC104Constants.M_BO_NA_1:
                    return "Bitstring of 32 bit (Read Only) - Chuỗi bit 32 bit";
                case IEC104Constants.M_ME_NA_1:
                    return "Measured value, normalized (Read Only) - Giá trị đo chuẩn hóa";
                case IEC104Constants.M_ME_NB_1:
                    return "Measured value, scaled (Read Only) - Giá trị đo có tỷ lệ";
                case IEC104Constants.M_ME_NC_1:
                    return "Measured value, floating point (Read Only) - Giá trị đo dấu phẩy động";
                case IEC104Constants.M_IT_NA_1:
                    return "Integrated totals (Read Only) - Tổng tích lũy";
                case IEC104Constants.C_SC_NA_1:
                    return "Single command (Read/Write) - Lệnh đơn";
                case IEC104Constants.C_DC_NA_1:
                    return "Double command (Read/Write) - Lệnh kép";
                case IEC104Constants.C_RC_NA_1:
                    return "Regulating step command (Read/Write) - Lệnh điều chỉnh bước";
                case IEC104Constants.C_SE_NA_1:
                    return "Set-point command, normalized (Read/Write) - Lệnh đặt điểm chuẩn hóa";
                case IEC104Constants.C_SE_NB_1:
                    return "Set-point command, scaled (Read/Write) - Lệnh đặt điểm có tỷ lệ";
                case IEC104Constants.C_SE_NC_1:
                    return "Set-point command, floating point (Read/Write) - Lệnh đặt điểm dấu phẩy động";
                case IEC104Constants.C_IC_NA_1:
                    return "Interrogation command (System) - Lệnh tra vấn";
                case IEC104Constants.C_CI_NA_1:
                    return "Counter interrogation command (System) - Lệnh tra vấn bộ đếm";
                case IEC104Constants.C_CS_NA_1:
                    return "Clock synchronization command (System) - Lệnh đồng bộ đồng hồ";
                case IEC104Constants.C_RD_NA_1:
                    return "Read command (System) - Lệnh đọc";
                case IEC104Constants.C_RP_NA_1:
                    return "Reset process command (System) - Lệnh reset tiến trình";
                default:
                    return $"Type ID {typeId} - Không xác định";
            }
        }

        #endregion

        #region VALIDATION METHODS

        /// <summary>
        /// Validate Common Address
        /// </summary>
        /// <param name="commonAddress">Common Address</param>
        /// <param name="errorMessage">Error message</param>
        /// <returns>True nếu hợp lệ</returns>
        public static bool ValidateCommonAddress(ushort commonAddress, out string errorMessage)
        {
            errorMessage = "";
            if (commonAddress == 0 || commonAddress > 65535)
            {
                errorMessage = "Common Address phải từ 1-65535";
                return false;
            }
            return true;
        }

        /// <summary>
        /// Validate Information Object Address
        /// </summary>
        /// <param name="ioa">IOA</param>
        /// <param name="errorMessage">Error message</param>
        /// <returns>True nếu hợp lệ</returns>
        public static bool ValidateIOA(uint ioa, out string errorMessage)
        {
            errorMessage = "";
            if (ioa == 0 || ioa > 16777215)
            {
                errorMessage = "Information Object Address phải từ 1-16777215";
                return false;
            }
            return true;
        }

        /// <summary>
        /// Validate Type ID
        /// </summary>
        /// <param name="typeId">Type ID</param>
        /// <param name="errorMessage">Error message</param>
        /// <returns>True nếu hợp lệ</returns>
        public static bool ValidateTypeID(byte typeId, out string errorMessage)
        {
            errorMessage = "";
            if (typeId == 0 || typeId > 127)
            {
                errorMessage = "Type ID phải từ 1-127";
                return false;
            }
            if (!IsValidTypeID(typeId))
            {
                errorMessage = $"Type ID {typeId} không được hỗ trợ";
                return false;
            }
            return true;
        }

        #endregion

        #region OVERRIDE METHODS

        /// <summary>
        /// Override ToString cho IEC104 format
        /// </summary>
        /// <returns>String representation</returns>
        public override string ToString()
        {
            if (ElementIndex > 0)
                return $"{CommonAddress}.{InformationObjectAddress}.{TypeID}.{ElementIndex}";
            else
                return $"{CommonAddress}.{InformationObjectAddress}.{TypeID}";
        }

        /// <summary>
        /// Override Equals
        /// </summary>
        /// <param name="obj">Object to compare</param>
        /// <returns>True if equal</returns>
        public override bool Equals(object obj)
        {
            if (obj is IEC104Address other)
            {
                return CommonAddress == other.CommonAddress &&
                       InformationObjectAddress == other.InformationObjectAddress &&
                       TypeID == other.TypeID &&
                       ElementIndex == other.ElementIndex;
            }
            return false;
        }

        /// <summary>
        /// Override GetHashCode
        /// </summary>
        /// <returns>Hash code</returns>
        public override int GetHashCode()
        {
            return HashCode.Combine(CommonAddress, InformationObjectAddress, TypeID, ElementIndex);
        }

        /// <summary>
        /// Copy address to new instance
        /// </summary>
        /// <returns>New address instance</returns>
        public IEC104Address Clone()
        {
            return new IEC104Address(CommonAddress, InformationObjectAddress, TypeID, ElementIndex)
            {
                IEC104DataType = IEC104DataType
            };
        }

        #endregion
    }
}