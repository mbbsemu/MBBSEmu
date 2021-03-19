using MBBSEmu.CPU;
using MBBSEmu.Date;
using MBBSEmu.Disassembler;
using MBBSEmu.Disassembler.Artifacts;
using MBBSEmu.DOS.Interrupts;
using MBBSEmu.DOS.Structs;
using MBBSEmu.Memory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NLog;

namespace MBBSEmu.DOS
{
    public class ExeRuntime
    {
        public MZFile File;
        public IMemoryCore Memory;
        public ICpuCore Cpu;
        public CpuRegisters Registers;
        private ILogger _logger;
        private FarPtr _programRealModeLoadAddress;

        private const int PROGRAM_MAXIMUM_ADDRESS = (0xA000 << 4);
        private const ushort PSP_SEGMENT = 0x2000;

        /// <summary>
        ///     Segment which holds the Environment Variables
        /// </summary>
        private const ushort ENVIRONMENT_SEGMENT = 0x2010;

        private readonly List<string> _environmentVariables;

        public ExeRuntime(MZFile file, List<string> environmentVariables, IClock clock, ILogger logger)
        {
            _logger = logger;
            File = file;
            Memory = new RealModeMemoryCore(logger);
            Cpu = new CpuCore(_logger);
            Registers = new CpuRegisters();
            Cpu.Reset(Memory, Registers, null, new List<IInterruptHandler> { new Int21h(Registers, Memory, clock, _logger, Console.In, Console.Out, Console.Error, Environment.CurrentDirectory), new Int1Ah(Registers, Memory, clock), new Int3Eh() });
            _environmentVariables = environmentVariables;

            Registers.ES = PSP_SEGMENT;
            Registers.DS = PSP_SEGMENT;
        }

        public bool Load()
        {
            LoadProgramIntoMemory();
            ApplyRelocation();
            SetupPSP();
            SetupEnvironmentVariables();
            CompileEntryPoint();
            return true;
        }

        public void Run()
        {
            while (!Registers.Halt)
                Cpu.Tick();
        }

        private void LoadProgramIntoMemory()
        {
            // compute load address
            var startingAddress = PROGRAM_MAXIMUM_ADDRESS - File.Header.ProgramSize;

            _programRealModeLoadAddress = RealModeMemoryCore.PhysicalToVirtualAddress(startingAddress);
            // might not be aligned to 16 bytes, so align cleanly
            _programRealModeLoadAddress.Offset = 0;

            Memory.SetArray(_programRealModeLoadAddress, File.ProgramData);
        }

        /// <summary>
        ///     Applies Relocation Records to the EXE using _programRealModeLoadAddress
        /// </summary>
        private void ApplyRelocation()
        {
            foreach (var relo in File.RelocationRecords)
            {
                var relocVirtualAddress = _programRealModeLoadAddress + relo;

                var inMemoryVirtualAddress = Memory.GetWord(relocVirtualAddress);
#if DEBUG
                // sanity check against overflows
                int v = inMemoryVirtualAddress + _programRealModeLoadAddress.Segment;
                if (v > 0xFFFF)
                    throw new ArgumentException("Relocated segment overflowed");
#endif
                inMemoryVirtualAddress += _programRealModeLoadAddress.Segment;

                Memory.SetWord(relocVirtualAddress, inMemoryVirtualAddress);
            }
        }

        private void CompileEntryPoint()
        {
            Registers.CS = (ushort)(File.Header.InitialCS + _programRealModeLoadAddress.Segment);
            Registers.IP = File.Header.InitialIP;

            // and compile/cache instructions
            //Memory.Recompile(Registers.CS, Registers.IP);

            Registers.SS = (ushort)(_programRealModeLoadAddress.Segment - 1 - (File.Header.InitialSP >> 4));
            Registers.SP = File.Header.InitialSP;
        }

        /// <summary>
        ///     Sets up PSP for the program Execution
        /// </summary>
        private void SetupPSP()
        {
            var psp = new PSPStruct { NextSegOffset = 0xA000, EnvSeg = ENVIRONMENT_SEGMENT };
            Memory.SetArray(PSP_SEGMENT, 0, psp.Data);

            Memory.AllocateVariable("Int21h-PSP", sizeof(ushort));
            Memory.SetWord("Int21h-PSP", PSP_SEGMENT);
        }

        /// <summary>
        ///     Copies any Environment Variables to the Environment Variables Segment
        /// </summary>
        private void SetupEnvironmentVariables()
        {
            if (_environmentVariables == null)
                return;

            ushort bytesWritten = 0;
            foreach (var v in _environmentVariables)
            {
                Memory.SetArray(ENVIRONMENT_SEGMENT, bytesWritten, Encoding.ASCII.GetBytes(v));
                bytesWritten += (ushort)(v.Length + 1);
            }
        }
    }
}
