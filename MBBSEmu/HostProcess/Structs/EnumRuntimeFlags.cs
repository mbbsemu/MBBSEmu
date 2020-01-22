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
        Noinjo = 1,

        /// <summary>
        ///     injoth() operation in progress
        /// </summary>
        Injoip = 1 << 1,

        /// <summary>
        ///     going away, just wait for msg done
        /// </summary>
        Byebye = 1 << 2,

        /// <summary>
        ///     no hardware, emulation chan only
        /// </summary>
        Nohdwe = 1 << 3,

        /// <summary>
        ///     chatting-with-sysop flag
        /// </summary>
        Opchat = 1 << 4,

        /// <summary>
        ///     channel-is-active (gen'ing status)
        /// </summary>
        Active = 1 << 5,

        /// <summary>
        ///     user has the "master key" flag
        /// </summary>
        Master = 1 << 6,

        /// <summary>
        ///     channel-is-2698 (XE2400) 
        /// </summary>
        Is2698 = 1 << 7,

        /// <summary>
        ///     don't logoff freeloader flag
        /// </summary>
        Nozap = 1 << 8,

        /// <summary>
        ///     channel-is-serial-port
        /// </summary>
        Isrial = 1 << 9,

        /// <summary>
        ///     exit-to-main-menu when sysop chat
        /// </summary>
        X2main = 1 << 10,

        /// <summary>
        ///     commands concatenated -- exit fast
        /// </summary>
        Concex = 1 << 11,

        /// <summary>
        ///     channel-is-X.25
        /// </summary>
        Isx25 = 1 << 12,

        /// <summary>
        ///     don't interpret global commands
        /// </summary>
        Noglob = 1 << 13,

        /// <summary>
        ///     user is "invisible" to others
        /// </summary>
        Invisb = 1 << 14,

        /// <summary>
        ///     abort output in-progress
        /// </summary>
        Aboip = 1 << 15,

        /// <summary>
        ///     monitor all in progress
        /// </summary>
        Monall = 1 << 16,

        /// <summary>
        ///     monitor input in progress 
        /// </summary>
        Monip = 1 << 17,

        /// <summary>
        ///     got one-min. warning of logoff
        /// </summary>
        Onemin = 1 << 18,

        /// <summary>
        ///     real hardware isn't 16550
        /// </summary>
        Non550 = 1 << 19,

        /// <summary>
        ///     Model 16 or 4, using XECOM 1201's
        /// </summary>
        Is1201 = 1 << 20,

        /// <summary>
        ///     GCDI channel
        /// </summary>
        Isgcdi = 1 << 21,

        /// <summary>
        ///     Hide input from the monitor screen 
        /// </summary>
        Monmhid = 1 << 22,

        /// <summary>
        ///     user is a C/S client 
        /// </summary>
        Isgcsu = 1 << 23,

        /// <summary>
        ///     user was C/S client (now in ASCII)
        /// </summary>
        Wsgcsu = 1 << 24,

        /// <summary>
        ///     C/S logon routines have been called
        /// </summary>
        Gcslon = 1 << 25,


        /// <summary>
        ///     C/S signup acct info being recorded
        /// </summary>
        Gcsdun = 1 << 26
    }
}
