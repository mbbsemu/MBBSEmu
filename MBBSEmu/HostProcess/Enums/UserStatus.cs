namespace MBBSEmu.HostProcess.Enums
{
    /// <summary>
    /// User Status values, mostly taken from BRKTHU.H
    /// </summary>
    public enum UserStatus
    {
        UNUSED = 0, // default unused value
        RING = 1, // ringing/lost carrier
        CMDOK = 2, // command completed OK
        CRSTG = 3, // CR-term'd string available
        INBLK = 4, // count-trg input available
        OUTMT = 5, // output buffer empty
        OBFCLR = 6, // output buffer cleared
        ABOREQ = 7, // abort output requested
        LOST2C = 11, // 2400 baud lost-carrier - lol
        CMN2OK = 12, // 2400 baud cmd complete
        CMN2NG = 13, // uart/hayes cmd byte not executable
        LOST25 = 21, // X.25 lost-carrier
        CM25OK = 22, //  X.25 cmd complete
        RCVX29 = 24, //  X.29 string received
        SPXTRM = 31, //  SPX terminate by other side
        IPXPSE = 32, //  IPX/SPX pause complete
        SPXINC = 34, //  SPX incoming call complete
        SPXOUT = 35, //  SPX outgoing call complete
        SPXTDN = 36, //  SPX termination complete
        IPXRER = 37, //  IPX recv error (chan group)
        IPXUNK = 38, //  IPX unknown packet (chan grp)
        SPXWDG = 39, //  SPX watchdog error, etc
        INAPP = 'I', // Inappropriate status (fm ^H cmd if 300bd)

        // status codes 200-249 are app-specific
        // status codes 250-255 are buffer overflow
        POLSTS = 192,
        CYCLE = 240,
    };
}
