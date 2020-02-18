using System;
using System.Collections.Generic;
using MBBSEmu.CPU;
using MBBSEmu.HostProcess.ExportedModules;
using MBBSEmu.Memory;

namespace MBBSEmu.HostProcess.ExecutionUnits
{
    /// <summary>
    ///     Represents a single execution unit, everything that is required for a portion of code within a module
    ///     to be executed, including CPU, Memory, Registers, and Module Exports
    /// </summary>
    public class ExecutionUnit
    {
        /// <summary>
        ///     Module dedicated CPU Core
        /// </summary>
        public readonly ICpuCore ModuleCpu;

        /// <summary>
        ///     Module dedicated CPU Registers
        /// </summary>
        public readonly CpuRegisters ModuleCpuRegisters;

        /// <summary>
        ///     Module Memory Space
        /// </summary>
        public readonly IMemoryCore ModuleMemory;

        /// <summary>
        ///     Exported Modules to be called from the CPU
        /// </summary>
        public readonly Dictionary<ushort, IExportedModule> ExportedModuleDictionary;

        public ExecutionUnit(IMemoryCore moduleMemory, Dictionary<ushort, IExportedModule> exportedModuleDictionary)
        {
            ModuleCpu = new CpuCore();
            ModuleCpuRegisters = new CpuRegisters();
            ModuleMemory = moduleMemory;
            ExportedModuleDictionary = exportedModuleDictionary;

            ModuleCpu.Reset(ModuleMemory, ModuleCpuRegisters, ExternalFunctionDelegate);
        }

        private ReadOnlySpan<byte> ExternalFunctionDelegate(ushort ordinal, ushort functionOrdinal)
        {
            if (!ExportedModuleDictionary.TryGetValue(ordinal, out var exportedModule))
                throw new Exception(
                    $"Unknown or Unimplemented Imported Module: {ordinal:X4}");

            return exportedModule.Invoke(functionOrdinal);
        }

        public CpuRegisters Execute(IntPtr16 entryPoint, ushort channelNumber, bool simulateCallFar = false, bool bypassState = false, Queue<ushort> initialStackValues = null)
        {
            //Reset Registers to Startup State for the CPU
            ModuleCpu.Reset();

            //Reset Registers
            ModuleCpuRegisters.CS = entryPoint.Segment;
            ModuleCpuRegisters.IP = entryPoint.Offset;

            //Any parameters that need to be passed into the function
            if (initialStackValues != null)
            {
                //Push Parameters
                while (initialStackValues.TryDequeue(out var valueToPush))
                    ModuleCpu.Push(valueToPush);
            }

            //Simulating a CALL FAR
            if (simulateCallFar)
            {
                //Set stack to simulate CALL FAR
                ModuleCpuRegisters.BP = ModuleCpuRegisters.SP;
                ModuleCpu.Push(ushort.MaxValue); //CS
                ModuleCpu.Push(ushort.MaxValue); //IP
            }

            //Things like TEXT_VARIABLES don't need us to re-setup the state, the Exported Functions are already setup properly
            if(!bypassState)
                foreach (var em in ExportedModuleDictionary.Values)
                    em.SetState(ModuleCpuRegisters, channelNumber);

            //Run until complete
            while (!ModuleCpuRegisters.Halt)
                ModuleCpu.Tick();

            //Update Session State
            if (!bypassState && channelNumber != ushort.MaxValue && initialStackValues == null)
                ExportedModuleDictionary[Majorbbs.Segment].UpdateSession(channelNumber);

            //Return Registers
            return ModuleCpuRegisters;
        }
    }
}
