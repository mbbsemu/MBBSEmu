using System.Collections.Generic;
using MBBSEmu.DOS.Interrupts;
using MBBSEmu.Memory;
using System;

namespace MBBSEmu.CPU
{
    /// <summary>
    ///     Interface for CpuCore
    /// </summary>
    public interface ICpuCore : ICpuRegisters, IDisposable
    {
        /// <summary>
        ///     Resets the CPU back to a starting state
        /// </summary>
        /// <param name="memoryCore"></param>
        /// <param name="invokeExternalFunctionDelegate"></param>
        /// <param name="interruptHandlers"></param>
        /// <param name="ioPortHandlers"></param>
        void Reset(IMemoryCore memoryCore, CpuCore.InvokeExternalFunctionDelegate invokeExternalFunctionDelegate, IEnumerable<IInterruptHandler> interruptHandlers, IDictionary<int, IIOPort> ioPortHandlers);

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

        void Interrupt(byte vectorNumber);
    }
}
