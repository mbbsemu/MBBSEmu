using System;
using System.IO;
using System.Threading;
using Iced.Intel;
using MBBSEmu.Memory;
using static Iced.Intel.AssemblerRegisters;

namespace MBBSEmu.CPU.Benchmark
{
    class Program
    {
        private CpuCore mbbsEmuCpuCore;
        private ProtectedModeMemoryCore protectedModeMemoryCore;
        private RealModeMemoryCore realModeMemoryCore;
        private IMemoryCore memoryCore;
        private ICpuRegisters mbbsEmuCpuRegisters;
        private bool _isRunning;

        public static void Main(string[] args)
        {
            new Program().Execute(args);
        }

        public Program() {}

        private void Execute(string[] args)
        {
            var realMode = args.Length == 1 && (args[0].Equals("-realmode") || args[0].Equals("-real"));

            if (realMode)
                memoryCore = realModeMemoryCore = new RealModeMemoryCore(logger: null);
            else
                memoryCore = protectedModeMemoryCore = new ProtectedModeMemoryCore(null);

            mbbsEmuCpuCore = new CpuCore(logger: null);
            mbbsEmuCpuRegisters = (ICpuRegisters)mbbsEmuCpuCore;
            mbbsEmuCpuCore.Reset(memoryCore, null, null, null);

            // Reset
            mbbsEmuCpuRegisters.Zero();
            mbbsEmuCpuCore.Reset();
            memoryCore.Clear();
            mbbsEmuCpuCore.Registers.CS = 0x1000;
            mbbsEmuCpuCore.Registers.DS = 2;
            mbbsEmuCpuCore.Registers.IP = 0;

            var instructions = new Assembler(16);
            var label_start = instructions.CreateLabel();
            var label_loop = instructions.CreateLabel();
            instructions.Label(ref label_start);
            instructions.mov(__word_ptr[0], 1);
            instructions.Label(ref label_loop);
            instructions.mov(ax, __word_ptr[0]);
            instructions.cmp(ax, 0x7FFF);
            instructions.je(label_start);
            instructions.inc(__word_ptr[0]);
            instructions.jmp(label_loop);

            CreateCodeSegment(instructions);
            CreateDataSegment(new ReadOnlySpan<byte>());

            _isRunning = true;
            new Thread(RunThread).Start();
            new Thread(MonitorThread).Start();

            Console.ReadKey();
            _isRunning = false;
        }

        private void RunThread()
        {
            while(_isRunning)
                mbbsEmuCpuCore.Tick();
        }

        private void MonitorThread()
        {
            while (_isRunning)
            {
                new AutoResetEvent(false).WaitOne(1000);
                Console.WriteLine($"Instructions Per Second: {mbbsEmuCpuCore.InstructionCounter}");
                mbbsEmuCpuCore.InstructionCounter = 0;
            }
        }

        private void CreateCodeSegment(Assembler instructions, ushort segmentOrdinal = 0x1000)
        {
            var stream = new MemoryStream();
            instructions.Assemble(new StreamCodeWriter(stream), 0);

            CreateCodeSegment(stream.ToArray(), segmentOrdinal);
        }

        private void CreateCodeSegment(ReadOnlySpan<byte> byteCode, ushort segmentOrdinal = 0x1000)
        {
            if (realModeMemoryCore != null)
            {
                realModeMemoryCore.SetArray(segmentOrdinal, 0, byteCode);
                return;
            }

            //Decode the Segment
            var instructionList = new InstructionList();
            var codeReader = new ByteArrayCodeReader(byteCode.ToArray());
            var decoder = Decoder.Create(16, codeReader);
            decoder.IP = 0x0;

            while (decoder.IP < (ulong)byteCode.Length)
            {
                decoder.Decode(out instructionList.AllocUninitializedElement());
            }

            CreateCodeSegment(instructionList, segmentOrdinal);
        }

        private void CreateCodeSegment(InstructionList instructionList, ushort segmentOrdinal = 0x1000)
        {
            protectedModeMemoryCore.AddSegment(segmentOrdinal, instructionList);
        }

        private void CreateDataSegment(ReadOnlySpan<byte> data, ushort segmentOrdinal = 2)
        {
            if (realModeMemoryCore != null)
            {
                realModeMemoryCore.SetArray(segmentOrdinal, 0, data);
                return;
            }

            protectedModeMemoryCore.AddSegment(segmentOrdinal);
            protectedModeMemoryCore.SetArray(segmentOrdinal, 0, data);
        }
    }
}
