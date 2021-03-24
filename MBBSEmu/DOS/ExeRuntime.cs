using MBBSEmu.CPU;
using MBBSEmu.Date;
using MBBSEmu.Disassembler;
using MBBSEmu.DOS.Interrupts;
using MBBSEmu.DOS.Structs;
using MBBSEmu.IO;
using MBBSEmu.Memory;
using System;
using System.IO;
using System.Collections.Generic;
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

        private ushort _pspSegment => (ushort)(_programRealModeLoadAddress.Segment - 16); // 256 bytes less
        private ushort _environmentSegment => (ushort)(_pspSegment - (4096 / 16)); // 4k less

        private const int PROGRAM_MAXIMUM_ADDRESS = (0xA000 << 4);

        private readonly Dictionary<string, string> _environmentVariables = new();

        public ExeRuntime(MZFile file, IClock clock, ILogger logger, IFileUtility fileUtility)
        {
            _logger = logger;
            File = file;
            Memory = new RealModeMemoryCore(logger);
            Cpu = new CpuCore(_logger);
            Registers = new CpuRegisters();
            Cpu.Reset(Memory, Registers, null, new List<IInterruptHandler> { new Int21h(Registers, Memory, clock, _logger, fileUtility, Console.In, Console.Out, Console.Error, Environment.CurrentDirectory), new Int1Ah(Registers, Memory, clock), new Int3Eh() });
        }

        public bool Load(string[] args)
        {
            CreateEnvironmentVariables(args);
            LoadProgramIntoMemory();
            ApplyRelocation();
            SetupPSP(args);
            SetupEnvironmentVariables();
            SetSegmentRegisters();
            return true;
        }

        public void Run()
        {
            while (!Registers.Halt)
                Cpu.Tick();
        }

        private string CreateCommandLine()
        {
            var exeName = Path.GetFileName(File.ExeFile);
            return $"C:\\BBSV6\\{exeName}";
        }

        private void CreateEnvironmentVariables(string[] args)
        {
            _environmentVariables["CMDLINE"] = CreateCommandLine() + ' ' + String.Join(' ', args);
            _environmentVariables["COMSPEC"] = "C:\\COMMAND.COM";
            _environmentVariables["COPYCMD"] = "COPY";
            _environmentVariables["DIRCMD"] = "DIR";
            _environmentVariables["PATH"] = "C:\\DOS;C:\\BBSV6";
            _environmentVariables["TMP"] = "C:\\TEMP";
            _environmentVariables["TEMP"] = "C:\\TEMP";
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

        private void SetSegmentRegisters()
        {
            Registers.CS = (ushort)(File.Header.InitialCS + _programRealModeLoadAddress.Segment);
            Registers.IP = File.Header.InitialIP;

            Registers.SS = (ushort)(File.Header.InitialSS + _programRealModeLoadAddress.Segment);
            Registers.SP = File.Header.InitialSP;

            Registers.ES = _pspSegment;
            Registers.DS = _pspSegment;
        }

        /// <summary>
        ///     Sets up PSP for the program Execution
        /// </summary>
        private void SetupPSP(string[] args)
        {
            var cmdLine = String.Join(' ', args);
            // maximum 126 characters, thanks to DOS
            if (cmdLine.Length > 126)
                cmdLine = cmdLine.Substring(0, 126);

            var psp = new PSPStruct { NextSegOffset = 0xE000, EnvSeg = _environmentSegment, CommandTailLength = (byte)cmdLine.Length };
            Array.Copy(Encoding.ASCII.GetBytes(cmdLine), 0, psp.CommandTail, 0, cmdLine.Length);

            Memory.SetArray(_pspSegment, 0, psp.Data);

            Memory.AllocateVariable("Int21h-PSP", sizeof(ushort));
            Memory.SetWord("Int21h-PSP", _pspSegment);
        }

        /// <summary>
        ///     Copies any Environment Variables to the Environment Variables Segment
        /// </summary>
        private void SetupEnvironmentVariables()
        {
            ushort bytesWritten = 0;
            foreach (var v in _environmentVariables)
            {
                string str = v.Key + "=" + v.Value + "\0";
                Memory.SetArray(_environmentSegment, bytesWritten, Encoding.ASCII.GetBytes(str));
                bytesWritten += (ushort)(str.Length);
            }
            // null terminate
            Memory.SetByte(_environmentSegment, bytesWritten++, 0);
            Memory.SetByte(_environmentSegment, bytesWritten++, 1);
            Memory.SetByte(_environmentSegment, bytesWritten++, 0);

            //Add EXE
            Memory.SetArray(_environmentSegment, bytesWritten, Encoding.ASCII.GetBytes(CreateCommandLine() + "\0"));
        }
    }
}
