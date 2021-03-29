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
        private static CpuCore mbbsEmuCpuCore;
        private static ProtectedModeMemoryCore mbbsEmuMemoryCore;
        private static CpuRegisters mbbsEmuCpuRegisters;
        private static bool _isRunning;

        static Program()
        {
            mbbsEmuMemoryCore = new ProtectedModeMemoryCore(null);
            mbbsEmuCpuRegisters = new CpuRegisters();
            mbbsEmuCpuCore = new CpuCore();
            mbbsEmuCpuCore.Reset(mbbsEmuMemoryCore, mbbsEmuCpuRegisters, null, null);
        }

        static void Main(string[] args)
        {
            //Reset
            mbbsEmuCpuRegisters.Zero();
            mbbsEmuCpuCore.Reset();
            mbbsEmuMemoryCore.Clear();
            mbbsEmuCpuCore.Registers.CS = 1;
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

        private static void RunThread()
        {
            while(_isRunning)
                mbbsEmuCpuCore.Tick();
        }

        private static void MonitorThread()
        {
            while (_isRunning)
            {
                new AutoResetEvent(false).WaitOne(1000);
                Console.WriteLine($"Instructions Per Second: {mbbsEmuCpuCore.InstructionCounter}");
                mbbsEmuCpuCore.InstructionCounter = 0;
            }
        }

        private static void CreateCodeSegment(Assembler instructions, ushort segmentOrdinal = 1)
        {
            var stream = new MemoryStream();
            instructions.Assemble(new StreamCodeWriter(stream), 0);

            CreateCodeSegment(stream.ToArray(), segmentOrdinal);
        }

        private static void CreateCodeSegment(ReadOnlySpan<byte> byteCode, ushort segmentOrdinal = 1)
        {

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

        private static void CreateCodeSegment(InstructionList instructionList, ushort segmentOrdinal = 1)
        {
            mbbsEmuMemoryCore.AddSegment(segmentOrdinal, instructionList);
        }

        private static void CreateDataSegment(ReadOnlySpan<byte> data, ushort segmentOrdinal = 2)
        {
            mbbsEmuMemoryCore.AddSegment(segmentOrdinal);
            mbbsEmuMemoryCore.SetArray(segmentOrdinal, 0, data);
        }
    }
}
