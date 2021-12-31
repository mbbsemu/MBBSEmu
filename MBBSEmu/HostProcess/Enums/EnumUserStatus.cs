namespace MBBSEmu.HostProcess.Enums
{
    /// <summary>/ <summary>
    /// <summary>/ User Status values, mostly taken from BRKTHU.H
    /// <summary>/ </summary>
    public enum EnumUserStatus : ushort
    {
        /// <summary>Default unused value</summary>
        UNUSED = 0,
        /// <summary>ringing/lost carrier</summary>
        RINGING = 1,
        /// <summary>command completed OK</summary>
        COMMAND_COMPLETED_OK = 2,
        /// <summary>CR-term'd string available</summary>
        CR_TERMINATED_STRING_AVAILABLE = 3,
        /// <summary>count-trg input available</summary>
        INBLK = 4,
        /// <summary>output buffer empty</summary>
        OUTPUT_BUFFER_EMPTY = 5,
        /// <summary>output buffer cleared</summary>
        OUTPUT_BUFFER_CLEARED = 6,
        /// <summary>abort output requested</summary>
        ABORT_OUTPUT_REQUESTED = 7,
        /// <summary>2400 baud lost-carrier - lol</summary>
        LOST_CARRIER_2400_BAUD = 11,
        /// <summary>2400 baud cmd complete</summary>
        COMMAND_COMPLETE_2400_BAUD = 12,
        /// <summary>uart/hayes cmd byte not executable</summary>
        CMN2NG = 13,
        /// <summary>X.25 lost-carrier</summary>
        LOST25 = 21,
        /// <summary> X.25 cmd complete</summary>
        CM25OK = 22,
        /// <summary> X.29 string received</summary>
        RCVX29 = 24,
        /// <summary> SPX terminate by other side</summary>
        SPXTRM = 31,
        /// <summary> IPX/SPX pause complete</summary>
        IPXPSE = 32,
        /// <summary> SPX incoming call complete</summary>
        SPXINC = 34,
        /// <summary> SPX outgoing call complete</summary>
        SPXOUT = 35,
        /// <summary> SPX termination complete</summary>
        SPXTDN = 36,
        /// <summary> IPX recv error (chan group)</summary>
        IPXRER = 37,
        /// <summary> IPX unknown packet (chan grp)</summary>
        IPXUNK = 38,
        /// <summary> SPX watchdog error, etc</summary>
        SPXWDG = 39,
        /// <summary>Inappropriate status (fm ^H cmd if 300bd)</summary>
        INAPPROPRIATE_STATUS = 'I',

        /**
          * status codes 200-249 are app-specific
          * status codes 250-255 are buffer overflow
          */

        /// <summary>Polling status</summary>
        POLLING_STATUS = 192,
        /// <summary>Cycle status</summary>
        CYCLE = 240,
    };
}
