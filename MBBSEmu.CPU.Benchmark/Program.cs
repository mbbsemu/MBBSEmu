using System;
using System.IO;
using System.Threading;
using Iced.Intel;
using MBBSEmu.Memory;

namespace MBBSEmu.CPU.Benchmark
{
    class Program
    {
        private static CpuCore mbbsEmuCpuCore;
        private static IMemoryCore mbbsEmuMemoryCore;
        private static CpuRegisters mbbsEmuCpuRegisters;
        private static bool _isRunning;

        static Program()
        {
            mbbsEmuMemoryCore = new MemoryCore();
            mbbsEmuCpuRegisters = new CpuRegisters();
            mbbsEmuCpuCore = new CpuCore();
            mbbsEmuCpuCore.Reset(mbbsEmuMemoryCore, mbbsEmuCpuRegisters, null);
        }

        static void Main(string[] args)
        {
            //Reset
            mbbsEmuCpuRegisters.Zero();
            mbbsEmuCpuCore.Reset();
            mbbsEmuMemoryCore.Clear();
            mbbsEmuCpuRegisters.CS = 1;
            mbbsEmuCpuRegisters.IP = 0;


            var msCodeSegment = new MemoryStream();

            //XOR AX, AX
            msCodeSegment.Write(new byte[] { 0x33, 0xC0});
            //CMP AX, 0xF
            msCodeSegment.Write(new byte[] { 0x3D, 0xF, 0x00});
            //JLE
            msCodeSegment.Write(new byte[] {0x7E, 0x02});
            //INC AX
            msCodeSegment.Write(new byte[] { 0x40 });
            //INC AX
            msCodeSegment.Write(new byte[] { 0x40 });
            //CMP AX, 0x64
            msCodeSegment.Write(new byte[] {0x3D, 0x64, 0x00});
            //JL
            msCodeSegment.Write(new byte[] {0x7C, 0xF2});

            CreateCodeSegment(msCodeSegment.ToArray());

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

            mbbsEmuMemoryCore.AddSegment(segmentOrdinal, instructionList);
        }

        private static void CreateDataSegment(ReadOnlySpan<byte> data, ushort segmentOrdinal = 2)
        {
            mbbsEmuMemoryCore.AddSegment(segmentOrdinal);
            mbbsEmuMemoryCore.SetArray(segmentOrdinal, 0, data);
        }
    }
}
