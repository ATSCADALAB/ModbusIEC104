using System;
using System.Collections.Generic;

namespace IEC104
{
    /// <summary>
    /// IEC104 Data Types - hoàn toàn độc lập cho IEC104
    /// Mapping với TypeID trong IEC104 standard
    /// </summary>
    public enum IEC104DataType : byte
    {
        #region MONITORING DIRECTION (READ ONLY)

        /// <summary>Single-point information M_SP_NA_1</summary>
        SinglePoint = IEC104Constants.M_SP_NA_1,

        /// <summary>Double-point information M_DP_NA_1</summary>
        DoublePoint = IEC104Constants.M_DP_NA_1,

        /// <summary>Step position information M_ST_NA_1</summary>
        StepPosition = IEC104Constants.M_ST_NA_1,

        /// <summary>Bitstring of 32 bit M_BO_NA_1</summary>
        Bitstring32 = IEC104Constants.M_BO_NA_1,

        /// <summary>Measured value, normalized value M_ME_NA_1</summary>
        NormalizedValue = IEC104Constants.M_ME_NA_1,

        /// <summary>Measured value, scaled value M_ME_NB_1</summary>
        ScaledValue = IEC104Constants.M_ME_NB_1,

        /// <summary>Measured value, short floating point M_ME_NC_1</summary>
        FloatValue = IEC104Constants.M_ME_NC_1,

        /// <summary>Integrated totals M_IT_NA_1</summary>
        IntegratedTotals = IEC104Constants.M_IT_NA_1,

        #endregion

        #region CONTROL DIRECTION (READ/WRITE)

        /// <summary>Single command C_SC_NA_1</summary>
        SingleCommand = IEC104Constants.C_SC_NA_1,

        /// <summary>Double command C_DC_NA_1</summary>
        DoubleCommand = IEC104Constants.C_DC_NA_1,

        /// <summary>Regulating step command C_RC_NA_1</summary>
        StepCommand = IEC104Constants.C_RC_NA_1,

        /// <summary>Set-point command, normalized value C_SE_NA_1</summary>
        NormalizedSetpoint = IEC104Constants.C_SE_NA_1,

        /// <summary>Set-point command, scaled value C_SE_NB_1</summary>
        ScaledSetpoint = IEC104Constants.C_SE_NB_1,

        /// <summary>Set-point command, short floating point C_SE_NC_1</summary>
        FloatSetpoint = IEC104Constants.C_SE_NC_1,

        #endregion

        #region SYSTEM COMMANDS

        /// <summary>Interrogation command C_IC_NA_1</summary>
        InterrogationCommand = IEC104Constants.C_IC_NA_1,

        /// <summary>Counter interrogation command C_CI_NA_1</summary>
        CounterInterrogationCommand = IEC104Constants.C_CI_NA_1,

        /// <summary>Read command C_RD_NA_1</summary>
        ReadCommand = IEC104Constants.C_RD_NA_1,

        /// <summary>Clock synchronization command C_CS_NA_1</summary>
        ClockSynchronizationCommand = IEC104Constants.C_CS_NA_1,

        /// <summary>Reset process command C_RP_NA_1</summary>
        ResetProcessCommand = IEC104Constants.C_RP_NA_1,

        #endregion
    }

    /// <summary>
    /// Access Right cho IEC104 - hoàn toàn độc lập
    /// </summary>
    public enum IEC104AccessRight
    {
        /// <summary>Chỉ đọc</summary>
        ReadOnly,
        /// <summary>Đọc và ghi</summary>
        ReadWrite
    }

    /// <summary>
    /// Quality Descriptor structure cho IEC104
    /// </summary>
    public struct IEC104QualityDescriptor
    {
        #region PROPERTIES

        /// <summary>Overflow bit (bit 0)</summary>
        public bool Overflow { get; set; }

        /// <summary>Blocked bit (bit 4)</summary>
        public bool Blocked { get; set; }

        /// <summary>Substituted bit (bit 5)</summary>
        public bool Substituted { get; set; }

        /// <summary>Not topical bit (bit 6)</summary>
        public bool NotTopical { get; set; }

        /// <summary>Invalid bit (bit 7)</summary>
        public bool Invalid { get; set; }

        /// <summary>Kiểm tra dữ liệu có tốt không</summary>
        public bool IsGood => !Invalid && !NotTopical;

        /// <summary>Raw quality byte</summary>
        public byte RawValue { get; set; }

        #endregion

        #region CONSTRUCTORS

        /// <summary>
        /// Constructor từ byte value
        /// </summary>
        /// <param name="qualityByte">Quality byte</param>
        public IEC104QualityDescriptor(byte qualityByte)
        {
            RawValue = qualityByte;
            Overflow = (qualityByte & IEC104Constants.QDS_OVERFLOW) != 0;
            Blocked = (qualityByte & IEC104Constants.QDS_BLOCKED) != 0;
            Substituted = (qualityByte & IEC104Constants.QDS_SUBSTITUTED) != 0;
            NotTopical = (qualityByte & IEC104Constants.QDS_NOT_TOPICAL) != 0;
            Invalid = (qualityByte & IEC104Constants.QDS_INVALID) != 0;
        }

        #endregion

        #region METHODS

        /// <summary>
        /// Chuyển thành byte value
        /// </summary>
        /// <returns>Quality byte</returns>
        public byte ToByte()
        {
            byte result = 0;
            if (Overflow) result |= IEC104Constants.QDS_OVERFLOW;
            if (Blocked) result |= IEC104Constants.QDS_BLOCKED;
            if (Substituted) result |= IEC104Constants.QDS_SUBSTITUTED;
            if (NotTopical) result |= IEC104Constants.QDS_NOT_TOPICAL;
            if (Invalid) result |= IEC104Constants.QDS_INVALID;
            return result;
        }

        /// <summary>
        /// Override ToString
        /// </summary>
        /// <returns>String representation</returns>
        public override string ToString()
        {
            var flags = new List<string>();
            if (Invalid) flags.Add("INVALID");
            if (NotTopical) flags.Add("NOT_TOPICAL");
            if (Substituted) flags.Add("SUBSTITUTED");
            if (Blocked) flags.Add("BLOCKED");
            if (Overflow) flags.Add("OVERFLOW");

            return flags.Count > 0 ? string.Join("|", flags) : "GOOD";
        }

        #endregion
    }

    /// <summary>
    /// Double Point Values cho IEC104
    /// </summary>
    public enum DoublePointValue : byte
    {
        /// <summary>Indeterminate or intermediate state</summary>
        Indeterminate = IEC104Constants.DPI_INDETERMINATE,

        /// <summary>OFF</summary>
        OFF = IEC104Constants.DPI_OFF,

        /// <summary>ON</summary>
        ON = IEC104Constants.DPI_ON,

        /// <summary>Indeterminate state</summary>
        Indeterminate2 = IEC104Constants.DPI_INDETERMINATE_2
    }

    /// <summary>
    /// Step Command Values cho IEC104
    /// </summary>
    public enum StepCommandValue : byte
    {
        /// <summary>Not permitted</summary>
        NotPermitted = IEC104Constants.RCS_NOT_PERMITTED,

        /// <summary>LOWER</summary>
        Lower = IEC104Constants.RCS_LOWER,

        /// <summary>HIGHER</summary>
        Higher = IEC104Constants.RCS_HIGHER,

        /// <summary>Not permitted</summary>
        NotPermitted2 = IEC104Constants.RCS_NOT_PERMITTED_2
    }

    /// <summary>
    /// Extension methods cho IEC104DataType
    /// </summary>
    public static class IEC104DataTypeExtensions
    {
        /// <summary>
        /// Lấy display name cho data type
        /// </summary>
        /// <param name="dataType">IEC104 data type</param>
        /// <returns>Display name</returns>
        public static string ToDisplayName(this IEC104DataType dataType)
        {
            switch (dataType)
            {
                case IEC104DataType.SinglePoint:
                    return "Single Point";
                case IEC104DataType.DoublePoint:
                    return "Double Point";
                case IEC104DataType.StepPosition:
                    return "Step Position";
                case IEC104DataType.Bitstring32:
                    return "Bitstring 32";
                case IEC104DataType.NormalizedValue:
                    return "Normalized Value";
                case IEC104DataType.ScaledValue:
                    return "Scaled Value";
                case IEC104DataType.FloatValue:
                    return "Float Value";
                case IEC104DataType.IntegratedTotals:
                    return "Integrated Totals";
                case IEC104DataType.SingleCommand:
                    return "Single Command";
                case IEC104DataType.DoubleCommand:
                    return "Double Command";
                case IEC104DataType.StepCommand:
                    return "Step Command";
                case IEC104DataType.NormalizedSetpoint:
                    return "Normalized Setpoint";
                case IEC104DataType.ScaledSetpoint:
                    return "Scaled Setpoint";
                case IEC104DataType.FloatSetpoint:
                    return "Float Setpoint";
                case IEC104DataType.InterrogationCommand:
                    return "Interrogation Command";
                case IEC104DataType.CounterInterrogationCommand:
                    return "Counter Interrogation";
                case IEC104DataType.ReadCommand:
                    return "Read Command";
                case IEC104DataType.ClockSynchronizationCommand:
                    return "Clock Synchronization";
                case IEC104DataType.ResetProcessCommand:
                    return "Reset Process";
                default:
                    return dataType.ToString();
            }
        }

        /// <summary>
        /// Parse string thành IEC104DataType
        /// </summary>
        /// <param name="dataTypeString">String data type</param>
        /// <returns>IEC104DataType</returns>
        public static IEC104DataType GetIEC104DataType(this string dataTypeString)
        {
            if (string.IsNullOrWhiteSpace(dataTypeString))
                return IEC104DataType.SinglePoint;

            var normalized = dataTypeString.ToUpper().Replace(" ", "").Replace("_", "");

            switch (normalized)
            {
                case "SINGLEPOINT":
                case "SP":
                    return IEC104DataType.SinglePoint;

                case "DOUBLEPOINT":
                case "DP":
                    return IEC104DataType.DoublePoint;

                case "STEPPOSITION":
                case "ST":
                    return IEC104DataType.StepPosition;

                case "BITSTRING32":
                case "BO":
                    return IEC104DataType.Bitstring32;

                case "NORMALIZEDVALUE":
                case "NORMALIZED":
                case "NVA":
                    return IEC104DataType.NormalizedValue;

                case "SCALEDVALUE":
                case "SCALED":
                case "SVA":
                    return IEC104DataType.ScaledValue;

                case "FLOATVALUE":
                case "FLOAT":
                    return IEC104DataType.FloatValue;

                case "INTEGRATEDTOTALS":
                case "INTEGRATED":
                case "IT":
                    return IEC104DataType.IntegratedTotals;

                case "SINGLECOMMAND":
                case "SC":
                    return IEC104DataType.SingleCommand;

                case "DOUBLECOMMAND":
                case "DC":
                    return IEC104DataType.DoubleCommand;

                case "STEPCOMMAND":
                case "RC":
                    return IEC104DataType.StepCommand;

                case "NORMALIZEDSETPOINT":
                case "NORMALIZEDSET":
                    return IEC104DataType.NormalizedSetpoint;

                case "SCALEDSETPOINT":
                case "SCALEDSET":
                    return IEC104DataType.ScaledSetpoint;

                case "FLOATSETPOINT":
                case "FLOATSET":
                    return IEC104DataType.FloatSetpoint;

                case "INTERROGATION":
                case "IC":
                    return IEC104DataType.InterrogationCommand;

                case "COUNTERINTERROGATION":
                case "CI":
                    return IEC104DataType.CounterInterrogationCommand;

                case "READ":
                case "RD":
                    return IEC104DataType.ReadCommand;

                case "CLOCKSYNCHRONIZATION":
                case "CLOCK":
                case "CS":
                    return IEC104DataType.ClockSynchronizationCommand;

                case "RESETPROCESS":
                case "RESET":
                case "RP":
                    return IEC104DataType.ResetProcessCommand;

                default:
                    return IEC104DataType.SinglePoint;
            }
        }

        /// <summary>
        /// Kiểm tra data type có phải là monitoring type không
        /// </summary>
        /// <param name="dataType">IEC104 data type</param>
        /// <returns>True nếu là monitoring type</returns>
        public static bool IsMonitoringType(this IEC104DataType dataType)
        {
            return IEC104Address.IsMonitoringType((byte)dataType);
        }

        /// <summary>
        /// Kiểm tra data type có phải là control type không
        /// </summary>
        /// <param name="dataType">IEC104 data type</param>
        /// <returns>True nếu là control type</returns>
        public static bool IsControlType(this IEC104DataType dataType)
        {
            return IEC104Address.IsControlType((byte)dataType);
        }

        /// <summary>
        /// Kiểm tra data type có phải là system type không
        /// </summary>
        /// <param name="dataType">IEC104 data type</param>
        /// <returns>True nếu là system type</returns>
        public static bool IsSystemType(this IEC104DataType dataType)
        {
            return IEC104Address.IsSystemType((byte)dataType);
        }

        /// <summary>
        /// Lấy kích thước element cho data type
        /// </summary>
        /// <param name="dataType">IEC104 data type</param>
        /// <returns>Kích thước element (bytes)</returns>
        public static int GetElementSize(this IEC104DataType dataType)
        {
            return IEC104Address.GetIEC104ElementSize((byte)dataType);
        }

        /// <summary>
        /// Lấy access right cho data type
        /// </summary>
        /// <param name="dataType">IEC104 data type</param>
        /// <returns>Access right</returns>
        public static IEC104AccessRight GetAccessRight(this IEC104DataType dataType)
        {
            if (dataType.IsMonitoringType())
                return IEC104AccessRight.ReadOnly;
            else if (dataType.IsControlType() || dataType.IsSystemType())
                return IEC104AccessRight.ReadWrite;
            else
                return IEC104AccessRight.ReadOnly;
        }

        /// <summary>
        /// Kiểm tra có phải discrete data type không
        /// </summary>
        /// <param name="dataType">IEC104 data type</param>
        /// <returns>True nếu là discrete</returns>
        public static bool IsDiscrete(this IEC104DataType dataType)
        {
            return dataType == IEC104DataType.SinglePoint ||
                   dataType == IEC104DataType.DoublePoint ||
                   dataType == IEC104DataType.SingleCommand ||
                   dataType == IEC104DataType.DoubleCommand;
        }

        /// <summary>
        /// Lấy TypeID byte cho data type
        /// </summary>
        /// <param name="dataType">IEC104 data type</param>
        /// <returns>TypeID byte</returns>
        public static byte GetTypeID(this IEC104DataType dataType)
        {
            return (byte)dataType;
        }
    }

    /// <summary>
    /// Extension methods cho IEC104AccessRight
    /// </summary>
    public static class IEC104AccessRightExtensions
    {
        /// <summary>
        /// Lấy display name cho access right
        /// </summary>
        /// <param name="accessRight">Access right</param>
        /// <returns>Display name</returns>
        public static string ToDisplayName(this IEC104AccessRight accessRight)
        {
            switch (accessRight)
            {
                case IEC104AccessRight.ReadOnly:
                    return "ReadOnly";
                case IEC104AccessRight.ReadWrite:
                    return "ReadWrite";
                default:
                    return accessRight.ToString();
            }
        }

        /// <summary>
        /// Parse string thành IEC104AccessRight
        /// </summary>
        /// <param name="accessRightString">String access right</param>
        /// <returns>IEC104AccessRight</returns>
        public static IEC104AccessRight GetIEC104AccessRight(this string accessRightString)
        {
            if (string.IsNullOrWhiteSpace(accessRightString))
                return IEC104AccessRight.ReadOnly;

            var normalized = accessRightString.ToUpper().Replace(" ", "");

            switch (normalized)
            {
                case "READWRITE":
                case "RW":
                case "WRITE":
                    return IEC104AccessRight.ReadWrite;

                case "READONLY":
                case "RO":
                case "READ":
                default:
                    return IEC104AccessRight.ReadOnly;
            }
        }
    }

    /// <summary>
    /// Utility class cho các operations chung của IEC104 data types
    /// </summary>
    public static class IEC104DataUtilities
    {
        /// <summary>
        /// Tạo Quality Descriptor mặc định (GOOD)
        /// </summary>
        /// <returns>Good quality descriptor</returns>
        public static IEC104QualityDescriptor CreateGoodQuality()
        {
            return new IEC104QualityDescriptor(0);
        }

        /// <summary>
        /// Tạo Quality Descriptor với Invalid flag
        /// </summary>
        /// <returns>Invalid quality descriptor</returns>
        public static IEC104QualityDescriptor CreateInvalidQuality()
        {
            return new IEC104QualityDescriptor(IEC104Constants.QDS_INVALID);
        }

        /// <summary>
        /// Tạo Quality Descriptor với Not Topical flag
        /// </summary>
        /// <returns>Not topical quality descriptor</returns>
        public static IEC104QualityDescriptor CreateNotTopicalQuality()
        {
            return new IEC104QualityDescriptor(IEC104Constants.QDS_NOT_TOPICAL);
        }

        /// <summary>
        /// Validate value cho specific data type
        /// </summary>
        /// <param name="dataType">Data type</param>
        /// <param name="value">Value string</param>
        /// <param name="errorMessage">Error message nếu có</param>
        /// <returns>True nếu valid</returns>
        public static bool ValidateValue(IEC104DataType dataType, string value, out string errorMessage)
        {
            errorMessage = "";

            if (string.IsNullOrWhiteSpace(value))
            {
                errorMessage = "Value cannot be empty";
                return false;
            }

            try
            {
                switch (dataType)
                {
                    case IEC104DataType.SinglePoint:
                    case IEC104DataType.SingleCommand:
                        // Phải là 0 hoặc 1
                        if (!value.Equals("0") && !value.Equals("1") &&
                            !value.ToLower().Equals("true") && !value.ToLower().Equals("false"))
                        {
                            errorMessage = "Single Point/Command value must be 0, 1, true, or false";
                            return false;
                        }
                        break;

                    case IEC104DataType.DoublePoint:
                    case IEC104DataType.DoubleCommand:
                        // Phải là 0, 1, 2, hoặc 3
                        if (!byte.TryParse(value, out byte dpValue) || dpValue > 3)
                        {
                            errorMessage = "Double Point/Command value must be 0-3";
                            return false;
                        }
                        break;

                    case IEC104DataType.StepPosition:
                    case IEC104DataType.StepCommand:
                        // Phải là byte value
                        if (!byte.TryParse(value, out _))
                        {
                            errorMessage = "Step Position/Command value must be 0-255";
                            return false;
                        }
                        break;

                    case IEC104DataType.NormalizedValue:
                    case IEC104DataType.NormalizedSetpoint:
                        // Phải là short value (-32768 to 32767)
                        if (!short.TryParse(value, out _))
                        {
                            errorMessage = "Normalized value must be -32768 to 32767";
                            return false;
                        }
                        break;

                    case IEC104DataType.ScaledValue:
                    case IEC104DataType.ScaledSetpoint:
                        // Phải là short value
                        if (!short.TryParse(value, out _))
                        {
                            errorMessage = "Scaled value must be -32768 to 32767";
                            return false;
                        }
                        break;

                    case IEC104DataType.FloatValue:
                    case IEC104DataType.FloatSetpoint:
                        // Phải là float value
                        if (!float.TryParse(value, out float floatVal))
                        {
                            errorMessage = "Float value must be a valid floating point number";
                            return false;
                        }
                        if (float.IsNaN(floatVal) || float.IsInfinity(floatVal))
                        {
                            errorMessage = "Float value cannot be NaN or Infinity";
                            return false;
                        }
                        break;

                    case IEC104DataType.Bitstring32:
                        // Phải là uint value
                        if (!uint.TryParse(value, out _))
                        {
                            errorMessage = "Bitstring32 value must be 0 to 4294967295";
                            return false;
                        }
                        break;

                    case IEC104DataType.IntegratedTotals:
                        // Phải là uint value (counter value)
                        if (!uint.TryParse(value, out _))
                        {
                            errorMessage = "Integrated Totals value must be 0 to 4294967295";
                            return false;
                        }
                        break;

                    default:
                        // System commands có thể có format khác
                        break;
                }

                return true;
            }
            catch
            {
                errorMessage = $"Invalid value format for {dataType.ToDisplayName()}";
                return false;
            }
        }

        /// <summary>
        /// Lấy default value cho IEC104 data type
        /// </summary>
        /// <param name="dataType">IEC104 data type</param>
        /// <returns>Default value as string</returns>
        public static string GetDefaultValue(IEC104DataType dataType)
        {
            switch (dataType)
            {
                case IEC104DataType.SinglePoint:
                case IEC104DataType.SingleCommand:
                    return "0";

                case IEC104DataType.DoublePoint:
                case IEC104DataType.DoubleCommand:
                    return "0"; // Indeterminate

                case IEC104DataType.StepPosition:
                case IEC104DataType.StepCommand:
                    return "0";

                case IEC104DataType.NormalizedValue:
                case IEC104DataType.NormalizedSetpoint:
                case IEC104DataType.ScaledValue:
                case IEC104DataType.ScaledSetpoint:
                    return "0";

                case IEC104DataType.FloatValue:
                case IEC104DataType.FloatSetpoint:
                    return "0.0";

                case IEC104DataType.Bitstring32:
                case IEC104DataType.IntegratedTotals:
                    return "0";

                default:
                    return "0";
            }
        }

        /// <summary>
        /// Lấy range description cho data type
        /// </summary>
        /// <param name="dataType">IEC104 data type</param>
        /// <returns>Range description</returns>
        public static string GetValueRange(IEC104DataType dataType)
        {
            switch (dataType)
            {
                case IEC104DataType.SinglePoint:
                case IEC104DataType.SingleCommand:
                    return "0 (OFF) hoặc 1 (ON)";

                case IEC104DataType.DoublePoint:
                case IEC104DataType.DoubleCommand:
                    return "0 (Indeterminate), 1 (OFF), 2 (ON), 3 (Indeterminate)";

                case IEC104DataType.StepPosition:
                case IEC104DataType.StepCommand:
                    return "0 đến 255";

                case IEC104DataType.NormalizedValue:
                case IEC104DataType.NormalizedSetpoint:
                    return "-32768 đến +32767 (normalized)";

                case IEC104DataType.ScaledValue:
                case IEC104DataType.ScaledSetpoint:
                    return "-32768 đến +32767 (scaled)";

                case IEC104DataType.FloatValue:
                case IEC104DataType.FloatSetpoint:
                    return "IEEE 754 floating point";

                case IEC104DataType.Bitstring32:
                    return "0 đến 4294967295 (32-bit bitstring)";

                case IEC104DataType.IntegratedTotals:
                    return "0 đến 4294967295 (counter value)";

                default:
                    return "Xem IEC104 standard";
            }
        }
    }
}