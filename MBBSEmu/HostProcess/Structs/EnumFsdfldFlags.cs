using System;

namespace MBBSEmu.HostProcess.Structs
{
    [Flags]
    public enum EnumFsdfldFlags : byte
    {
        /// <summary>
        ///     multiple choice field
        /// </summary>
        FFFMCH = 1,

        /// <summary>
        ///     field hast at least some alternate values
        /// </summary>
        FFFALT = 1 << 1,

        /// <summary>
        ///     field has a min and/or max in field specs
        /// </summary>
        FFFMMX = 1 << 2,

        /// <summary>
        ///     field does not accept spaces
        /// </summary>
        FFFNSP = 1 << 3,

        /// <summary>
        ///     field has changed this session
        /// </summary>
        FFFCHG = 1 << 4,

        /// <summary>
        ///     no negative numbers allowed
        /// </summary>
        FFFNNG = 1 << 5,

        /// <summary>
        ///     field entry needs to be secret
        /// </summary>
        FFFSEC = 1 << 6,

        /// <summary>
        ///     fields to "avoid" during entry or display
        /// </summary>
        FFFAVD = 1 << 7
    }
}
