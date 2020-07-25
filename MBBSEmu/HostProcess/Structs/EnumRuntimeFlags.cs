using System;

namespace MBBSEmu.HostProcess.Structs
{

    /// <summary>
    ///     Runtime Flags for MajorBBS, defined in MAJORBBS.H
    /// </summary>
    [Flags]
    public enum EnumRuntimeFlags
    {
        /// <summary>
        ///     unable to receive injoth() msgs
        /// </summary>
        Noinjo = 1, //0x00000001L

        /// <summary>
        ///     injoth() operation in progress
        /// </summary>
        Injoip = 1 << 1, //0x00000002L

        /// <summary>
        ///     going away, just wait for msg done
        /// </summary>
        Byebye = 1 << 2, //0x00000004L

        /// <summary>
        ///     no hardware, emulation chan only
        /// </summary>
        Nohdwe = 1 << 3, //0x00000008L

        /// <summary>
        ///     chatting-with-sysop flag
        /// </summary>
        Opchat = 1 << 4, //0x00000010L

        /// <summary>
        ///     channel-is-active (gen'ing status)
        /// </summary>
        Active = 1 << 5, //0x00000020L

        /// <summary>
        ///     user has the "master key" flag
        /// </summary>
        Master = 1 << 6, //0x00000040L

        /// <summary>
        ///     channel-is-2698 (XE2400) 
        /// </summary>
        Is2698 = 1 << 7, //0x00000080L

        /// <summary>
        ///     don't logoff freeloader flag
        /// </summary>
        Nozap = 1 << 8, //0x00000100L

        /// <summary>
        ///     channel-is-serial-port
        /// </summary>
        Isrial = 1 << 9, //0x00000200L

        /// <summary>
        ///     exit-to-main-menu when sysop chat
        /// </summary>
        X2main = 1 << 10, //0x00000400L

        /// <summary>
        ///     commands concatenated -- exit fast
        /// </summary>
        Concex = 1 << 11, //0x00000800L

        /// <summary>
        ///     channel-is-X.25
        /// </summary>
        Isx25 = 1 << 12, //0x00001000L

        /// <summary>
        ///     don't interpret global commands
        /// </summary>
        Noglob = 1 << 13, //0x00002000L

        /// <summary>
        ///     user is "invisible" to others
        /// </summary>
        Invisb = 1 << 14, //0x00004000L

        /// <summary>
        ///     abort output in-progress
        /// </summary>
        Aboip = 1 << 15, //0x00008000L

        /// <summary>
        ///     monitor all in progress
        /// </summary>
        Monall = 1 << 16, //0x00010000L

        /// <summary>
        ///     monitor input in progress 
        /// </summary>
        Monip = 1 << 17, //0x00020000L

        /// <summary>
        ///     got one-min. warning of logoff
        /// </summary>
        Onemin = 1 << 18, //0x00040000L

        /// <summary>
        ///     real hardware isn't 16550
        /// </summary>
        Non550 = 1 << 19, //0x00080000L

        /// <summary>
        ///     Model 16 or 4, using XECOM 1201's
        /// </summary>
        Is1201 = 1 << 20, //0x00100000L

        /// <summary>
        ///     GCDI channel
        /// </summary>
        Isgcdi = 1 << 21, //0x00200000L

        /// <summary>
        ///     Hide input from the monitor screen 
        /// </summary>
        Monmhid = 1 << 22, //0x00400000L

        /// <summary>
        ///     user is a C/S client 
        /// </summary>
        Isgcsu = 1 << 23, //0x00800000L

        /// <summary>
        ///     user was C/S client (now in ASCII)
        /// </summary>
        Wsgcsu = 1 << 24, //0x01000000L

        /// <summary>
        ///     C/S logon routines have been called
        /// </summary>
        Gcslon = 1 << 25, //0x02000000L

        /// <summary>
        ///     C/S signup acct info being recorded
        /// </summary>
        Gcsdun = 1 << 26 //0x04000000L
    }
}
