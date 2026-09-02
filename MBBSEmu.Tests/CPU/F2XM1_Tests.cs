using Iced.Intel;
using System;
using Xunit;

namespace MBBSEmu.Tests.CPU
{
    public class F2XM1_Tests : CpuTestBase
    {
        [Theory]
        [InlineData(1d)]
        [InlineData(-1d)]
        [InlineData(0.5d)]
        [InlineData(0d)]
        public void F2XM1_Test(double ST0Value)
        {
            Reset();

            mbbsEmuCpuRegisters.Fpu.SetStackTop(0);
            mbbsEmuCpuCore.FpuStack[0] = ST0Value;

            var instructions = new Assembler(16);
            instructions.f2xm1();
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(Math.Pow(2, ST0Value) - 1, mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackTop()]);
        }
    }
}
