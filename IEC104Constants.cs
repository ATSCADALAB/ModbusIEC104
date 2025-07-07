using System;

namespace ModbusIEC104
{
    /// <summary>
    /// Hằng số cho giao thức IEC 60870-5-104
    /// </summary>
    public static class IEC104Constants
    {
        #region FRAME CONSTANTS
        /// <summary>Start byte của frame (0x68)</summary>
        public const byte START_BYTE = 0x68;

        /// <summary>Độ dài tối đa của APDU (253 bytes)</summary>
        public const int MAX_APDU_LENGTH = 253;

        /// <summary>Độ dài control field (4 bytes)</summary>
        public const int CONTROL_FIELD_LENGTH = 4;

        /// <summary>Độ dài tối thiểu của frame (6 bytes: start + length + 4 bytes control)</summary>
        public const int MIN_FRAME_LENGTH = 6;

        /// <summary>Độ dài header (start + length = 2 bytes)</summary>
        public const int FRAME_HEADER_LENGTH = 2;
        #endregion

        #region PORT AND CONNECTION
        /// <summary>Cổng mặc định của IEC104 (2404)</summary>
        public const int DEFAULT_PORT = 2404;

        /// <summary>Timeout mặc định cho kết nối (30 giây)</summary>
        public const int DEFAULT_CONNECTION_TIMEOUT = 30000;

        /// <summary>Timeout mặc định cho đọc dữ liệu (10 giây)</summary>
        public const int DEFAULT_READ_TIMEOUT = 10000;
        #endregion

        #region PROTOCOL PARAMETERS
        /// <summary>Tham số k - Số lượng I-frame tối đa không được ACK (12)</summary>
        public const ushort DEFAULT_K_PARAMETER = 12;

        /// <summary>Tham số w - Số lượng I-frame trước khi gửi ACK (8)</summary>
        public const ushort DEFAULT_W_PARAMETER = 8;

        /// <summary>t0 - Timeout cho kết nối (30 giây)</summary>
        public const ushort DEFAULT_T0_TIMEOUT = 30;

        /// <summary>t1 - Timeout cho gửi hoặc test frame (15 giây)</summary>
        public const ushort DEFAULT_T1_TIMEOUT = 15;

        /// <summary>t2 - Timeout cho ACK khi không có traffic (10 giây)</summary>
        public const ushort DEFAULT_T2_TIMEOUT = 10;

        /// <summary>t3 - Timeout cho test frame khi kết nối idle (20 giây)</summary>
        public const ushort DEFAULT_T3_TIMEOUT = 20;
        #endregion

        #region FRAME FORMAT MASKS
        /// <summary>Mask để kiểm tra I-format (0x01)</summary>
        public const byte I_FORMAT_MASK = 0x01;

        /// <summary>Mask để kiểm tra S-format (0x03)</summary>
        public const byte S_FORMAT_MASK = 0x03;

        /// <summary>Giá trị cho S-format (0x01)</summary>
        public const byte S_FORMAT_VALUE = 0x01;

        /// <summary>Mask để kiểm tra U-format (0x03)</summary>
        public const byte U_FORMAT_MASK = 0x03;

        /// <summary>Giá trị cho U-format (0x03)</summary>
        public const byte U_FORMAT_VALUE = 0x03;
        #endregion

        #region U-FRAME FUNCTIONS
        /// <summary>STARTDT ACT - Lệnh bắt đầu truyền dữ liệu (0x07)</summary>
        public const byte STARTDT_ACT = 0x07;

        /// <summary>STARTDT CON - Xác nhận bắt đầu truyền dữ liệu (0x0B)</summary>
        public const byte STARTDT_CON = 0x0B;

        /// <summary>STOPDT ACT - Lệnh dừng truyền dữ liệu (0x13)</summary>
        public const byte STOPDT_ACT = 0x13;

        /// <summary>STOPDT CON - Xác nhận dừng truyền dữ liệu (0x23)</summary>
        public const byte STOPDT_CON = 0x23;

        /// <summary>TESTFR ACT - Test frame (0x43)</summary>
        public const byte TESTFR_ACT = 0x43;

        /// <summary>TESTFR CON - Xác nhận test frame (0x83)</summary>
        public const byte TESTFR_CON = 0x83;
        #endregion

        #region TYPE IDENTIFICATION (TypeID)
        // Process information in monitoring direction
        /// <summary>M_SP_NA_1 - Single-point information (1)</summary>
        public const byte M_SP_NA_1 = 1;

        /// <summary>M_DP_NA_1 - Double-point information (3)</summary>
        public const byte M_DP_NA_1 = 3;

        /// <summary>M_ST_NA_1 - Step position information (5)</summary>
        public const byte M_ST_NA_1 = 5;

        /// <summary>M_BO_NA_1 - Bitstring of 32 bit (7)</summary>
        public const byte M_BO_NA_1 = 7;

        /// <summary>M_ME_NA_1 - Measured value, normalized value (9)</summary>
        public const byte M_ME_NA_1 = 9;

        /// <summary>M_ME_NB_1 - Measured value, scaled value (11)</summary>
        public const byte M_ME_NB_1 = 11;

        /// <summary>M_ME_NC_1 - Measured value, short floating point number (13)</summary>
        public const byte M_ME_NC_1 = 13;

        /// <summary>M_IT_NA_1 - Integrated totals (15)</summary>
        public const byte M_IT_NA_1 = 15;

        // Process information with time tag
        /// <summary>M_SP_TB_1 - Single-point information with time tag CP56Time2a (30)</summary>
        public const byte M_SP_TB_1 = 30;

        /// <summary>M_DP_TB_1 - Double-point information with time tag CP56Time2a (31)</summary>
        public const byte M_DP_TB_1 = 31;

        /// <summary>M_ST_TB_1 - Step position information with time tag CP56Time2a (32)</summary>
        public const byte M_ST_TB_1 = 32;

        /// <summary>M_BO_TB_1 - Bitstring of 32 bit with time tag CP56Time2a (33)</summary>
        public const byte M_BO_TB_1 = 33;

        /// <summary>M_ME_TD_1 - Measured value, normalized value with time tag CP56Time2a (34)</summary>
        public const byte M_ME_TD_1 = 34;

        /// <summary>M_ME_TE_1 - Measured value, scaled value with time tag CP56Time2a (35)</summary>
        public const byte M_ME_TE_1 = 35;

        /// <summary>M_ME_TF_1 - Measured value, short floating point number with time tag CP56Time2a (36)</summary>
        public const byte M_ME_TF_1 = 36;

        /// <summary>M_IT_TB_1 - Integrated totals with time tag CP56Time2a (37)</summary>
        public const byte M_IT_TB_1 = 37;

        // Control direction
        /// <summary>C_SC_NA_1 - Single command (45)</summary>
        public const byte C_SC_NA_1 = 45;

        /// <summary>C_DC_NA_1 - Double command (46)</summary>
        public const byte C_DC_NA_1 = 46;

        /// <summary>C_RC_NA_1 - Regulating step command (47)</summary>
        public const byte C_RC_NA_1 = 47;

        /// <summary>C_SE_NA_1 - Set-point command, normalized value (48)</summary>
        public const byte C_SE_NA_1 = 48;

        /// <summary>C_SE_NB_1 - Set-point command, scaled value (49)</summary>
        public const byte C_SE_NB_1 = 49;

        /// <summary>C_SE_NC_1 - Set-point command, short floating point number (50)</summary>
        public const byte C_SE_NC_1 = 50;

        /// <summary>C_BO_NA_1 - Bitstring of 32 bit (51)</summary>
        public const byte C_BO_NA_1 = 51;

        // System information
        /// <summary>C_IC_NA_1 - Interrogation command (100)</summary>
        public const byte C_IC_NA_1 = 100;

        /// <summary>C_CI_NA_1 - Counter interrogation command (101)</summary>
        public const byte C_CI_NA_1 = 101;

        /// <summary>C_RD_NA_1 - Read command (102)</summary>
        public const byte C_RD_NA_1 = 102;

        /// <summary>C_CS_NA_1 - Clock synchronization command (103)</summary>
        public const byte C_CS_NA_1 = 103;

        /// <summary>C_TS_NA_1 - Test command (104)</summary>
        public const byte C_TS_NA_1 = 104;

        /// <summary>C_RP_NA_1 - Reset process command (105)</summary>
        public const byte C_RP_NA_1 = 105;

        /// <summary>C_CD_NA_1 - Delay acquisition command (106)</summary>
        public const byte C_CD_NA_1 = 106;
        #endregion

        #region CAUSE OF TRANSMISSION (COT)
        /// <summary>Not used (0)</summary>
        public const byte COT_NOT_USED = 0;

        /// <summary>Periodic, cyclic (1)</summary>
        public const byte COT_PERIODIC = 1;

        /// <summary>Background scan (2)</summary>
        public const byte COT_BACKGROUND_SCAN = 2;

        /// <summary>Spontaneous (3)</summary>
        public const byte COT_SPONTANEOUS = 3;

        /// <summary>Initialized (4)</summary>
        public const byte COT_INITIALIZED = 4;

        /// <summary>Request or requested (5)</summary>
        public const byte COT_REQUEST = 5;

        /// <summary>Activation (6)</summary>
        public const byte COT_ACTIVATION = 6;

        /// <summary>Activation confirmation (7)</summary>
        public const byte COT_ACTIVATION_CON = 7;

        /// <summary>Deactivation (8)</summary>
        public const byte COT_DEACTIVATION = 8;

        /// <summary>Deactivation confirmation (9)</summary>
        public const byte COT_DEACTIVATION_CON = 9;

        /// <summary>Activation termination (10)</summary>
        public const byte COT_ACTIVATION_TERMINATION = 10;

        /// <summary>Return information caused by a remote command (11)</summary>
        public const byte COT_RETURN_INFO_REMOTE = 11;

        /// <summary>Return information caused by a local command (12)</summary>
        public const byte COT_RETURN_INFO_LOCAL = 12;

        /// <summary>File transfer (13)</summary>
        public const byte COT_FILE_TRANSFER = 13;

        /// <summary>Interrogated by station interrogation (20)</summary>
        public const byte COT_INTERROGATED_BY_STATION = 20;

        /// <summary>Interrogated by group 1 interrogation (21)</summary>
        public const byte COT_INTERROGATED_BY_GROUP1 = 21;

        /// <summary>Interrogated by group 2 interrogation (22)</summary>
        public const byte COT_INTERROGATED_BY_GROUP2 = 22;

        /// <summary>Interrogated by group 3 interrogation (23)</summary>
        public const byte COT_INTERROGATED_BY_GROUP3 = 23;

        /// <summary>Interrogated by group 4 interrogation (24)</summary>
        public const byte COT_INTERROGATED_BY_GROUP4 = 24;

        /// <summary>Interrogated by group 5 interrogation (25)</summary>
        public const byte COT_INTERROGATED_BY_GROUP5 = 25;

        /// <summary>Interrogated by group 6 interrogation (26)</summary>
        public const byte COT_INTERROGATED_BY_GROUP6 = 26;

        /// <summary>Interrogated by group 7 interrogation (27)</summary>
        public const byte COT_INTERROGATED_BY_GROUP7 = 27;

        /// <summary>Interrogated by group 8 interrogation (28)</summary>
        public const byte COT_INTERROGATED_BY_GROUP8 = 28;

        /// <summary>Interrogated by group 9 interrogation (29)</summary>
        public const byte COT_INTERROGATED_BY_GROUP9 = 29;

        /// <summary>Interrogated by group 10 interrogation (30)</summary>
        public const byte COT_INTERROGATED_BY_GROUP10 = 30;

        /// <summary>Interrogated by group 11 interrogation (31)</summary>
        public const byte COT_INTERROGATED_BY_GROUP11 = 31;

        /// <summary>Interrogated by group 12 interrogation (32)</summary>
        public const byte COT_INTERROGATED_BY_GROUP12 = 32;

        /// <summary>Interrogated by group 13 interrogation (33)</summary>
        public const byte COT_INTERROGATED_BY_GROUP13 = 33;

        /// <summary>Interrogated by group 14 interrogation (34)</summary>
        public const byte COT_INTERROGATED_BY_GROUP14 = 34;

        /// <summary>Interrogated by group 15 interrogation (35)</summary>
        public const byte COT_INTERROGATED_BY_GROUP15 = 35;

        /// <summary>Interrogated by group 16 interrogation (36)</summary>
        public const byte COT_INTERROGATED_BY_GROUP16 = 36;

        /// <summary>Requested by counter interrogation (37)</summary>
        public const byte COT_REQUESTED_BY_COUNTER_INTERROGATION = 37;

        /// <summary>Unknown type identification (44)</summary>
        public const byte COT_UNKNOWN_TYPE_ID = 44;

        /// <summary>Unknown cause of transmission (45)</summary>
        public const byte COT_UNKNOWN_COT = 45;

        /// <summary>Unknown common address of ASDU (46)</summary>
        public const byte COT_UNKNOWN_COMMON_ADDRESS = 46;

        /// <summary>Unknown information object address (47)</summary>
        public const byte COT_UNKNOWN_IOA = 47;
        #endregion

        #region QUALIFIER VALUES
        /// <summary>Qualifier of Interrogation - Station interrogation (20)</summary>
        public const byte QOI_STATION_INTERROGATION = 20;

        /// <summary>Qualifier of Interrogation - Group 1 interrogation (21-36)</summary>
        public const byte QOI_GROUP1_INTERROGATION = 21;

        /// <summary>Qualifier of Counter Interrogation - General request counter (5)</summary>
        public const byte QCC_GENERAL_REQUEST_COUNTER = 5;

        /// <summary>Qualifier of Counter Interrogation - Request counter group 1 (1)</summary>
        public const byte QCC_REQUEST_COUNTER_GROUP1 = 1;

        /// <summary>Qualifier of Reset Process - General reset of process (1)</summary>
        public const byte QRP_GENERAL_RESET = 1;

        /// <summary>Qualifier of Reset Process - Reset pending information (2)</summary>
        public const byte QRP_RESET_PENDING_INFO = 2;
        #endregion

        #region QUALITY DESCRIPTOR BITS
        /// <summary>Quality bit - Overflow (OV)</summary>
        public const byte QDS_OVERFLOW = 0x01;

        /// <summary>Quality bit - Blocked (BL)</summary>
        public const byte QDS_BLOCKED = 0x10;

        /// <summary>Quality bit - Substituted (SB)</summary>
        public const byte QDS_SUBSTITUTED = 0x20;

        /// <summary>Quality bit - Not topical (NT)</summary>
        public const byte QDS_NOT_TOPICAL = 0x40;

        /// <summary>Quality bit - Invalid (IV)</summary>
        public const byte QDS_INVALID = 0x80;

        /// <summary>Quality bit - Good quality (no bits set)</summary>
        public const byte QDS_GOOD = 0x00;
        #endregion

        #region TIME CONSTANTS
        /// <summary>Độ dài của CP56Time2a (7 bytes)</summary>
        public const int CP56TIME2A_LENGTH = 7;

        /// <summary>Độ dài của CP24Time2a (3 bytes)</summary>
        public const int CP24TIME2A_LENGTH = 3;

        /// <summary>Milliseconds per minute</summary>
        public const int MILLISECONDS_PER_MINUTE = 60000;

        /// <summary>Minutes per hour</summary>
        public const int MINUTES_PER_HOUR = 60;

        /// <summary>Hours per day</summary>
        public const int HOURS_PER_DAY = 24;
        #endregion

        #region COMMAND CONSTANTS
        /// <summary>Select command</summary>
        public const byte SELECT_COMMAND = 0x80;

        /// <summary>Execute command</summary>
        public const byte EXECUTE_COMMAND = 0x00;

        /// <summary>Single command OFF</summary>
        public const byte SCS_OFF = 0x00;

        /// <summary>Single command ON</summary>
        public const byte SCS_ON = 0x01;

        /// <summary>Double command NOT DETERMINED</summary>
        public const byte DCS_NOT_DETERMINED = 0x00;

        /// <summary>Double command OFF</summary>
        public const byte DCS_OFF = 0x01;

        /// <summary>Double command ON</summary>
        public const byte DCS_ON = 0x02;

        /// <summary>Double command NOT DETERMINED (illegal)</summary>
        public const byte DCS_NOT_DETERMINED_ILLEGAL = 0x03;
        #endregion

        #region VALIDATION RANGES
        /// <summary>Minimum Common Address</summary>
        public const ushort MIN_COMMON_ADDRESS = 1;

        /// <summary>Maximum Common Address</summary>
        public const ushort MAX_COMMON_ADDRESS = 65534;

        /// <summary>Minimum Information Object Address</summary>
        public const uint MIN_IOA = 1;

        /// <summary>Maximum Information Object Address</summary>
        public const uint MAX_IOA = 16777215; // 3 bytes = 24 bits

        /// <summary>Maximum sequence number (15 bits)</summary>
        public const ushort MAX_SEQUENCE_NUMBER = 32767;

        /// <summary>Maximum number of information objects in one ASDU</summary>
        public const byte MAX_INFO_OBJECTS_PER_ASDU = 127;
        #endregion

        #region ERROR CODES
        /// <summary>No error</summary>
        public const int ERROR_NONE = 0;

        /// <summary>Connection timeout</summary>
        public const int ERROR_CONNECTION_TIMEOUT = -1;

        /// <summary>Invalid frame format</summary>
        public const int ERROR_INVALID_FRAME = -2;

        /// <summary>STARTDT rejected</summary>
        public const int ERROR_STARTDT_REJECTED = -3;

        /// <summary>Socket error</summary>
        public const int ERROR_SOCKET = -4;

        /// <summary>Invalid ASDU</summary>
        public const int ERROR_INVALID_ASDU = -5;

        /// <summary>Sequence number error</summary>
        public const int ERROR_SEQUENCE_NUMBER = -6;

        /// <summary>Frame too long</summary>
        public const int ERROR_FRAME_TOO_LONG = -7;

        /// <summary>Invalid parameters</summary>
        public const int ERROR_INVALID_PARAMETERS = -8;
        #endregion

        #region HELPER METHODS
        /// <summary>
        /// Kiểm tra TypeID có hợp lệ không
        /// </summary>
        /// <param name="typeId">Type ID cần kiểm tra</param>
        /// <returns>True nếu hợp lệ</returns>
        public static bool IsValidTypeID(byte typeId)
        {
            return (typeId >= 1 && typeId <= 51) ||
                   (typeId >= 58 && typeId <= 64) ||
                   (typeId >= 100 && typeId <= 110) ||
                   (typeId >= 120 && typeId <= 126);
        }

        /// <summary>
        /// Kiểm tra Cause of Transmission có hợp lệ không
        /// </summary>
        /// <param name="cot">COT cần kiểm tra</param>
        /// <returns>True nếu hợp lệ</returns>
        public static bool IsValidCOT(byte cot)
        {
            return cot <= 47;
        }

        /// <summary>
        /// Kiểm tra Common Address có hợp lệ không
        /// </summary>
        /// <param name="commonAddress">Common Address cần kiểm tra</param>
        /// <returns>True nếu hợp lệ</returns>
        public static bool IsValidCommonAddress(ushort commonAddress)
        {
            return commonAddress >= MIN_COMMON_ADDRESS && commonAddress <= MAX_COMMON_ADDRESS;
        }

        /// <summary>
        /// Kiểm tra Information Object Address có hợp lệ không
        /// </summary>
        /// <param name="ioa">IOA cần kiểm tra</param>
        /// <returns>True nếu hợp lệ</returns>
        public static bool IsValidIOA(uint ioa)
        {
            return ioa >= MIN_IOA && ioa <= MAX_IOA;
        }

        /// <summary>
        /// Lấy tên của TypeID
        /// </summary>
        /// <param name="typeId">Type ID</param>
        /// <returns>Tên của TypeID</returns>
        public static string GetTypeIDName(byte typeId)
        {
            switch (typeId)
            {
                case M_SP_NA_1: return "M_SP_NA_1 (Single-point information)";
                case M_DP_NA_1: return "M_DP_NA_1 (Double-point information)";
                case M_ST_NA_1: return "M_ST_NA_1 (Step position information)";
                case M_BO_NA_1: return "M_BO_NA_1 (Bitstring of 32 bit)";
                case M_ME_NA_1: return "M_ME_NA_1 (Measured value, normalized)";
                case M_ME_NB_1: return "M_ME_NB_1 (Measured value, scaled)";
                case M_ME_NC_1: return "M_ME_NC_1 (Measured value, float)";
                case M_IT_NA_1: return "M_IT_NA_1 (Integrated totals)";
                case M_SP_TB_1: return "M_SP_TB_1 (Single-point with time tag)";
                case M_DP_TB_1: return "M_DP_TB_1 (Double-point with time tag)";
                case M_ST_TB_1: return "M_ST_TB_1 (Step position with time tag)";
                case M_BO_TB_1: return "M_BO_TB_1 (Bitstring with time tag)";
                case M_ME_TD_1: return "M_ME_TD_1 (Measured normalized with time tag)";
                case M_ME_TE_1: return "M_ME_TE_1 (Measured scaled with time tag)";
                case M_ME_TF_1: return "M_ME_TF_1 (Measured float with time tag)";
                case M_IT_TB_1: return "M_IT_TB_1 (Integrated totals with time tag)";
                case C_SC_NA_1: return "C_SC_NA_1 (Single command)";
                case C_DC_NA_1: return "C_DC_NA_1 (Double command)";
                case C_RC_NA_1: return "C_RC_NA_1 (Regulating step command)";
                case C_SE_NA_1: return "C_SE_NA_1 (Set-point command, normalized)";
                case C_SE_NB_1: return "C_SE_NB_1 (Set-point command, scaled)";
                case C_SE_NC_1: return "C_SE_NC_1 (Set-point command, float)";
                case C_BO_NA_1: return "C_BO_NA_1 (Bitstring command)";
                case C_IC_NA_1: return "C_IC_NA_1 (Interrogation command)";
                case C_CI_NA_1: return "C_CI_NA_1 (Counter interrogation command)";
                case C_RD_NA_1: return "C_RD_NA_1 (Read command)";
                case C_CS_NA_1: return "C_CS_NA_1 (Clock synchronization command)";
                case C_TS_NA_1: return "C_TS_NA_1 (Test command)";
                case C_RP_NA_1: return "C_RP_NA_1 (Reset process command)";
                case C_CD_NA_1: return "C_CD_NA_1 (Delay acquisition command)";
                default: return $"Unknown TypeID ({typeId})";
            }
        }

        /// <summary>
        /// Lấy tên của Cause of Transmission
        /// </summary>
        /// <param name="cot">Cause of Transmission</param>
        /// <returns>Tên của COT</returns>
        public static string GetCOTName(byte cot)
        {
            switch (cot)
            {
                case COT_NOT_USED: return "Not used";
                case COT_PERIODIC: return "Periodic, cyclic";
                case COT_BACKGROUND_SCAN: return "Background scan";
                case COT_SPONTANEOUS: return "Spontaneous";
                case COT_INITIALIZED: return "Initialized";
                case COT_REQUEST: return "Request or requested";
                case COT_ACTIVATION: return "Activation";
                case COT_ACTIVATION_CON: return "Activation confirmation";
                case COT_DEACTIVATION: return "Deactivation";
                case COT_DEACTIVATION_CON: return "Deactivation confirmation";
                case COT_ACTIVATION_TERMINATION: return "Activation termination";
                case COT_RETURN_INFO_REMOTE: return "Return info caused by remote command";
                case COT_RETURN_INFO_LOCAL: return "Return info caused by local command";
                case COT_FILE_TRANSFER: return "File transfer";
                case COT_INTERROGATED_BY_STATION: return "Interrogated by station interrogation";
                case COT_INTERROGATED_BY_GROUP1: return "Interrogated by group 1";
                case COT_INTERROGATED_BY_GROUP2: return "Interrogated by group 2";
                case COT_INTERROGATED_BY_GROUP3: return "Interrogated by group 3";
                case COT_INTERROGATED_BY_GROUP4: return "Interrogated by group 4";
                case COT_INTERROGATED_BY_GROUP5: return "Interrogated by group 5";
                case COT_INTERROGATED_BY_GROUP6: return "Interrogated by group 6";
                case COT_INTERROGATED_BY_GROUP7: return "Interrogated by group 7";
                case COT_INTERROGATED_BY_GROUP8: return "Interrogated by group 8";
                case COT_INTERROGATED_BY_GROUP9: return "Interrogated by group 9";
                case COT_INTERROGATED_BY_GROUP10: return "Interrogated by group 10";
                case COT_INTERROGATED_BY_GROUP11: return "Interrogated by group 11";
                case COT_INTERROGATED_BY_GROUP12: return "Interrogated by group 12";
                case COT_INTERROGATED_BY_GROUP13: return "Interrogated by group 13";
                case COT_INTERROGATED_BY_GROUP14: return "Interrogated by group 14";
                case COT_INTERROGATED_BY_GROUP15: return "Interrogated by group 15";
                case COT_INTERROGATED_BY_GROUP16: return "Interrogated by group 16";
                case COT_REQUESTED_BY_COUNTER_INTERROGATION: return "Requested by counter interrogation";
                case COT_UNKNOWN_TYPE_ID: return "Unknown type identification";
                case COT_UNKNOWN_COT: return "Unknown cause of transmission";
                case COT_UNKNOWN_COMMON_ADDRESS: return "Unknown common address";
                case COT_UNKNOWN_IOA: return "Unknown information object address";
                default: return $"Unknown COT ({cot})";
            }
        }
        #endregion
    }
}