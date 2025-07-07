namespace IEC104
{
    /// <summary>
    /// Định nghĩa các hằng số cho giao thức IEC104 - FIXED VERSION
    /// </summary>
    public static class IEC104Constants
    {
        #region FRAME FORMAT CONSTANTS

        /// <summary>Start byte cho tất cả frame IEC104</summary>
        public const byte START_BYTE = 0x68;

        /// <summary>Độ dài tối đa của APDU</summary>
        public const int MAX_APDU_LENGTH = 253;

        /// <summary>Độ dài control field</summary>
        public const int CONTROL_FIELD_LENGTH = 4;

        /// <summary>Độ dài tối thiểu của frame (start + length + control)</summary>
        public const int MIN_FRAME_LENGTH = 6;

        /// <summary>Port mặc định cho IEC104</summary>
        public const int DEFAULT_PORT = 2404;

        #endregion

        #region FRAME FORMAT TYPES

        /// <summary>I-Format frame (Information)</summary>
        public const byte I_FORMAT = 0x00;

        /// <summary>S-Format frame (Supervisory)</summary>
        public const byte S_FORMAT = 0x01;

        /// <summary>U-Format frame (Unnumbered)</summary>
        public const byte U_FORMAT = 0x03;

        #endregion

        #region U-FRAME FUNCTIONS

        /// <summary>STARTDT act (Start Data Transfer activation)</summary>
        public const byte STARTDT_ACT = 0x07;

        /// <summary>STARTDT con (Start Data Transfer confirmation)</summary>
        public const byte STARTDT_CON = 0x0B;

        /// <summary>STOPDT act (Stop Data Transfer activation)</summary>
        public const byte STOPDT_ACT = 0x13;

        /// <summary>STOPDT con (Stop Data Transfer confirmation)</summary>
        public const byte STOPDT_CON = 0x23;

        /// <summary>TESTFR act (Test Frame activation)</summary>
        public const byte TESTFR_ACT = 0x43;

        /// <summary>TESTFR con (Test Frame confirmation)</summary>
        public const byte TESTFR_CON = 0x83;

        #endregion

        #region TYPE IDENTIFICATION (MONITORING DIRECTION)

        /// <summary>Single-point information M_SP_NA_1</summary>
        public const byte M_SP_NA_1 = 1;

        /// <summary>Double-point information M_DP_NA_1</summary>
        public const byte M_DP_NA_1 = 3;

        /// <summary>Step position information M_ST_NA_1</summary>
        public const byte M_ST_NA_1 = 5;

        /// <summary>Bitstring of 32 bit M_BO_NA_1</summary>
        public const byte M_BO_NA_1 = 7;

        /// <summary>Measured value, normalized value M_ME_NA_1</summary>
        public const byte M_ME_NA_1 = 9;

        /// <summary>Measured value, scaled value M_ME_NB_1</summary>
        public const byte M_ME_NB_1 = 11;

        /// <summary>Measured value, short floating point M_ME_NC_1</summary>
        public const byte M_ME_NC_1 = 13;

        /// <summary>Integrated totals M_IT_NA_1</summary>
        public const byte M_IT_NA_1 = 15;

        #endregion

        #region TYPE IDENTIFICATION (CONTROL DIRECTION)

        /// <summary>Single command C_SC_NA_1</summary>
        public const byte C_SC_NA_1 = 45;

        /// <summary>Double command C_DC_NA_1</summary>
        public const byte C_DC_NA_1 = 46;

        /// <summary>Regulating step command C_RC_NA_1</summary>
        public const byte C_RC_NA_1 = 47;

        /// <summary>Set-point command, normalized value C_SE_NA_1</summary>
        public const byte C_SE_NA_1 = 48;

        /// <summary>Set-point command, scaled value C_SE_NB_1</summary>
        public const byte C_SE_NB_1 = 49;

        /// <summary>Set-point command, short floating point C_SE_NC_1</summary>
        public const byte C_SE_NC_1 = 50;

        #endregion

        #region TYPE IDENTIFICATION (SYSTEM COMMANDS)

        /// <summary>Interrogation command C_IC_NA_1</summary>
        public const byte C_IC_NA_1 = 100;

        /// <summary>Counter interrogation command C_CI_NA_1</summary>
        public const byte C_CI_NA_1 = 101;

        /// <summary>Read command C_RD_NA_1</summary>
        public const byte C_RD_NA_1 = 102;

        /// <summary>Clock synchronization command C_CS_NA_1</summary>
        public const byte C_CS_NA_1 = 103;

        /// <summary>Reset process command C_RP_NA_1</summary>
        public const byte C_RP_NA_1 = 105;

        #endregion

        #region CAUSE OF TRANSMISSION

        /// <summary>Periodic</summary>
        public const byte COT_PERIODIC = 1;

        /// <summary>Background scan</summary>
        public const byte COT_BACKGROUND = 2;

        /// <summary>Spontaneous</summary>
        public const byte COT_SPONTANEOUS = 3;

        /// <summary>Initialized</summary>
        public const byte COT_INITIALIZED = 4;

        /// <summary>Request or requested</summary>
        public const byte COT_REQUEST = 5;

        /// <summary>Activation</summary>
        public const byte COT_ACTIVATION = 6;

        /// <summary>Activation confirmation</summary>
        public const byte COT_ACTIVATION_CON = 7;

        /// <summary>Deactivation</summary>
        public const byte COT_DEACTIVATION = 8;

        /// <summary>Deactivation confirmation</summary>
        public const byte COT_DEACTIVATION_CON = 9;

        /// <summary>Activation termination</summary>
        public const byte COT_ACTIVATION_TERM = 10;

        /// <summary>Return information remote command</summary>
        public const byte COT_RETURN_INFO_REMOTE = 11;

        /// <summary>Return information local command</summary>
        public const byte COT_RETURN_INFO_LOCAL = 12;

        /// <summary>File transfer</summary>
        public const byte COT_FILE_TRANSFER = 13;

        #endregion

        #region CAUSE OF TRANSMISSION (NEGATIVE)

        /// <summary>Unknown type identification</summary>
        public const byte COT_UNKNOWN_TYPE_ID = 44;

        /// <summary>Unknown cause of transmission</summary>
        public const byte COT_UNKNOWN_COT = 45;

        /// <summary>Unknown common address</summary>
        public const byte COT_UNKNOWN_CA = 46;

        /// <summary>Unknown information object address</summary>
        public const byte COT_UNKNOWN_IOA = 47;

        #endregion

        #region QUALIFIER OF INTERROGATION

        /// <summary>Station interrogation (global)</summary>
        public const byte QOI_STATION = 20;

        /// <summary>Group 1 interrogation</summary>
        public const byte QOI_GROUP_1 = 21;

        /// <summary>Group 2 interrogation</summary>
        public const byte QOI_GROUP_2 = 22;

        /// <summary>Group 3 interrogation</summary>
        public const byte QOI_GROUP_3 = 23;

        /// <summary>Group 4 interrogation</summary>
        public const byte QOI_GROUP_4 = 24;

        #endregion

        #region QUALITY DESCRIPTOR BITS

        /// <summary>Overflow bit</summary>
        public const byte QDS_OVERFLOW = 0x01;

        /// <summary>Blocked bit</summary>
        public const byte QDS_BLOCKED = 0x10;

        /// <summary>Substituted bit</summary>
        public const byte QDS_SUBSTITUTED = 0x20;

        /// <summary>Not topical bit</summary>
        public const byte QDS_NOT_TOPICAL = 0x40;

        /// <summary>Invalid bit</summary>
        public const byte QDS_INVALID = 0x80;

        #endregion

        #region PROTOCOL PARAMETERS

        /// <summary>Default value for k parameter (max difference send/receive)</summary>
        public const ushort DEFAULT_K = 12;

        /// <summary>Default value for w parameter (acknowledge window)</summary>
        public const ushort DEFAULT_W = 8;

        /// <summary>Default value for t0 timeout (connection timeout)</summary>
        public const ushort DEFAULT_T0 = 30;

        /// <summary>Default value for t1 timeout (send timeout)</summary>
        public const ushort DEFAULT_T1 = 15;

        /// <summary>Default value for t2 timeout (acknowledge timeout)</summary>
        public const ushort DEFAULT_T2 = 10;

        /// <summary>Default value for t3 timeout (test frame timeout)</summary>
        public const ushort DEFAULT_T3 = 20;

        #endregion

        #region RESULT CODES - FIXED

        /// <summary>Operation successful</summary>
        public const int RESULT_OK = 0;

        /// <summary>No error - same as RESULT_OK for compatibility</summary>
        public const int ERROR_NONE = 0;

        /// <summary>General error</summary>
        public const int RESULT_ERROR = -1;

        /// <summary>Connection timeout</summary>
        public const int ERR_CONNECTION_TIMEOUT = 0x0100;

        /// <summary>Connection failed</summary>
        public const int ERR_CONNECTION_FAILED = 0x0200;

        /// <summary>Send timeout</summary>
        public const int ERR_SEND_TIMEOUT = 0x0300;

        /// <summary>Receive timeout</summary>
        public const int ERR_RECEIVE_TIMEOUT = 0x0400;

        /// <summary>Invalid frame format</summary>
        public const int ERR_INVALID_FRAME = 0x0500;

        /// <summary>STARTDT failed</summary>
        public const int ERR_STARTDT_FAILED = 0x0600;

        /// <summary>STOPDT failed</summary>
        public const int ERR_STOPDT_FAILED = 0x0700;

        /// <summary>Test frame failed</summary>
        public const int ERR_TESTFR_FAILED = 0x0800;

        /// <summary>Sequence number error</summary>
        public const int ERR_SEQUENCE_ERROR = 0x0900;

        /// <summary>Not connected</summary>
        public const int ERR_NOT_CONNECTED = 0x0A00;

        #endregion

        #region DOUBLE POINT VALUES

        /// <summary>Double point indeterminate or intermediate state</summary>
        public const byte DPI_INDETERMINATE = 0;

        /// <summary>Double point OFF</summary>
        public const byte DPI_OFF = 1;

        /// <summary>Double point ON</summary>
        public const byte DPI_ON = 2;

        /// <summary>Double point indeterminate state</summary>
        public const byte DPI_INDETERMINATE_2 = 3;

        #endregion

        #region STEP COMMAND VALUES

        /// <summary>Step command not permitted</summary>
        public const byte RCS_NOT_PERMITTED = 0;

        /// <summary>Step command LOWER</summary>
        public const byte RCS_LOWER = 1;

        /// <summary>Step command HIGHER</summary>
        public const byte RCS_HIGHER = 2;

        /// <summary>Step command not permitted</summary>
        public const byte RCS_NOT_PERMITTED_2 = 3;

        #endregion

        #region COMMAND QUALIFIER BITS - FIXED

        /// <summary>Select/Execute bit - Execute</summary>
        public const byte SE_EXECUTE = 0x00;

        /// <summary>Select/Execute bit - Select</summary>
        public const byte SE_SELECT = 0x80;

        /// <summary>Qualifier of command - No additional definition</summary>
        public const byte QU_NO_ADDITIONAL = 0;

        /// <summary>Qualifier of command - Short pulse duration</summary>
        public const byte QU_SHORT_PULSE = 1;

        /// <summary>Qualifier of command - Long pulse duration</summary>
        public const byte QU_LONG_PULSE = 2;

        /// <summary>Qualifier of command - Persistent output</summary>
        public const byte QU_PERSISTENT = 3;

        #endregion

        #region COUNTER INTERROGATION QUALIFIER

        /// <summary>General counter request</summary>
        public const byte QCC_RQT_GENERAL = 5;

        /// <summary>Request counter group 1</summary>
        public const byte QCC_RQT_GROUP_1 = 1;

        /// <summary>Request counter group 2</summary>
        public const byte QCC_RQT_GROUP_2 = 2;

        /// <summary>Request counter group 3</summary>
        public const byte QCC_RQT_GROUP_3 = 3;

        /// <summary>Request counter group 4</summary>
        public const byte QCC_RQT_GROUP_4 = 4;

        #endregion
    }
}