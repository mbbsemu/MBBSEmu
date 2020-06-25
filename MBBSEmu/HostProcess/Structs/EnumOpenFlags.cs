using System;

namespace MBBSEmu.HostProcess.Structs
{
    [Flags]
    public enum EnumOpenFlags : ushort
    {

        O_RDONLY = 1,
        O_WRONLY = 1 << 1,
        O_RDRW = 1 << 2,

        /// <summary>
        /// create and open file
        /// </summary>
        O_CREAT = 1 << 8,

        /// <summary>
        /// open with truncation
        /// </summary>
        O_TRUNC = 1 << 9,

        /// <summary>
        /// exclusive open
        /// </summary>
        O_EXCL = 1 << 10,

        O_CHANGED = 1 << 12,
        O_DEVICE = 1 << 13,

        /// <summary>
        /// CR-LF translation
        /// </summary>
        O_TEXT = 1 << 14,

        //no translation
        O_BINARY = 1 << 15
    }
}
