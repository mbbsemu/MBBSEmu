using MBBSEmu.Memory;

namespace MBBSEmu.CPU
{
    /// <summary>
    ///     Interface for CpuCore
    /// </summary>
    public interface ICpuCore
    {
        /// <summary>
        ///     Resets the CPU back to a starting state
        /// </summary>
        /// <param name="memoryCore"></param>
        /// <param name="cpuRegisters"></param>
        /// <param name="invokeExternalFunctionDelegate"></param>
        void Reset(IMemoryCore memoryCore, CpuRegisters cpuRegisters,
            CpuCore.InvokeExternalFunctionDelegate invokeExternalFunctionDelegate);

        /// <summary>
        ///     Resets the CPU to a startup state
        /// </summary>
        void Reset();

        /// <summary>
        ///     Resets the CPU to a startup state with the specified Base Pointer (BP) value
        /// </summary>
        /// <param name="stackBase"></param>
        void Reset(ushort stackBase);

        /// <summary>
        ///     Ticks the CPU one instruction
        /// </summary>
        void Tick();

        /// <summary>
        ///     Pushes the given word to the Stack
        /// </summary>
        /// <param name="value"></param>
        void Push(ushort value);
    }
}