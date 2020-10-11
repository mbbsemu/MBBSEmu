using System;
using Iced.Intel;
using Xunit;
using static Iced.Intel.AssemblerRegisters;

namespace MBBSEmu.Tests.CPU
{
    public class FSTSW_Tests : CpuTestBase
    {
        [Fact]
        public void FSTSW_Test_AX()
        {
            Reset();
            mbbsEmuCpuRegisters.Fpu.StatusWord = 0xFFFF;
            
            var instructions = new Assembler(16);
            instructions.fstsw(ax);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();
            mbbsEmuCpuCore.Tick();

            Assert.Equal(0xFFFF, mbbsEmuCpuRegisters.AX);
        }

        [Fact]
        public void FSTSW_Test_M16()
        {
            Reset();
            mbbsEmuCpuRegisters.Fpu.StatusWord = 0xFFFF;
            CreateDataSegment(new ReadOnlySpan<byte>(), 2);
            mbbsEmuCpuRegisters.DS = 2;

            var instructions = new Assembler(16);
            instructions.fstsw(__word_ptr[0]);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();
            mbbsEmuCpuCore.Tick();

            Assert.Equal(0xFFFF, mbbsEmuMemoryCore.GetWord(2,0));
        }
       
    }
}
