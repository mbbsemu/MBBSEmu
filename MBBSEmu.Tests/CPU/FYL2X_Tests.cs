using Iced.Intel;
using System;
using Xunit;

namespace MBBSEmu.Tests.CPU
{
    public class FYL2X_Tests : CpuTestBase
    {
        [Theory]
        [InlineData(8d, 1d)]
        [InlineData(2d, 3d)]
        [InlineData(100d, 1d)]
        public void FYL2X_Test(double ST0Value, double ST1Value)
        {
            Reset();

            mbbsEmuCpuRegisters.Fpu.SetStackTop(1);
            mbbsEmuCpuCore.FpuStack[1] = ST0Value; //ST0
            mbbsEmuCpuCore.FpuStack[0] = ST1Value; //ST1

            var instructions = new Assembler(16);
            instructions.fyl2x();
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(ST1Value * Math.Log2(ST0Value), mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackTop()]);
        }
    }
}
