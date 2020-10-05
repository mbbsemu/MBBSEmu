using System;

namespace MBBSEmu.CPU
{
    /// <summary>
    ///     x87 FPU Status Register Flags
    /// </summary>
    [Flags]
    public enum EnumFpuStatusFlags : ushort
    {
        InvalidOperationException = 1,
        DenormalizedOperandException = 1 << 1,
        ZeroDivideException = 1 << 2,
        OverflowException = 1 << 3,
        UnderflowException = 1 << 4,
        PrecisionException = 1 << 5,
        StackFault = 1 << 6,
        ErrorSummaryStatus = 1 << 7,
        Code0 = 1 << 8,
        Code1 = 1 << 9,
        Code2 = 1 << 10,
        Code3 = 1 << 14,
        FPUBusy = 1 << 15
    }
}