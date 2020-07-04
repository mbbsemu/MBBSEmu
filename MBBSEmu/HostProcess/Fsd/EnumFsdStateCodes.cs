namespace MBBSEmu.HostProcess.Fsd
{
    /// <summary>
    ///     FSD State Codes
    ///
    ///     FSD.H
    /// </summary>
    public enum EnumFsdStateCodes : ushort
    {
        /// <summary>
        ///     beginning, ANSI point mode
        /// </summary>
        FSDAPT = 0,

        /// <summary>
        ///     entering stuff, ANSI mode
        /// </summary>
        FSDAEN = 1,

        /// <summary>
        ///     Non-ANSI point mode
        /// </summary>
        FSDNPT = 2,

        /// <summary>
        ///     Non-ANSI entry mode
        /// </summary>
        FSDNEN = 3,

        /// <summary>
        ///     entry ready, buffering new keystrokes
        /// </summary>
        FSDBUF = 4,

        /// <summary>
        ///     entry processed, but still buffering keystrokes
        /// </summary>
        FSDSTB = 5,

        /// <summary>
        ///     need to wipe out help message (ANSI only)
        /// </summary>
        FSDKHP = 6,

        /// <summary>
        ///     exit fsd entry session, save answers
        /// </summary>
        FSDSAV = 7,

        /// <summary>
        ///     exit fsd entry session, toss answers
        /// </summary>
        FSDQIT = 8,

        /// <summary>
        ///     field entry is OK, store it
        /// </summary>
        VFYOK = 0xFFFF, // -1

        /// <summary>
        ///     OK as long as MIN, MAX, ALT check out
        /// </summary>
        VFYCHK = 0xFFFE, // -2

        /// <summary>
        ///     field entry is rejected, reenter
        /// </summary>
        VFYREJ = 0xFFFD, // -3

        /// <summary>
        ///     revert to default field entry & move on
        /// </summary>
        VFYDEF = 0xFFFC // -4
    }
}
