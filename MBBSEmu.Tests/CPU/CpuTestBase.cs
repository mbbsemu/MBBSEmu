using Iced.Intel;
using MBBSEmu.BIOS;
using MBBSEmu.CPU;
using MBBSEmu.Date;
using MBBSEmu.Memory;
using System;
using System.Collections.Generic;
using System.IO;

namespace MBBSEmu.Tests.CPU
{
    public abstract class CpuTestBase : TestBase, IDisposable
    {
        private protected CpuCore mbbsEmuCpuCore;
        private protected IMemoryCore mbbsEmuMemoryCore;
        private protected ProtectedModeMemoryCore mbbsEmuProtectedModeMemoryCore;
        private protected ICpuRegisters mbbsEmuCpuRegisters;
        private protected readonly ProgrammableIntervalTimer pit;

        private protected readonly FakeClock fakeClock = new FakeClock();

        protected CpuTestBase()
        {
            mbbsEmuMemoryCore = mbbsEmuProtectedModeMemoryCore = ProtectedModeMemoryCore.GetInstance(logger: null);
            mbbsEmuCpuCore = new CpuCore(logger: null);
            mbbsEmuCpuRegisters = mbbsEmuCpuCore;

            pit = new ProgrammableIntervalTimer(logger: null, fakeClock, mbbsEmuCpuCore);

            mbbsEmuCpuCore.Reset(
                mbbsEmuMemoryCore,
                null,
                null,
                new Dictionary<int, IIOPort>
                {
                    {0x40, pit},
                    {0x41, pit},
                    {0x42, pit},
                    {0x43, pit},
                });
        }

        public void Dispose()
        {
            mbbsEmuCpuCore.Dispose();
        }

        protected void Reset()
        {
            mbbsEmuCpuCore.Reset();
            mbbsEmuMemoryCore.Clear();
            mbbsEmuCpuRegisters.CS = 1;
            mbbsEmuCpuRegisters.IP = 0;
        }

        protected void CreateCodeSegment(Assembler instructions, ushort segmentOrdinal = 1)
        {
            var stream = new MemoryStream();
            instructions.Assemble(new StreamCodeWriter(stream), 0);

            CreateCodeSegment(stream.ToArray(), segmentOrdinal);
        }

        protected void CreateCodeSegment(ReadOnlySpan<byte> byteCode, ushort segmentOrdinal = 1)
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

        protected void CreateCodeSegment(InstructionList instructionList, ushort segmentOrdinal = 1)
        {
            mbbsEmuProtectedModeMemoryCore.AddSegment(segmentOrdinal, instructionList);
        }

        protected void CreateDataSegment(ReadOnlySpan<byte> data, ushort segmentOrdinal = 2)
        {
            mbbsEmuProtectedModeMemoryCore.AddSegment(segmentOrdinal);
            mbbsEmuMemoryCore.SetArray(segmentOrdinal, 0, data);
        }

    }
}
