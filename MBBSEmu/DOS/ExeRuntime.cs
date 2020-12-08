using MBBSEmu.CPU;
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

        /// <summary>
        ///     Segment which holds the Environment Variables
        /// </summary>
        private const ushort ENVIRONMENT_SEGMENT = 0xFFF0;

        /// <summary>
        ///     Segment which contains the PSP Struct
        ///
        ///     Normally +10h beyond the last segment of the EXE
        /// </summary>
        private readonly ushort PSP_SEGMENT;

        private readonly List<string> _environmentVariables;

        public ExeRuntime(MZFile file, List<string> environmentVariables, ILogger logger)
        {
            _logger = logger;
            File = file;
            Memory = new MemoryCore();
            Cpu = new CpuCore(_logger);
            Registers = new CpuRegisters();
            Cpu.Reset(Memory, Registers, null, new List<IInterruptHandler> { new Int21h(Registers, Memory), new Int1Ah(Registers, Memory) });
            _environmentVariables = environmentVariables;
            PSP_SEGMENT = (ushort)(file.Segments.Count + 0x10);

            Registers.IP = 0;
            Registers.CS = 1;
            Registers.ES = PSP_SEGMENT;
            Registers.DS = PSP_SEGMENT;
        }

        public bool Load()
        {
            ApplyRelocation();
            LoadSegments();
            SetupPSP();
            SetupEnvironmentVariables();
            return true;
        }

        public void Run()
        {
            while (!Registers.Halt)
                Cpu.Tick();
        }

        /// <summary>
        ///     Applies Relocation Records to the EXE Code Segments
        /// </summary>
        private void ApplyRelocation()
        {
            //Get the Code Segment in the MZFile
            var codeSegment = File.Segments.First(x => x.Flag == (ushort)EnumSegmentFlags.Code).Data;

            //For the time being, only handle the 1st relo for the data segment
            foreach (var relo in File.RelocationRecords)
            {
                //Data Segment is always the last one
                Array.Copy(BitConverter.GetBytes((ushort)File.Segments.Count), 0, codeSegment, relo.Offset,
                    sizeof(ushort));
            }
        }

        /// <summary>
        ///     Loads EXE Code Segments into the Memory Core
        /// </summary>
        private void LoadSegments()
        {
            for (ushort i = 1; i <= File.Segments.Count; i++)
            {
                var seg = File.Segments[i - 1];
                switch (seg.Flag)
                {
                    case (ushort)EnumSegmentFlags.Code:
                        {
                            Memory.AddSegment(seg);
                            break;
                        }
                    case (ushort)EnumSegmentFlags.Data:
                        {
                            Memory.AddSegment(i);
                            Memory.SetArray(i, 0, seg.Data);
                            break;
                        }
                }
            }
        }

        /// <summary>
        ///     Sets up PSP for the program Execution
        /// </summary>
        private void SetupPSP()
        {
            var psp = new PSPStruct { NextSegOffset = 0x9FFF, EnvSeg = ENVIRONMENT_SEGMENT };
            Memory.AddSegment(PSP_SEGMENT);
            Memory.SetArray(PSP_SEGMENT, 0, psp.Data);

            Memory.AllocateVariable("Int21h-PSP", sizeof(ushort));
            Memory.SetWord("Int21h-PSP", PSP_SEGMENT);
        }

        /// <summary>
        ///     Copies any Environment Variables to the Environment Variables Segment
        /// </summary>
        private void SetupEnvironmentVariables()
        {
            Memory.AddSegment(ENVIRONMENT_SEGMENT);

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
