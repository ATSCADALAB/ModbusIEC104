using System;

namespace ModbusIEC104.Common
{
    /// <summary>
    /// Các loại dữ liệu hỗ trợ trong IEC104
    /// </summary>
    public enum DataType
    {
        /// <summary>Boolean - 1 bit</summary>
        Bool = 0,

        /// <summary>Byte - 8 bit unsigned</summary>
        Byte = 1,

        /// <summary>SByte - 8 bit signed</summary>
        SByte = 2,

        /// <summary>UInt16 - 16 bit unsigned</summary>
        UInt16 = 3,

        /// <summary>Int16 - 16 bit signed</summary>
        Int16 = 4,

        /// <summary>UInt32 - 32 bit unsigned</summary>
        UInt32 = 5,

        /// <summary>Int32 - 32 bit signed</summary>
        Int32 = 6,

        /// <summary>UInt64 - 64 bit unsigned</summary>
        UInt64 = 7,

        /// <summary>Int64 - 64 bit signed</summary>
        Int64 = 8,

        /// <summary>Float - 32 bit floating point</summary>
        Float = 9,

        /// <summary>Double - 64 bit floating point</summary>
        Double = 10,

        /// <summary>String - Variable length string</summary>
        String = 11,

        /// <summary>DateTime - Date and time</summary>
        DateTime = 12,

        /// <summary>IEC104 Normalized Value (-1.0 to +1.0)</summary>
        NormalizedValue = 20,

        /// <summary>IEC104 Scaled Value (with factor)</summary>
        ScaledValue = 21,

        /// <summary>IEC104 Single Point (On/Off)</summary>
        SinglePoint = 22,

        /// <summary>IEC104 Double Point (Off/On/Indeterminate/Invalid)</summary>
        DoublePoint = 23,

        /// <summary>IEC104 Step Position</summary>
        StepPosition = 24,

        /// <summary>IEC104 Bitstring 32</summary>
        Bitstring32 = 25,

        /// <summary>IEC104 Integrated Totals</summary>
        IntegratedTotals = 26,

        /// <summary>IEC104 CP56Time2a (7 bytes timestamp)</summary>
        CP56Time2a = 27,

        /// <summary>IEC104 CP24Time2a (3 bytes timestamp)</summary>
        CP24Time2a = 28,

        /// <summary>Quality Descriptor (1 byte)</summary>
        Quality = 29,

        /// <summary>Unknown data type</summary>
        Unknown = 255
    }

    /// <summary>
    /// Chất lượng dữ liệu IEC104
    /// </summary>
    [Flags]
    public enum Quality : byte
    {
        /// <summary>Dữ liệu tốt</summary>
        Good = 0x00,

        /// <summary>Overflow - Vượt quá phạm vi</summary>
        Overflow = 0x01,

        /// <summary>Blocked - Bị chặn</summary>
        Blocked = 0x10,

        /// <summary>Substituted - Bị thay thế</summary>
        Substituted = 0x20,

        /// <summary>Not Topical - Không cập nhật</summary>
        NotTopical = 0x40,

        /// <summary>Invalid - Không hợp lệ</summary>
        Invalid = 0x80,

        /// <summary>Mask để lấy tất cả quality bits</summary>
        QualityMask = 0xF0
    }

    /// <summary>
    /// Trạng thái Single Point
    /// </summary>
    public enum SinglePointState : byte
    {
        /// <summary>Off/False</summary>
        Off = 0,

        /// <summary>On/True</summary>
        On = 1
    }

    /// <summary>
    /// Trạng thái Double Point
    /// </summary>
    public enum DoublePointState : byte
    {
        /// <summary>Indeterminate or intermediate state</summary>
        Indeterminate = 0,

        /// <summary>Determined state OFF</summary>
        Off = 1,

        /// <summary>Determined state ON</summary>
        On = 2,

        /// <summary>Indeterminate state</summary>
        Indeterminate2 = 3
    }

    /// <summary>
    /// Cause of Transmission
    /// </summary>
    public enum CauseOfTransmission : byte
    {
        /// <summary>Not used</summary>
        NotUsed = 0,

        /// <summary>Periodic, cyclic</summary>
        Periodic = 1,

        /// <summary>Background scan</summary>
        BackgroundScan = 2,

        /// <summary>Spontaneous</summary>
        Spontaneous = 3,

        /// <summary>Initialized</summary>
        Initialized = 4,

        /// <summary>Request or requested</summary>
        Request = 5,

        /// <summary>Activation</summary>
        Activation = 6,

        /// <summary>Activation confirmation</summary>
        ActivationConfirmation = 7,

        /// <summary>Deactivation</summary>
        Deactivation = 8,

        /// <summary>Deactivation confirmation</summary>
        DeactivationConfirmation = 9,

        /// <summary>Activation termination</summary>
        ActivationTermination = 10,

        /// <summary>Return information caused by a remote command</summary>
        ReturnInfoRemote = 11,

        /// <summary>Return information caused by a local command</summary>
        ReturnInfoLocal = 12,

        /// <summary>File transfer</summary>
        FileTransfer = 13,

        /// <summary>Interrogated by station interrogation</summary>
        InterrogatedByStation = 20,

        /// <summary>Interrogated by group 1</summary>
        InterrogatedByGroup1 = 21,

        /// <summary>Interrogated by group 2</summary>
        InterrogatedByGroup2 = 22,

        /// <summary>Interrogated by group 3</summary>
        InterrogatedByGroup3 = 23,

        /// <summary>Interrogated by group 4</summary>
        InterrogatedByGroup4 = 24,

        /// <summary>Interrogated by group 5</summary>
        InterrogatedByGroup5 = 25,

        /// <summary>Interrogated by group 6</summary>
        InterrogatedByGroup6 = 26,

        /// <summary>Interrogated by group 7</summary>
        InterrogatedByGroup7 = 27,

        /// <summary>Interrogated by group 8</summary>
        InterrogatedByGroup8 = 28,

        /// <summary>Interrogated by group 9</summary>
        InterrogatedByGroup9 = 29,

        /// <summary>Interrogated by group 10</summary>
        InterrogatedByGroup10 = 30,

        /// <summary>Interrogated by group 11</summary>
        InterrogatedByGroup11 = 31,

        /// <summary>Interrogated by group 12</summary>
        InterrogatedByGroup12 = 32,

        /// <summary>Interrogated by group 13</summary>
        InterrogatedByGroup13 = 33,

        /// <summary>Interrogated by group 14</summary>
        InterrogatedByGroup14 = 34,

        /// <summary>Interrogated by group 15</summary>
        InterrogatedByGroup15 = 35,

        /// <summary>Interrogated by group 16</summary>
        InterrogatedByGroup16 = 36,

        /// <summary>Requested by counter interrogation</summary>
        RequestedByCounterInterrogation = 37,

        /// <summary>Unknown type identification</summary>
        UnknownTypeId = 44,

        /// <summary>Unknown cause of transmission</summary>
        UnknownCot = 45,

        /// <summary>Unknown common address</summary>
        UnknownCommonAddress = 46,

        /// <summary>Unknown information object address</summary>
        UnknownIoa = 47
    }

    /// <summary>
    /// Loại Interrogation
    /// </summary>
    public enum InterrogationType : byte
    {
        /// <summary>Station interrogation (global)</summary>
        General = 20,

        /// <summary>Group 1 interrogation</summary>
        Group1 = 21,

        /// <summary>Group 2 interrogation</summary>
        Group2 = 22,

        /// <summary>Group 3 interrogation</summary>
        Group3 = 23,

        /// <summary>Group 4 interrogation</summary>
        Group4 = 24,

        /// <summary>Group 5 interrogation</summary>
        Group5 = 25,

        /// <summary>Group 6 interrogation</summary>
        Group6 = 26,

        /// <summary>Group 7 interrogation</summary>
        Group7 = 27,

        /// <summary>Group 8 interrogation</summary>
        Group8 = 28,

        /// <summary>Group 9 interrogation</summary>
        Group9 = 29,

        /// <summary>Group 10 interrogation</summary>
        Group10 = 30,

        /// <summary>Group 11 interrogation</summary>
        Group11 = 31,

        /// <summary>Group 12 interrogation</summary>
        Group12 = 32,

        /// <summary>Group 13 interrogation</summary>
        Group13 = 33,

        /// <summary>Group 14 interrogation</summary>
        Group14 = 34,

        /// <summary>Group 15 interrogation</summary>
        Group15 = 35,

        /// <summary>Group 16 interrogation</summary>
        Group16 = 36
    }

    /// <summary>
    /// Loại Counter Interrogation
    /// </summary>
    public enum CounterInterrogationType : byte
    {
        /// <summary>Request counter group 1</summary>
        Group1 = 1,

        /// <summary>Request counter group 2</summary>
        Group2 = 2,

        /// <summary>Request counter group 3</summary>
        Group3 = 3,

        /// <summary>Request counter group 4</summary>
        Group4 = 4,

        /// <summary>General request counter</summary>
        General = 5
    }

    /// <summary>
    /// Qualifier of Command
    /// </summary>
    [Flags]
    public enum QualifierOfCommand : byte
    {
        /// <summary>No additional definition</summary>
        None = 0x00,

        /// <summary>Short pulse duration</summary>
        ShortPulse = 0x00,

        /// <summary>Long pulse duration</summary>
        LongPulse = 0x01,

        /// <summary>Persistent output</summary>
        Persistent = 0x02,

        /// <summary>Reserved</summary>
        Reserved = 0x03,

        /// <summary>Select command</summary>
        Select = 0x80,

        /// <summary>Execute command</summary>
        Execute = 0x00
    }

    /// <summary>
    /// Helper class cho data types
    /// </summary>
    public static class DataTypeHelper
    {
        /// <summary>
        /// Lấy kích thước của data type (bytes)
        /// </summary>
        /// <param name="dataType">Data type</param>
        /// <returns>Kích thước (bytes)</returns>
        public static int GetSize(DataType dataType)
        {
            switch (dataType)
            {
                case DataType.Bool:
                case DataType.Byte:
                case DataType.SByte:
                case DataType.SinglePoint:
                case DataType.DoublePoint:
                case DataType.StepPosition:
                case DataType.Quality:
                    return 1;

                case DataType.UInt16:
                case DataType.Int16:
                case DataType.NormalizedValue:
                case DataType.ScaledValue:
                    return 2;

                case DataType.UInt32:
                case DataType.Int32:
                case DataType.Float:
                case DataType.Bitstring32:
                    return 4;

                case DataType.UInt64:
                case DataType.Int64:
                case DataType.Double:
                    return 8;

                case DataType.CP24Time2a:
                    return 3;

                case DataType.CP56Time2a:
                    return 7;

                case DataType.IntegratedTotals:
                    return 5; // 4 bytes value + 1 byte sequence

                case DataType.String:
                case DataType.DateTime:
                case DataType.Unknown:
                default:
                    return 0; // Variable or unknown size
            }
        }

        /// <summary>
        /// Kiểm tra data type có phải là numeric không
        /// </summary>
        /// <param name="dataType">Data type</param>
        /// <returns>True nếu là numeric</returns>
        public static bool IsNumeric(DataType dataType)
        {
            switch (dataType)
            {
                case DataType.Byte:
                case DataType.SByte:
                case DataType.UInt16:
                case DataType.Int16:
                case DataType.UInt32:
                case DataType.Int32:
                case DataType.UInt64:
                case DataType.Int64:
                case DataType.Float:
                case DataType.Double:
                case DataType.NormalizedValue:
                case DataType.ScaledValue:
                case DataType.IntegratedTotals:
                    return true;

                default:
                    return false;
            }
        }

        /// <summary>
        /// Kiểm tra data type có phải là integer không
        /// </summary>
        /// <param name="dataType">Data type</param>
        /// <returns>True nếu là integer</returns>
        public static bool IsInteger(DataType dataType)
        {
            switch (dataType)
            {
                case DataType.Byte:
                case DataType.SByte:
                case DataType.UInt16:
                case DataType.Int16:
                case DataType.UInt32:
                case DataType.Int32:
                case DataType.UInt64:
                case DataType.Int64:
                case DataType.ScaledValue:
                case DataType.IntegratedTotals:
                    return true;

                default:
                    return false;
            }
        }

        /// <summary>
        /// Kiểm tra data type có phải là floating point không
        /// </summary>
        /// <param name="dataType">Data type</param>
        /// <returns>True nếu là floating point</returns>
        public static bool IsFloatingPoint(DataType dataType)
        {
            switch (dataType)
            {
                case DataType.Float:
                case DataType.Double:
                case DataType.NormalizedValue:
                    return true;

                default:
                    return false;
            }
        }

        /// <summary>
        /// Kiểm tra data type có phải là IEC104 specific không
        /// </summary>
        /// <param name="dataType">Data type</param>
        /// <returns>True nếu là IEC104 specific</returns>
        public static bool IsIEC104Specific(DataType dataType)
        {
            switch (dataType)
            {
                case DataType.NormalizedValue:
                case DataType.ScaledValue:
                case DataType.SinglePoint:
                case DataType.DoublePoint:
                case DataType.StepPosition:
                case DataType.Bitstring32:
                case DataType.IntegratedTotals:
                case DataType.CP56Time2a:
                case DataType.CP24Time2a:
                case DataType.Quality:
                    return true;

                default:
                    return false;
            }
        }

        /// <summary>
        /// Lấy tên hiển thị của data type
        /// </summary>
        /// <param name="dataType">Data type</param>
        /// <returns>Tên hiển thị</returns>
        public static string GetDisplayName(DataType dataType)
        {
            switch (dataType)
            {
                case DataType.Bool: return "Boolean";
                case DataType.Byte: return "Byte (8-bit unsigned)";
                case DataType.SByte: return "SByte (8-bit signed)";
                case DataType.UInt16: return "UInt16 (16-bit unsigned)";
                case DataType.Int16: return "Int16 (16-bit signed)";
                case DataType.UInt32: return "UInt32 (32-bit unsigned)";
                case DataType.Int32: return "Int32 (32-bit signed)";
                case DataType.UInt64: return "UInt64 (64-bit unsigned)";
                case DataType.Int64: return "Int64 (64-bit signed)";
                case DataType.Float: return "Float (32-bit)";
                case DataType.Double: return "Double (64-bit)";
                case DataType.String: return "String";
                case DataType.DateTime: return "DateTime";
                case DataType.NormalizedValue: return "Normalized Value (-1.0 to +1.0)";
                case DataType.ScaledValue: return "Scaled Value (with factor)";
                case DataType.SinglePoint: return "Single Point (On/Off)";
                case DataType.DoublePoint: return "Double Point (Off/On/Intermediate/Invalid)";
                case DataType.StepPosition: return "Step Position";
                case DataType.Bitstring32: return "Bitstring 32-bit";
                case DataType.IntegratedTotals: return "Integrated Totals";
                case DataType.CP56Time2a: return "CP56Time2a (7-byte timestamp)";
                case DataType.CP24Time2a: return "CP24Time2a (3-byte timestamp)";
                case DataType.Quality: return "Quality Descriptor";
                case DataType.Unknown: return "Unknown";
                default: return dataType.ToString();
            }
        }

        /// <summary>
        /// Lấy default value cho data type
        /// </summary>
        /// <param name="dataType">Data type</param>
        /// <returns>Default value</returns>
        public static object GetDefaultValue(DataType dataType)
        {
            switch (dataType)
            {
                case DataType.Bool:
                case DataType.SinglePoint:
                    return false;

                case DataType.Byte:
                    return (byte)0;

                case DataType.SByte:
                    return (sbyte)0;

                case DataType.UInt16:
                    return (ushort)0;

                case DataType.Int16:
                    return (short)0;

                case DataType.UInt32:
                case DataType.Bitstring32:
                    return (uint)0;

                case DataType.Int32:
                case DataType.ScaledValue:
                case DataType.IntegratedTotals:
                    return (int)0;

                case DataType.UInt64:
                    return (ulong)0;

                case DataType.Int64:
                    return (long)0;

                case DataType.Float:
                case DataType.NormalizedValue:
                    return 0.0f;

                case DataType.Double:
                    return 0.0d;

                case DataType.String:
                    return string.Empty;

                case DataType.DateTime:
                case DataType.CP56Time2a:
                case DataType.CP24Time2a:
                    return DateTime.MinValue;

                case DataType.DoublePoint:
                    return DoublePointState.Indeterminate;

                case DataType.StepPosition:
                    return (byte)0;

                case DataType.Quality:
                    return Quality.Good;

                default:
                    return null;
            }
        }
    }
}