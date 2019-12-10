using System;

namespace MBBSEmu.CPU
{
    /// <summary>
    ///     Enumerator to represent the Intel x86 FLAGS Register
    ///
    ///     More Info: https://en.wikipedia.org/wiki/FLAGS_register
    /// </summary>
    [Flags]
    public enum EnumFlags : ushort
    {
        /// <summary>
        ///     Carry Flag
        /// </summary>
        CF = 1,

        /// <summary>
        ///     Parity Flag
        /// </summary>
        PF = 1 << 2,

        /// <summary>
        ///     Adjust Flag
        /// </summary>
        AF = 1 << 4,

        /// <summary>
        ///     Zero Flag
        /// </summary>
        ZF = 1 << 6,

        /// <summary>
        ///     Sign Flag
        /// </summary>
        SF = 1 << 7,

        /// <summary>
        ///     Trap Flag
        /// </summary>
        TF = 1 << 8,

        /// <summary>
        ///     Interrupt Enable Flag
        /// </summary>
        IF = 1 << 9,

        /// <summary>
        ///     Direction Flag
        /// </summary>
        DF = 1 << 10,

        /// <summary>
        ///     Overflow Flag
        /// </summary>
        OF = 1 << 11,

        /// <summary>
        ///     I/O Privilege Level
        /// </summary>
        IOPL1 = 1 << 12,

        /// <summary>
        ///     I/O Privilege Level
        /// </summary>
        IOPL2 = 1 << 13,

        /// <summary>
        ///     Nested Task Flag
        /// </summary>
        NT = 1 << 14
    }
}
