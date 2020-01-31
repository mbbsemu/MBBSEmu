using MBBSEmu.Memory;

namespace MBBSEmu.CPU
{
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

        void Reset();
        void Tick();

        void Push(ushort value);
    }
}