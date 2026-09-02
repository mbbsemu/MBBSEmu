using Iced.Intel;
using System;
using Xunit;

namespace MBBSEmu.Tests.CPU
{
    public class FSINCOS_Tests : CpuTestBase
    {
        [Theory]
        [InlineData(1d)]
        [InlineData(-1d)]
        [InlineData(0d)]
        public void FSINCOS_Test(double ST0Value)
        {
            Reset();

            mbbsEmuCpuRegisters.Fpu.SetStackTop(0);
            mbbsEmuCpuCore.FpuStack[0] = ST0Value;

            var instructions = new Assembler(16);
            instructions.fsincos();
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            //ST(0) becomes cosine, ST(1) becomes sine
            Assert.Equal(Math.Cos(ST0Value), mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackTop()]);
            Assert.Equal(Math.Sin(ST0Value), mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackPointer(Register.ST1)]);
        }
    }
}
