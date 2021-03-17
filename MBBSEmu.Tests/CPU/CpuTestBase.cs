using Iced.Intel;
using MBBSEmu.CPU;
using MBBSEmu.Memory;
using System;
using System.IO;

namespace MBBSEmu.Tests.CPU
{
    public abstract class CpuTestBase : TestBase
    {
        private protected CpuCore mbbsEmuCpuCore;
        private protected IMemoryCore mbbsEmuMemoryCore;
        private protected ProtectedModeMemoryCore mbbsEmuProtectedModeMemoryCore;
        private protected CpuRegisters mbbsEmuCpuRegisters;

        protected CpuTestBase()
        {
            mbbsEmuMemoryCore = mbbsEmuProtectedModeMemoryCore = new ProtectedModeMemoryCore(null);
            mbbsEmuCpuRegisters = new CpuRegisters();
            mbbsEmuCpuCore = new CpuCore();
            mbbsEmuCpuCore.Reset(mbbsEmuMemoryCore, mbbsEmuCpuRegisters, null, null);
        }

        protected void Reset()
        {
            mbbsEmuCpuRegisters.Zero();
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
