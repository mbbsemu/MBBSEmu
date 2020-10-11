using System;
namespace MBBSEmu.CPU
{
    /// <summary>
    ///     Flags for the x87 FPU Control Word
    /// </summary>
    [Flags]
    public enum EnumFpuControlWordFlags : ushort
    {
       InvalidOperation = 1,
       DenomalOperand = 1 << 1,
       ZeroDivide = 1 << 2,
       Overflow = 1 << 3,
       Underflow = 1 << 4,
       Precision = 1 << 5
    }
}
