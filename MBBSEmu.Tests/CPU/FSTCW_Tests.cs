using Iced.Intel;
using System;
using Xunit;
using static Iced.Intel.AssemblerRegisters;

namespace MBBSEmu.Tests.CPU
{
    public class FSTCW_Tests : CpuTestBase
    {
        [Fact]
        public void FSTCW_Test_M16()
        {
            Reset();
            mbbsEmuCpuRegisters.Fpu.ControlWord = 0xFFFF;
            CreateDataSegment(new ReadOnlySpan<byte>(), 2);
            mbbsEmuCpuRegisters.DS = 2;

            var instructions = new Assembler(16);
            instructions.fstcw(__word_ptr[0]);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();
            mbbsEmuCpuCore.Tick();

            Assert.Equal(0xFFFF, mbbsEmuMemoryCore.GetWord(2, 0));
        }
    }
}
