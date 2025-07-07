using System;
using System.Collections.Generic;

namespace IEC104
{
    /// <summary>
    /// IEC104 Address - FIXED VERSION - hoàn toàn độc lập, không extend từ ModbusTCP
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

        /// <summary>Type Identification - FIXED property name to avoid confusion</summary>
        public byte TypeIdentification
        {
            get => TypeID;
            set => TypeID = value;
        }

        /// <summary>Element Index - cho sequence of elements (0-255)</summary>
        public int ElementIndex { get; set; } = 0;

        /// <summary>IEC104 Data Type enum</summary>
        public IEC104DataType DataType { get; set; }

        /// <summary>Device Name - ADDED for device mapping</summary>
        public string DeviceName { get; set; }

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

        /// <summary>Kiểm tra có phải command type không - ADDED property</summary>
        public bool IsCommandType
        {
            get => IsControlType(TypeID) || IsSystemType(TypeID);
        }

        /// <summary>Kích thước element theo bytes</summary>
        public int ElementSize
        {
            get => GetIEC104ElementSize(TypeID);
        }

        /// <summary>Full address string - ADDED for compatibility</summary>
        public string FullAddress
        {
            get => ToString();
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
        /// Constructor từ address string - ADDED for compatibility
        /// </summary>
        /// <param name="addressString">Address string</param>
        public IEC104Address(string addressString)
        {
            ParseAddress(addressString);
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
            DataType = (IEC104DataType)typeId;
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

        #region PARSING METHODS - ADDED

        /// <summary>
        /// Parse địa chỉ từ string - ADDED method for compatibility
        /// </summary>
        /// <param name="addressString">Address string</param>
        public void ParseAddress(string addressString)
        {
            var address = Parse(addressString, out string description);
            if (address != null)
            {
                CommonAddress = address.CommonAddress;
                InformationObjectAddress = address.InformationObjectAddress;
                TypeID = address.TypeID;
                ElementIndex = address.ElementIndex;
                DataType = address.DataType;
                DeviceName = address.DeviceName;
            }
        }

        /// <summary>
        /// Kiểm tra address có hợp lệ không - ADDED method for compatibility
        /// </summary>
        /// <returns>True nếu hợp lệ</returns>
        public bool IsValid()
        {
            return IsValid;
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
                    DataType = (IEC104DataType)typeId
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

        #region UTILITY METHODS - ADDED

        /// <summary>
        /// Clone address - ADDED method for compatibility
        /// </summary>
        /// <returns>Cloned address</returns>
        public object Clone()
        {
            return new IEC104Address(CommonAddress, InformationObjectAddress, TypeID, ElementIndex)
            {
                DataType = DataType,
                DeviceName = DeviceName
            };
        }

        /// <summary>
        /// Compare với address khác - ADDED method for compatibility
        /// </summary>
        /// <param name="other">Address khác</param>
        /// <returns>Kết quả so sánh</returns>
        public int CompareTo(IEC104Address other)
        {
            if (other == null) return 1;

            int result = CommonAddress.CompareTo(other.CommonAddress);
            if (result != 0) return result;

            result = InformationObjectAddress.CompareTo(other.InformationObjectAddress);
            if (result != 0) return result;

            result = TypeID.CompareTo(other.TypeID);
            if (result != 0) return result;

            return ElementIndex.CompareTo(other.ElementIndex);
        }

        /// <summary>
        /// Lấy hash value cho address
        /// </summary>
        /// <returns>Hash value</returns>
        public string GetHashValue()
        {
            return $"{CommonAddress}.{InformationObjectAddress}.{TypeID}.{ElementIndex}";
        }

        /// <summary>
        /// Kiểm tra address có khớp với pattern không
        /// </summary>
        /// <param name="pattern">Pattern để kiểm tra</param>
        /// <returns>True nếu khớp</returns>
        public bool MatchesPattern(string pattern)
        {
            if (string.IsNullOrEmpty(pattern))
                return false;

            // Simple wildcard matching
            if (pattern == "*")
                return true;

            var parts = pattern.Split('.');
            var addressParts = new string[]
            {
                CommonAddress.ToString(),
                InformationObjectAddress.ToString(),
                TypeID.ToString(),
                ElementIndex.ToString()
            };

            for (int i = 0; i < Math.Min(parts.Length, addressParts.Length); i++)
            {
                if (parts[i] != "*" && parts[i] != addressParts[i])
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
        public IEC104Address Copy()
        {
            return new IEC104Address(CommonAddress, InformationObjectAddress, TypeID, ElementIndex)
            {
                DataType = DataType,
                DeviceName = DeviceName
            };
        }

        #endregion

        #region CONVERSION METHODS - ADDED

        /// <summary>
        /// Convert sang dictionary cho serialization
        /// </summary>
        /// <returns>Dictionary representation</returns>
        public Dictionary<string, object> ToDictionary()
        {
            return new Dictionary<string, object>
            {
                ["CommonAddress"] = CommonAddress,
                ["InformationObjectAddress"] = InformationObjectAddress,
                ["TypeID"] = TypeID,
                ["ElementIndex"] = ElementIndex,
                ["DataType"] = DataType.ToString(),
                ["DeviceName"] = DeviceName ?? "",
                ["AccessRight"] = AccessRight.ToString(),
                ["IsDiscrete"] = IsDiscrete,
                ["IsCommandType"] = IsCommandType,
                ["ElementSize"] = ElementSize,
                ["IsValid"] = IsValid
            };
        }

        /// <summary>
        /// Create từ dictionary
        /// </summary>
        /// <param name="dict">Dictionary data</param>
        /// <returns>IEC104Address instance</returns>
        public static IEC104Address FromDictionary(Dictionary<string, object> dict)
        {
            if (dict == null)
                return null;

            try
            {
                var address = new IEC104Address();

                if (dict.TryGetValue("CommonAddress", out object ca))
                    address.CommonAddress = Convert.ToUInt16(ca);

                if (dict.TryGetValue("InformationObjectAddress", out object ioa))
                    address.InformationObjectAddress = Convert.ToUInt32(ioa);

                if (dict.TryGetValue("TypeID", out object typeId))
                    address.TypeID = Convert.ToByte(typeId);

                if (dict.TryGetValue("ElementIndex", out object elementIndex))
                    address.ElementIndex = Convert.ToInt32(elementIndex);

                if (dict.TryGetValue("DataType", out object dataType))
                    Enum.TryParse(dataType.ToString(), out IEC104DataType dt);

                if (dict.TryGetValue("DeviceName", out object deviceName))
                    address.DeviceName = deviceName?.ToString();

                return address.IsValid ? address : null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Convert sang JSON string
        /// </summary>
        /// <returns>JSON representation</returns>
        public string ToJson()
        {
            var dict = ToDictionary();
            return System.Text.Json.JsonSerializer.Serialize(dict);
        }

        /// <summary>
        /// Create từ JSON string
        /// </summary>
        /// <param name="json">JSON string</param>
        /// <returns>IEC104Address instance</returns>
        public static IEC104Address FromJson(string json)
        {
            try
            {
                var dict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(json);
                return FromDictionary(dict);
            }
            catch
            {
                return null;
            }
        }

        #endregion

        #region FACTORY METHODS - ADDED

        /// <summary>
        /// Tạo address cho Single Point
        /// </summary>
        /// <param name="commonAddress">Common Address</param>
        /// <param name="ioa">Information Object Address</param>
        /// <returns>IEC104Address for Single Point</returns>
        public static IEC104Address CreateSinglePoint(ushort commonAddress, uint ioa)
        {
            return new IEC104Address(commonAddress, ioa, IEC104Constants.M_SP_NA_1)
            {
                DataType = IEC104DataType.SinglePoint
            };
        }

        /// <summary>
        /// Tạo address cho Double Point
        /// </summary>
        /// <param name="commonAddress">Common Address</param>
        /// <param name="ioa">Information Object Address</param>
        /// <returns>IEC104Address for Double Point</returns>
        public static IEC104Address CreateDoublePoint(ushort commonAddress, uint ioa)
        {
            return new IEC104Address(commonAddress, ioa, IEC104Constants.M_DP_NA_1)
            {
                DataType = IEC104DataType.DoublePoint
            };
        }

        /// <summary>
        /// Tạo address cho Float Value
        /// </summary>
        /// <param name="commonAddress">Common Address</param>
        /// <param name="ioa">Information Object Address</param>
        /// <returns>IEC104Address for Float Value</returns>
        public static IEC104Address CreateFloatValue(ushort commonAddress, uint ioa)
        {
            return new IEC104Address(commonAddress, ioa, IEC104Constants.M_ME_NC_1)
            {
                DataType = IEC104DataType.FloatValue
            };
        }

        /// <summary>
        /// Tạo address cho Single Command
        /// </summary>
        /// <param name="commonAddress">Common Address</param>
        /// <param name="ioa">Information Object Address</param>
        /// <returns>IEC104Address for Single Command</returns>
        public static IEC104Address CreateSingleCommand(ushort commonAddress, uint ioa)
        {
            return new IEC104Address(commonAddress, ioa, IEC104Constants.C_SC_NA_1)
            {
                DataType = IEC104DataType.SingleCommand
            };
        }

        /// <summary>
        /// Tạo address cho Double Command
        /// </summary>
        /// <param name="commonAddress">Common Address</param>
        /// <param name="ioa">Information Object Address</param>
        /// <returns>IEC104Address for Double Command</returns>
        public static IEC104Address CreateDoubleCommand(ushort commonAddress, uint ioa)
        {
            return new IEC104Address(commonAddress, ioa, IEC104Constants.C_DC_NA_1)
            {
                DataType = IEC104DataType.DoubleCommand
            };
        }

        /// <summary>
        /// Tạo address cho Float Setpoint
        /// </summary>
        /// <param name="commonAddress">Common Address</param>
        /// <param name="ioa">Information Object Address</param>
        /// <returns>IEC104Address for Float Setpoint</returns>
        public static IEC104Address CreateFloatSetpoint(ushort commonAddress, uint ioa)
        {
            return new IEC104Address(commonAddress, ioa, IEC104Constants.C_SE_NC_1)
            {
                DataType = IEC104DataType.FloatSetpoint
            };
        }

        /// <summary>
        /// Tạo range của addresses
        /// </summary>
        /// <param name="commonAddress">Common Address</param>
        /// <param name="startIOA">Start IOA</param>
        /// <param name="endIOA">End IOA</param>
        /// <param name="typeId">Type ID</param>
        /// <returns>List of IEC104Address</returns>
        public static List<IEC104Address> CreateRange(ushort commonAddress, uint startIOA, uint endIOA, byte typeId)
        {
            var addresses = new List<IEC104Address>();

            if (startIOA > endIOA || !IsValidTypeID(typeId))
                return addresses;

            for (uint ioa = startIOA; ioa <= endIOA; ioa++)
            {
                var address = new IEC104Address(commonAddress, ioa, typeId)
                {
                    DataType = (IEC104DataType)typeId
                };
                addresses.Add(address);
            }

            return addresses;
        }

        #endregion
    }

    /// <summary>
    /// Access Right enum - FIXED to match original
    /// </summary>
    public enum AccessRight
    {
        /// <summary>Chỉ đọc</summary>
        ReadOnly,
        /// <summary>Đọc và ghi</summary>
        ReadWrite
    }

    /// <summary>
    /// IEC104 Address Collection - ADDED utility class
    /// </summary>
    public class IEC104AddressCollection : List<IEC104Address>
    {
        /// <summary>
        /// Find address by IOA
        /// </summary>
        /// <param name="ioa">Information Object Address</param>
        /// <returns>Found address or null</returns>
        public IEC104Address FindByIOA(uint ioa)
        {
            return Find(addr => addr.InformationObjectAddress == ioa);
        }

        /// <summary>
        /// Find addresses by Common Address
        /// </summary>
        /// <param name="commonAddress">Common Address</param>
        /// <returns>List of matching addresses</returns>
        public List<IEC104Address> FindByCommonAddress(ushort commonAddress)
        {
            return FindAll(addr => addr.CommonAddress == commonAddress);
        }

        /// <summary>
        /// Find addresses by Type ID
        /// </summary>
        /// <param name="typeId">Type ID</param>
        /// <returns>List of matching addresses</returns>
        public List<IEC104Address> FindByTypeID(byte typeId)
        {
            return FindAll(addr => addr.TypeID == typeId);
        }

        /// <summary>
        /// Find addresses by Device Name
        /// </summary>
        /// <param name="deviceName">Device Name</param>
        /// <returns>List of matching addresses</returns>
        public List<IEC104Address> FindByDeviceName(string deviceName)
        {
            return FindAll(addr => addr.DeviceName == deviceName);
        }

        /// <summary>
        /// Get all monitoring addresses (read-only)
        /// </summary>
        /// <returns>List of monitoring addresses</returns>
        public List<IEC104Address> GetMonitoringAddresses()
        {
            return FindAll(addr => addr.AccessRight == AccessRight.ReadOnly);
        }

        /// <summary>
        /// Get all command addresses (read-write)
        /// </summary>
        /// <returns>List of command addresses</returns>
        public List<IEC104Address> GetCommandAddresses()
        {
            return FindAll(addr => addr.AccessRight == AccessRight.ReadWrite);
        }

        /// <summary>
        /// Validate all addresses
        /// </summary>
        /// <returns>List of invalid addresses</returns>
        public List<IEC104Address> ValidateAll()
        {
            return FindAll(addr => !addr.IsValid);
        }

        /// <summary>
        /// Group by Common Address
        /// </summary>
        /// <returns>Dictionary grouped by Common Address</returns>
        public Dictionary<ushort, List<IEC104Address>> GroupByCommonAddress()
        {
            var groups = new Dictionary<ushort, List<IEC104Address>>();

            foreach (var address in this)
            {
                if (!groups.ContainsKey(address.CommonAddress))
                    groups[address.CommonAddress] = new List<IEC104Address>();

                groups[address.CommonAddress].Add(address);
            }

            return groups;
        }

        /// <summary>
        /// Export to CSV format
        /// </summary>
        /// <returns>CSV string</returns>
        public string ToCsv()
        {
            var csv = "CommonAddress,IOA,TypeID,ElementIndex,DataType,DeviceName,AccessRight,IsValid\n";

            foreach (var address in this)
            {
                csv += $"{address.CommonAddress},{address.InformationObjectAddress},{address.TypeID}," +
                       $"{address.ElementIndex},{address.DataType},{address.DeviceName ?? ""}," +
                       $"{address.AccessRight},{address.IsValid}\n";
            }

            return csv;
        }
    }
}