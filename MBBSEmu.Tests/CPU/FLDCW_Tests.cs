using Iced.Intel;
using System;
using Xunit;
using static Iced.Intel.AssemblerRegisters;

namespace MBBSEmu.Tests.CPU
{
    public class FLDCW_Tests : CpuTestBase
    {
        [Fact]
        public void FLDCW_Test()
        {
            Reset();
            mbbsEmuCpuRegisters.Fpu.ControlWord = 0;
            CreateDataSegment(new ReadOnlySpan<byte>(), 2);
            mbbsEmuMemoryCore.SetWord(2,0, 0xFFFF);
            mbbsEmuCpuRegisters.DS = 2;

            var instructions = new Assembler(16);
            instructions.fldcw(__word_ptr[0]);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(0xFFFF, mbbsEmuCpuRegisters.Fpu.ControlWord);
        }
    }
}
