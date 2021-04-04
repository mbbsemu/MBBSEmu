using MBBSEmu.CPU;
using MBBSEmu.Date;
using MBBSEmu.Disassembler;
using MBBSEmu.DOS.Interrupts;
using MBBSEmu.DOS.Structs;
using MBBSEmu.IO;
using MBBSEmu.Memory;
using NLog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using MBBSEmu.Session;

namespace MBBSEmu.DOS
{
    public class ExeRuntime
    {
        /*
         Memory layout
         0x1000:0000 => PSP
         0x1010:0000 => Program memory

         0x9FBF - envSize - 0x0001:0000 => end of heap
         0x9FBF - envSize => environment segment
         0x9FBF => end of memory
        */
        private const ushort PROGRAM_START_ADDRESS = 0x1000;
        private const ushort PSP_LENGTH = 256;

        public MZFile File;
        public IMemoryCore Memory;
        public ICpuCore Cpu;
        public ICpuRegisters Registers;
        private ILogger _logger;
        private SessionBase _sessionBase;
        private readonly FarPtr _programRealModeLoadAddress = new FarPtr(PROGRAM_START_ADDRESS + (PSP_LENGTH >> 4), 0);
        private readonly ushort _pspSegment = PROGRAM_START_ADDRESS;
        private ushort _environmentSegment;
        private ushort _environmentSize;
        private ushort _nextSegmentOffset => (ushort)(_environmentSegment - 1);

        private readonly Dictionary<string, string> _environmentVariables = new();

        public ExeRuntime(MZFile file, IClock clock, ILogger logger, IFileUtility fileUtility, SessionBase sessionBase, IStream stdin, IStream stdout, IStream stderr)
        {
            _logger = logger;
            File = file;
            Memory = new RealModeMemoryCore(0x8000, logger);
            Cpu = new CpuCore(_logger);
            Registers = new CpuRegisters();

            Registers = (ICpuRegisters)Cpu;
            Cpu.Reset(Memory, null,
                new List<IInterruptHandler>
                {
                    new Int21h(Registers, Memory, clock, _logger, fileUtility, stdin, stdout, stderr),
                    new Int1Ah(Registers, Memory, clock),
                    new Int3Eh(),
                    new Int10h(Registers, _logger, stdout),
                });
        }

        public ExeRuntime(MZFile file, IClock clock, ILogger logger, IFileUtility fileUtility, SessionBase sessionBase) : this(
            file,
            clock,
            logger,
            fileUtility,
            sessionBase,
            new BlockingCollectionReaderStream(sessionBase.DataFromClient),
            new BlockingCollectionWriterStream(sessionBase.DataToClient),
            new TextWriterStream(Console.Error)) {}

        private static ushort GetNextSegment(ushort segment, uint size) => (ushort)(segment + (size >> 4) + 1);
        private static ushort GetPreviousSegment(ushort segment, uint size) => (ushort)(segment - (size >> 4) - 1);

        public bool Load(string[] args)
        {
            CreateEnvironmentVariables(args);
            LoadProgramIntoMemory();
            ApplyRelocation();
            SetupEnvironmentVariables();
            SetupPSP(args);
            SetSegmentRegisters();
            return true;
        }

        public void Run()
        {
            while (!Registers.Halt)
                Cpu.Tick();

            _sessionBase?.Stop();
        }

        private string GetFullExecutingPath()
        {
            var exeName = Path.GetFileName(File.ExeFile);
            return $"C:\\BBSV6\\{exeName}";
        }

        private void CreateEnvironmentVariables(string[] args)
        {
            _environmentVariables["CMDLINE"] = GetFullExecutingPath() + ' ' + String.Join(' ', args);
            _environmentVariables["COMSPEC"] = "C:\\COMMAND.COM";
            _environmentVariables["COPYCMD"] = "COPY";
            _environmentVariables["DIRCMD"] = "DIR";
            _environmentVariables["PATH"] = "C:\\DOS;C:\\BBSV6";
            _environmentVariables["TMP"] = "C:\\TEMP";
            _environmentVariables["TEMP"] = "C:\\TEMP";
        }

        private void LoadProgramIntoMemory()
        {
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
                var v = inMemoryVirtualAddress + _programRealModeLoadAddress.Segment;
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
            var cmdLine = ' ' + String.Join(' ', args);
            // maximum 126 characters, thanks to DOS
            if (cmdLine.Length > 126)
                cmdLine = cmdLine.Substring(0, 126);

            var psp = new PSPStruct { NextSegOffset = _nextSegmentOffset, EnvSeg = _environmentSegment, CommandTailLength = (byte)cmdLine.Length};
            Array.Copy(Encoding.ASCII.GetBytes(cmdLine), 0, psp.CommandTail, 0, cmdLine.Length);

            Memory.SetArray(_pspSegment, 0, psp.Data);

            var pspPtr = Memory.AllocateVariable("Int21h-PSP", sizeof(ushort));
            Memory.SetWord(pspPtr, _pspSegment);
        }

        /// <summary>
        ///     Copies any Environment Variables to the Environment Variables Segment.
        /// </summary>
        /// <return>Total bytes used in the environment block</return>
        private void SetupEnvironmentVariables()
        {
            _environmentSize = CalculateEnvironmentSize();
            _environmentSegment = GetPreviousSegment(0x9FBF, _environmentSize);

            ushort bytesWritten = 0;
            foreach (var v in _environmentVariables)
            {
                var str = $"{v.Key}={v.Value}\0";
                Memory.SetArray(_environmentSegment, bytesWritten, Encoding.ASCII.GetBytes(str));
                bytesWritten += (ushort)str.Length;
            }

            // null terminate and add separation between vars & cmdline
            Memory.SetByte(_environmentSegment, bytesWritten++, 0);
            Memory.SetByte(_environmentSegment, bytesWritten++, 1);
            Memory.SetByte(_environmentSegment, bytesWritten++, 0);

            //Add EXE
            var exePath = Encoding.ASCII.GetBytes(GetFullExecutingPath() + "\0");
            Memory.SetArray(_environmentSegment, bytesWritten, exePath);
            bytesWritten += (ushort)exePath.Length;

            if (_environmentSize != bytesWritten)
                throw new InvalidProgramException("We screwed up");
        }

        private ushort CalculateEnvironmentSize()
        {
            ushort size = 0;
            foreach (var v in _environmentVariables)
            {
                size += (ushort)(v.Key + "=" + v.Value + "\0").Length;
            }

            return (ushort)(size + 4 + GetFullExecutingPath().Length);
        }
    }
}
