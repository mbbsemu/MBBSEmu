using System;
using System.Collections.Generic;
using MBBSEmu.CPU;
using MBBSEmu.HostProcess.ExportedModules;
using MBBSEmu.Memory;

namespace MBBSEmu.HostProcess.ExecutionUnits
{
    public class ExecutionUnit
    {
        /// <summary>
        ///     Module dedicated CPU Core
        /// </summary>
        public ICpuCore ModuleCpu { get; set; }

        /// <summary>
        ///     Module dedicated CPU Registers
        /// </summary>
        public CpuRegisters ModuleCpuRegisters { get; set; }

        /// <summary>
        ///     Module Memory Space
        /// </summary>
        public IMemoryCore ModuleMemory { get; set; }

        /// <summary>
        ///     Exported Modules to be called from the CPU
        /// </summary>
        public Dictionary<ushort, IExportedModule> ExportedModuleDictionary { get; set; }

        public ExecutionUnit(IMemoryCore moduleMemory)
        {
            ModuleCpu = new CpuCore();
            ModuleCpuRegisters = new CpuRegisters();
            ModuleMemory = moduleMemory;
            ExportedModuleDictionary = new Dictionary<ushort, IExportedModule>();

            ModuleCpu.Reset(ModuleMemory, ModuleCpuRegisters, ExternalFunctionDelegate);
        }

        private ReadOnlySpan<byte> ExternalFunctionDelegate(ushort ordinal, ushort functionOrdinal)
        {
            if (!ExportedModuleDictionary.TryGetValue(ordinal, out var exportedModule))
                throw new Exception(
                    $"Unknown or Unimplemented Imported Module: {ordinal:X4}");

            return exportedModule.Invoke(functionOrdinal);
        }

        public CpuRegisters Execute(IntPtr16 entryPoint, ushort channelNumber, bool simulateCallFar = false, Queue<ushort> initialStackValues = null)
        {
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

            //Setup the state of the exported functions
            foreach (var em in ExportedModuleDictionary.Values)
                em.SetState(ModuleCpuRegisters, channelNumber);

            //Run until complete
            while (!ModuleCpuRegisters.Halt)
                ModuleCpu.Tick();

            //Return Registers
            return ModuleCpuRegisters;
        }
    }
}
