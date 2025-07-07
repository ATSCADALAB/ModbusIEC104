using System;

namespace IEC104
{
    /// <summary>
    /// IEC104 Data Converter - extend từ ModbusTCP.Converter để support IEC104 specific conversions
    /// Xử lý chuyển đổi dữ liệu theo chuẩn IEC104
    /// </summary>
    public static class IEC104Converter
    {
        #region NORMALIZED VALUE CONVERSION

        /// <summary>
        /// Convert normalized value (-32768 to +32767) sang real value
        /// Formula: RealValue = (NormalizedValue / 32768.0) * (MaxValue - MinValue) + MinValue
        /// </summary>
        /// <param name="normalizedValue">Normalized value (-32768 to +32767)</param>
        /// <param name="minValue">Minimum real value</param>
        /// <param name="maxValue">Maximum real value</param>
        /// <returns>Real value</returns>
        public static float ConvertNormalizedValue(short normalizedValue, float minValue = -1.0f, float maxValue = 1.0f)
        {
            // Clamp normalized value to valid range
            float normalized = Math.Max(-32768, Math.Min(32767, normalizedValue));

            // Convert to -1.0 to +1.0 range
            float ratio = normalized / 32768.0f;

            // Scale to real range
            float range = maxValue - minValue;
            return (ratio * range / 2.0f) + (minValue + maxValue) / 2.0f;
        }

        /// <summary>
        /// Convert real value sang normalized value (-32768 to +32767)
        /// </summary>
        /// <param name="realValue">Real value</param>
        /// <param name="minValue">Minimum real value</param>
        /// <param name="maxValue">Maximum real value</param>
        /// <returns>Normalized value</returns>
        public static short ConvertToNormalizedValue(float realValue, float minValue = -1.0f, float maxValue = 1.0f)
        {
            // Clamp real value to valid range
            float clamped = Math.Max(minValue, Math.Min(maxValue, realValue));

            // Convert to -1.0 to +1.0 range
            float range = maxValue - minValue;
            float ratio = ((clamped - (minValue + maxValue) / 2.0f) * 2.0f) / range;

            // Scale to normalized range
            return (short)(ratio * 32768.0f);
        }

        #endregion

        #region SCALED VALUE CONVERSION

        /// <summary>
        /// Convert scaled value sang real value using scale factor
        /// Formula: RealValue = (ScaledValue * ScaleFactor) + Offset
        /// </summary>
        /// <param name="scaledValue">Scaled value (-32768 to +32767)</param>
        /// <param name="scaleFactor">Scale factor</param>
        /// <param name="offset">Offset value</param>
        /// <returns>Real value</returns>
        public static float ConvertScaledValue(short scaledValue, float scaleFactor = 1.0f, float offset = 0.0f)
        {
            return (scaledValue * scaleFactor) + offset;
        }

        /// <summary>
        /// Convert real value sang scaled value
        /// </summary>
        /// <param name="realValue">Real value</param>
        /// <param name="scaleFactor">Scale factor</param>
        /// <param name="offset">Offset value</param>
        /// <returns>Scaled value</returns>
        public static short ConvertToScaledValue(float realValue, float scaleFactor = 1.0f, float offset = 0.0f)
        {
            if (Math.Abs(scaleFactor) < float.Epsilon)
                return 0;

            float scaled = (realValue - offset) / scaleFactor;
            return (short)Math.Max(-32768, Math.Min(32767, scaled));
        }

        #endregion

        #region QUALITY DESCRIPTOR CONVERSION

        /// <summary>
        /// Create Quality Descriptor từ byte value
        /// </summary>
        /// <param name="qualityByte">Quality byte</param>
        /// <returns>Quality Descriptor</returns>
        public static IEC104QualityDescriptor GetQualityDescriptor(byte qualityByte)
        {
            return new IEC104QualityDescriptor(qualityByte);
        }

        /// <summary>
        /// Convert Quality Descriptor sang byte value
        /// </summary>
        /// <param name="quality">Quality Descriptor</param>
        /// <returns>Quality byte</returns>
        public static byte SetQualityDescriptor(IEC104QualityDescriptor quality)
        {
            return quality.ToByte();
        }

        /// <summary>
        /// Merge quality từ multiple sources
        /// </summary>
        /// <param name="qualities">Array of quality descriptors</param>
        /// <returns>Merged quality</returns>
        public static IEC104QualityDescriptor MergeQuality(params IEC104QualityDescriptor[] qualities)
        {
            if (qualities == null || qualities.Length == 0)
                return IEC104DataUtilities.CreateGoodQuality();

            var result = new IEC104QualityDescriptor();

            foreach (var quality in qualities)
            {
                result.Invalid |= quality.Invalid;
                result.NotTopical |= quality.NotTopical;
                result.Substituted |= quality.Substituted;
                result.Blocked |= quality.Blocked;
                result.Overflow |= quality.Overflow;
            }

            return result;
        }

        #endregion

        #region DOUBLE POINT VALUE CONVERSION

        /// <summary>
        /// Get Double Point Value từ byte
        /// </summary>
        /// <param name="rawValue">Raw byte value</param>
        /// <returns>Double Point Value</returns>
        public static DoublePointValue GetDoublePointValue(byte rawValue)
        {
            byte dpi = (byte)(rawValue & 0x03); // Lower 2 bits
            return (DoublePointValue)dpi;
        }

        /// <summary>
        /// Set Double Point Value trong byte
        /// </summary>
        /// <param name="value">Double Point Value</param>
        /// <param name="qualityByte">Existing quality byte</param>
        /// <returns>Combined byte value</returns>
        public static byte SetDoublePointValue(DoublePointValue value, byte qualityByte = 0)
        {
            byte result = (byte)(qualityByte & 0xFC); // Clear lower 2 bits
            result |= (byte)((byte)value & 0x03);     // Set DPI bits
            return result;
        }

        /// <summary>
        /// Convert Double Point Value sang boolean (for compatibility)
        /// </summary>
        /// <param name="value">Double Point Value</param>
        /// <returns>Boolean equivalent</returns>
        public static bool DoublePointToBoolean(DoublePointValue value)
        {
            return value == DoublePointValue.ON;
        }

        /// <summary>
        /// Convert boolean sang Double Point Value
        /// </summary>
        /// <param name="value">Boolean value</param>
        /// <returns>Double Point Value</returns>
        public static DoublePointValue BooleanToDoublePoint(bool value)
        {
            return value ? DoublePointValue.ON : DoublePointValue.OFF;
        }

        #endregion

        #region STEP COMMAND VALUE CONVERSION

        /// <summary>
        /// Get Step Command Value từ byte
        /// </summary>
        /// <param name="rawValue">Raw byte value</param>
        /// <returns>Step Command Value</returns>
        public static StepCommandValue GetStepCommandValue(byte rawValue)
        {
            byte rcs = (byte)(rawValue & 0x03); // Lower 2 bits
            return (StepCommandValue)rcs;
        }

        /// <summary>
        /// Set Step Command Value trong byte
        /// </summary>
        /// <param name="value">Step Command Value</param>
        /// <param name="selectExecute">Select/Execute bit</param>
        /// <param name="qualifier">Qualifier (5 bits)</param>
        /// <returns>Combined byte value</returns>
        public static byte SetStepCommandValue(StepCommandValue value, bool selectExecute = false, byte qualifier = 0)
        {
            byte result = (byte)((qualifier & 0x1F) << 2); // QU: bits 2-6
            result |= (byte)((byte)value & 0x03);          // RCS: bits 0-1
            if (selectExecute) result |= 0x80;             // S/E: bit 7
            return result;
        }

        #endregion

        #region TIME CONVERSION (CP56Time2a)

        /// <summary>
        /// Convert CP56Time2a (7 bytes) sang DateTime
        /// Format: ms(2) + min(1) + hour(1) + day(1) + month(1) + year(1)
        /// </summary>
        /// <param name="timeBytes">7-byte time array</param>
        /// <returns>DateTime value</returns>
        public static DateTime ConvertCP56Time2a(byte[] timeBytes)
        {
            if (timeBytes == null || timeBytes.Length < 7)
                return DateTime.MinValue;

            try
            {
                // Milliseconds (0-59999)
                ushort milliseconds = (ushort)(timeBytes[0] | (timeBytes[1] << 8));
                int ms = milliseconds % 1000;
                int seconds = milliseconds / 1000;

                // Minutes (0-59)
                int minutes = timeBytes[2] & 0x3F;

                // Hours (0-23)
                int hours = timeBytes[3] & 0x1F;

                // Day (1-31)
                int day = timeBytes[4] & 0x1F;

                // Month (1-12)
                int month = timeBytes[5] & 0x0F;

                // Year (0-99, represents 2000-2099)
                int year = (timeBytes[6] & 0x7F) + 2000;

                // Validate ranges
                if (seconds > 59 || minutes > 59 || hours > 23 ||
                    day < 1 || day > 31 || month < 1 || month > 12)
                {
                    return DateTime.MinValue;
                }

                return new DateTime(year, month, day, hours, minutes, seconds, ms);
            }
            catch
            {
                return DateTime.MinValue;
            }
        }

        /// <summary>
        /// Convert DateTime sang CP56Time2a (7 bytes)
        /// </summary>
        /// <param name="dateTime">DateTime value</param>
        /// <returns>7-byte time array</returns>
        public static byte[] ConvertToCP56Time2a(DateTime dateTime)
        {
            var result = new byte[7];

            try
            {
                // Milliseconds + seconds in milliseconds
                ushort totalMs = (ushort)(dateTime.Second * 1000 + dateTime.Millisecond);
                result[0] = (byte)(totalMs & 0xFF);
                result[1] = (byte)((totalMs >> 8) & 0xFF);

                // Minutes
                result[2] = (byte)(dateTime.Minute & 0x3F);

                // Hours
                result[3] = (byte)(dateTime.Hour & 0x1F);

                // Day
                result[4] = (byte)(dateTime.Day & 0x1F);

                // Month
                result[5] = (byte)(dateTime.Month & 0x0F);

                // Year (subtract 2000, limit to 0-99)
                int year = Math.Max(0, Math.Min(99, dateTime.Year - 2000));
                result[6] = (byte)(year & 0x7F);

                return result;
            }
            catch
            {
                return new byte[7]; // Return zeros if conversion fails
            }
        }

        #endregion

        #region BITSTRING CONVERSION

        /// <summary>
        /// Convert 32-bit bitstring sang individual bits
        /// </summary>
        /// <param name="bitstring">32-bit value</param>
        /// <returns>Array of 32 boolean values</returns>
        public static bool[] ConvertBitstringToBits(uint bitstring)
        {
            var bits = new bool[32];
            for (int i = 0; i < 32; i++)
            {
                bits[i] = (bitstring & (1u << i)) != 0;
            }
            return bits;
        }

        /// <summary>
        /// Convert array of bits sang 32-bit bitstring
        /// </summary>
        /// <param name="bits">Array of boolean values (max 32)</param>
        /// <returns>32-bit value</returns>
        public static uint ConvertBitsToBitstring(bool[] bits)
        {
            if (bits == null)
                return 0;

            uint result = 0;
            int count = Math.Min(32, bits.Length);

            for (int i = 0; i < count; i++)
            {
                if (bits[i])
                    result |= (1u << i);
            }

            return result;
        }

        /// <summary>
        /// Get specific bit từ bitstring
        /// </summary>
        /// <param name="bitstring">32-bit value</param>
        /// <param name="bitIndex">Bit index (0-31)</param>
        /// <returns>Bit value</returns>
        public static bool GetBitstringBit(uint bitstring, int bitIndex)
        {
            if (bitIndex < 0 || bitIndex > 31)
                return false;

            return (bitstring & (1u << bitIndex)) != 0;
        }

        /// <summary>
        /// Set specific bit trong bitstring
        /// </summary>
        /// <param name="bitstring">32-bit value</param>
        /// <param name="bitIndex">Bit index (0-31)</param>
        /// <param name="value">Bit value</param>
        /// <returns>Modified bitstring</returns>
        public static uint SetBitstringBit(uint bitstring, int bitIndex, bool value)
        {
            if (bitIndex < 0 || bitIndex > 31)
                return bitstring;

            if (value)
                return bitstring | (1u << bitIndex);
            else
                return bitstring & ~(1u << bitIndex);
        }

        #endregion

        #region INTEGRATED TOTALS (COUNTER) CONVERSION

        /// <summary>
        /// Convert Binary Counter Reading (BCR) - 5 bytes
        /// Format: Counter Value (4 bytes) + Sequence Number + Carry + Adjust + Invalid (1 byte)
        /// </summary>
        /// <param name="bcrBytes">5-byte BCR array</param>
        /// <param name="counterValue">Counter value output</param>
        /// <param name="sequenceNumber">Sequence number output</param>
        /// <param name="carry">Carry flag output</param>
        /// <param name="adjust">Adjust flag output</param>
        /// <param name="invalid">Invalid flag output</param>
        /// <returns>True if conversion successful</returns>
        public static bool ConvertBCR(byte[] bcrBytes, out uint counterValue, out byte sequenceNumber,
            out bool carry, out bool adjust, out bool invalid)
        {
            counterValue = 0;
            sequenceNumber = 0;
            carry = false;
            adjust = false;
            invalid = false;

            if (bcrBytes == null || bcrBytes.Length < 5)
                return false;

            try
            {
                // Counter value (4 bytes, little endian)
                counterValue = (uint)(bcrBytes[0] | (bcrBytes[1] << 8) | (bcrBytes[2] << 16) | (bcrBytes[3] << 24));

                // Status byte
                byte status = bcrBytes[4];
                sequenceNumber = (byte)(status & 0x1F);  // Bits 0-4
                carry = (status & 0x20) != 0;            // Bit 5
                adjust = (status & 0x40) != 0;           // Bit 6
                invalid = (status & 0x80) != 0;          // Bit 7

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Create Binary Counter Reading (BCR) - 5 bytes
        /// </summary>
        /// <param name="counterValue">Counter value</param>
        /// <param name="sequenceNumber">Sequence number (0-31)</param>
        /// <param name="carry">Carry flag</param>
        /// <param name="adjust">Adjust flag</param>
        /// <param name="invalid">Invalid flag</param>
        /// <returns>5-byte BCR array</returns>
        public static byte[] CreateBCR(uint counterValue, byte sequenceNumber = 0,
            bool carry = false, bool adjust = false, bool invalid = false)
        {
            var result = new byte[5];

            // Counter value (4 bytes, little endian)
            result[0] = (byte)(counterValue & 0xFF);
            result[1] = (byte)((counterValue >> 8) & 0xFF);
            result[2] = (byte)((counterValue >> 16) & 0xFF);
            result[3] = (byte)((counterValue >> 24) & 0xFF);

            // Status byte
            byte status = (byte)(sequenceNumber & 0x1F);
            if (carry) status |= 0x20;
            if (adjust) status |= 0x40;
            if (invalid) status |= 0x80;
            result[4] = status;

            return result;
        }

        #endregion

        #region COMMAND OBJECT CONVERSION

        /// <summary>
        /// Create Single Command Object (SCO)
        /// </summary>
        /// <param name="commandState">Command state (true/false)</param>
        /// <param name="selectExecute">Select (true) or Execute (false)</param>
        /// <param name="qualifier">Qualifier of command (0-31)</param>
        /// <returns>SCO byte</returns>
        public static byte CreateSingleCommandObject(bool commandState, bool selectExecute = false, byte qualifier = 0)
        {
            byte sco = (byte)(qualifier & 0x1F);  // QU: bits 1-5
            if (commandState) sco |= 0x01;        // SCS: bit 0
            if (selectExecute) sco |= 0x80;       // S/E: bit 7
            return sco;
        }

        /// <summary>
        /// Parse Single Command Object (SCO)
        /// </summary>
        /// <param name="sco">SCO byte</param>
        /// <param name="commandState">Command state output</param>
        /// <param name="selectExecute">Select/Execute flag output</param>
        /// <param name="qualifier">Qualifier output</param>
        public static void ParseSingleCommandObject(byte sco, out bool commandState, out bool selectExecute, out byte qualifier)
        {
            commandState = (sco & 0x01) != 0;
            selectExecute = (sco & 0x80) != 0;
            qualifier = (byte)((sco >> 1) & 0x1F);
        }

        /// <summary>
        /// Create Double Command Object (DCO)
        /// </summary>
        /// <param name="commandState">Double command state (0-3)</param>
        /// <param name="selectExecute">Select (true) or Execute (false)</param>
        /// <param name="qualifier">Qualifier of command (0-31)</param>
        /// <returns>DCO byte</returns>
        public static byte CreateDoubleCommandObject(DoublePointValue commandState, bool selectExecute = false, byte qualifier = 0)
        {
            byte dco = (byte)(qualifier & 0x1F);     // QU: bits 2-6
            dco |= (byte)((byte)commandState & 0x03); // DCS: bits 0-1
            if (selectExecute) dco |= 0x80;          // S/E: bit 7
            return dco;
        }

        /// <summary>
        /// Parse Double Command Object (DCO)
        /// </summary>
        /// <param name="dco">DCO byte</param>
        /// <param name="commandState">Command state output</param>
        /// <param name="selectExecute">Select/Execute flag output</param>
        /// <param name="qualifier">Qualifier output</param>
        public static void ParseDoubleCommandObject(byte dco, out DoublePointValue commandState, out bool selectExecute, out byte qualifier)
        {
            commandState = (DoublePointValue)(dco & 0x03);
            selectExecute = (dco & 0x80) != 0;
            qualifier = (byte)((dco >> 2) & 0x1F);
        }

        #endregion

        #region UTILITY METHODS

        /// <summary>
        /// Validate IEC104 value range cho specific data type
        /// </summary>
        /// <param name="dataType">IEC104 data type</param>
        /// <param name="value">Value to validate</param>
        /// <param name="errorMessage">Error message if invalid</param>
        /// <returns>True if valid</returns>
        public static bool ValidateValueRange(IEC104DataType dataType, object value, out string errorMessage)
        {
            errorMessage = "";

            try
            {
                switch (dataType)
                {
                    case IEC104DataType.NormalizedValue:
                    case IEC104DataType.NormalizedSetpoint:
                        if (value is short shortVal)
                        {
                            // Normalized values: -32768 to +32767
                            return true; // short range is already correct
                        }
                        errorMessage = "Normalized value must be short (-32768 to +32767)";
                        return false;

                    case IEC104DataType.ScaledValue:
                    case IEC104DataType.ScaledSetpoint:
                        if (value is short)
                        {
                            return true; // short range is correct
                        }
                        errorMessage = "Scaled value must be short (-32768 to +32767)";
                        return false;

                    case IEC104DataType.FloatValue:
                    case IEC104DataType.FloatSetpoint:
                        if (value is float floatVal)
                        {
                            if (float.IsNaN(floatVal) || float.IsInfinity(floatVal))
                            {
                                errorMessage = "Float value cannot be NaN or Infinity";
                                return false;
                            }
                            return true;
                        }
                        errorMessage = "Float value must be float type";
                        return false;

                    case IEC104DataType.SinglePoint:
                    case IEC104DataType.SingleCommand:
                        if (value is bool)
                        {
                            return true;
                        }
                        errorMessage = "Single point/command value must be boolean";
                        return false;

                    case IEC104DataType.DoublePoint:
                    case IEC104DataType.DoubleCommand:
                        if (value is DoublePointValue dpv)
                        {
                            return Enum.IsDefined(typeof(DoublePointValue), dpv);
                        }
                        errorMessage = "Double point/command value must be DoublePointValue enum";
                        return false;

                    default:
                        return true; // Unknown types pass validation
                }
            }
            catch
            {
                errorMessage = $"Value type mismatch for {dataType}";
                return false;
            }
        }

        public static object GetDefaultValue(IEC104DataType dataType)
        {
            switch (dataType)
            {
                case IEC104DataType.SinglePoint:
                case IEC104DataType.SingleCommand:
                    return false;

                case IEC104DataType.DoublePoint:
                case IEC104DataType.DoubleCommand:
                    return DoublePointValue.Indeterminate;

                case IEC104DataType.StepPosition:
                case IEC104DataType.StepCommand:
                    return (byte)0;

                case IEC104DataType.NormalizedValue:
                case IEC104DataType.NormalizedSetpoint:
                case IEC104DataType.ScaledValue:
                case IEC104DataType.ScaledSetpoint:
                    return (short)0;

                case IEC104DataType.FloatValue:
                case IEC104DataType.FloatSetpoint:
                    return 0.0f;

                case IEC104DataType.Bitstring32:
                    return (uint)0;

                case IEC104DataType.IntegratedTotals:
                    return (uint)0;

                default:
                    return null;
            }
        }

        /// <summary>
        /// Convert object value sang string representation
        /// </summary>
        /// <param name="dataType">IEC104 data type</param>
        /// <param name="value">Value object</param>
        /// <param name="quality">Quality descriptor</param>
        /// <returns>String representation</returns>
        public static string ConvertValueToString(IEC104DataType dataType, object value, IEC104QualityDescriptor quality)
        {
            if (value == null)
                return "NULL";

            try
            {
                string valueStr = "";

                switch (dataType)
                {
                    case IEC104DataType.SinglePoint:
                    case IEC104DataType.SingleCommand:
                        valueStr = (bool)value ? "1" : "0";
                        break;

                    case IEC104DataType.DoublePoint:
                    case IEC104DataType.DoubleCommand:
                        var dpv = (DoublePointValue)value;
                        valueStr = ((int)dpv).ToString();
                        break;

                    case IEC104DataType.StepPosition:
                    case IEC104DataType.StepCommand:
                        valueStr = value.ToString();
                        break;

                    case IEC104DataType.NormalizedValue:
                    case IEC104DataType.NormalizedSetpoint:
                        valueStr = value.ToString();
                        break;

                    case IEC104DataType.ScaledValue:
                    case IEC104DataType.ScaledSetpoint:
                        valueStr = value.ToString();
                        break;

                    case IEC104DataType.FloatValue:
                    case IEC104DataType.FloatSetpoint:
                        valueStr = ((float)value).ToString("F6");
                        break;

                    case IEC104DataType.Bitstring32:
                        valueStr = value.ToString();
                        break;

                    case IEC104DataType.IntegratedTotals:
                        valueStr = value.ToString();
                        break;

                    default:
                        valueStr = value.ToString();
                        break;
                }

                // Append quality if not good
                if (!quality.IsGood)
                {
                    valueStr += $" [{quality}]";
                }

                return valueStr;
            }
            catch
            {
                return "ERROR";
            }
        }

        /// <summary>
        /// Convert string sang typed value cho specific data type
        /// </summary>
        /// <param name="dataType">IEC104 data type</param>
        /// <param name="valueString">String value</param>
        /// <returns>Typed value object hoặc null nếu lỗi</returns>
        public static object ConvertStringToValue(IEC104DataType dataType, string valueString)
        {
            if (string.IsNullOrWhiteSpace(valueString))
                return GetDefaultValue(dataType);

            try
            {
                switch (dataType)
                {
                    case IEC104DataType.SinglePoint:
                    case IEC104DataType.SingleCommand:
                        if (valueString.Equals("1") || valueString.ToLower().Equals("true"))
                            return true;
                        else if (valueString.Equals("0") || valueString.ToLower().Equals("false"))
                            return false;
                        break;

                    case IEC104DataType.DoublePoint:
                    case IEC104DataType.DoubleCommand:
                        if (byte.TryParse(valueString, out byte dpVal) && dpVal <= 3)
                            return (DoublePointValue)dpVal;
                        break;

                    case IEC104DataType.StepPosition:
                    case IEC104DataType.StepCommand:
                        if (byte.TryParse(valueString, out byte stepVal))
                            return stepVal;
                        break;

                    case IEC104DataType.NormalizedValue:
                    case IEC104DataType.NormalizedSetpoint:
                        if (short.TryParse(valueString, out short normVal))
                            return normVal;
                        break;

                    case IEC104DataType.ScaledValue:
                    case IEC104DataType.ScaledSetpoint:
                        if (short.TryParse(valueString, out short scaleVal))
                            return scaleVal;
                        break;

                    case IEC104DataType.FloatValue:
                    case IEC104DataType.FloatSetpoint:
                        if (float.TryParse(valueString, out float floatVal))
                            return floatVal;
                        break;

                    case IEC104DataType.Bitstring32:
                        if (uint.TryParse(valueString, out uint bitVal))
                            return bitVal;
                        break;

                    case IEC104DataType.IntegratedTotals:
                        if (uint.TryParse(valueString, out uint counterVal))
                            return counterVal;
                        break;
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Clamp value vào range hợp lệ cho data type
        /// </summary>
        /// <param name="dataType">IEC104 data type</param>
        /// <param name="value">Value to clamp</param>
        /// <returns>Clamped value</returns>
        public static object ClampValue(IEC104DataType dataType, object value)
        {
            if (value == null)
                return GetDefaultValue(dataType);

            try
            {
                switch (dataType)
                {
                    case IEC104DataType.NormalizedValue:
                    case IEC104DataType.NormalizedSetpoint:
                        if (value is short shortVal)
                            return shortVal; // short is already in correct range
                        if (value is int intVal)
                            return (short)Math.Max(-32768, Math.Min(32767, intVal));
                        if (value is float floatVal)
                            return (short)Math.Max(-32768, Math.Min(32767, floatVal));
                        break;

                    case IEC104DataType.ScaledValue:
                    case IEC104DataType.ScaledSetpoint:
                        if (value is short shortVal2)
                            return shortVal2;
                        if (value is int intVal2)
                            return (short)Math.Max(-32768, Math.Min(32767, intVal2));
                        if (value is float floatVal2)
                            return (short)Math.Max(-32768, Math.Min(32767, floatVal2));
                        break;

                    case IEC104DataType.StepPosition:
                    case IEC104DataType.StepCommand:
                        if (value is byte byteVal)
                            return byteVal;
                        if (value is int intVal3)
                            return (byte)Math.Max(0, Math.Min(255, intVal3));
                        break;

                    case IEC104DataType.DoublePoint:
                    case IEC104DataType.DoubleCommand:
                        if (value is DoublePointValue dpv)
                            return dpv;
                        if (value is int intVal4)
                            return (DoublePointValue)Math.Max(0, Math.Min(3, intVal4));
                        break;

                    default:
                        return value; // No clamping needed for other types
                }

                return GetDefaultValue(dataType);
            }
            catch
            {
                return GetDefaultValue(dataType);
            }
        }

        #endregion
    }
}