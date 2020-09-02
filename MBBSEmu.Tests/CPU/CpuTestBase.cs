using Iced.Intel;
using MBBSEmu.CPU;
using MBBSEmu.Memory;
using System;
using System.Collections.Generic;

namespace MBBSEmu.Tests.CPU
{
    public abstract class CpuTestBase : TestBase
    {
        private protected CpuCore mbbsEmuCpuCore;
        private protected IMemoryCore mbbsEmuMemoryCore;
        private protected CpuRegisters mbbsEmuCpuRegisters;

        protected CpuTestBase()
        {
            mbbsEmuMemoryCore = new MemoryCore();
            mbbsEmuCpuRegisters = new CpuRegisters();
            mbbsEmuCpuCore = new CpuCore();
            mbbsEmuCpuCore.Reset(mbbsEmuMemoryCore, mbbsEmuCpuRegisters, null);
        }

        protected void Reset()
        {
            mbbsEmuCpuRegisters.Zero();
            mbbsEmuCpuCore.Reset();
            mbbsEmuMemoryCore.Clear();
            mbbsEmuCpuRegisters.CS = 1;
            mbbsEmuCpuRegisters.IP = 0;
        }

        protected void CreateCodeSegment(IReadOnlyList<Instruction> instructions, ushort segmentOrdinal = 1)
        {
            var instructionList = new InstructionList(instructions.Count);

            foreach(var i in instructions)
                instructionList.Add(i);

            CreateCodeSegment(instructionList, segmentOrdinal);
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
            mbbsEmuMemoryCore.AddSegment(segmentOrdinal, instructionList);
        }

        protected void CreateDataSegment(ReadOnlySpan<byte> data, ushort segmentOrdinal = 2)
        {
            mbbsEmuMemoryCore.AddSegment(segmentOrdinal);
            mbbsEmuMemoryCore.SetArray(segmentOrdinal, 0, data);
        }

    }
}