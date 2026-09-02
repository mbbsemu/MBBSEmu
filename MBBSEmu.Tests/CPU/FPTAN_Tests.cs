using Iced.Intel;
using System;
using Xunit;

namespace MBBSEmu.Tests.CPU
{
    public class FPTAN_Tests : CpuTestBase
    {
        [Theory]
        [InlineData(1d)]
        [InlineData(-1d)]
        [InlineData(0d)]
        public void FPTAN_Test(double ST0Value)
        {
            Reset();

            mbbsEmuCpuRegisters.Fpu.SetStackTop(0);
            mbbsEmuCpuCore.FpuStack[0] = ST0Value;

            var instructions = new Assembler(16);
            instructions.fptan();
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            //ST(0) becomes 1.0, ST(1) becomes the tangent
            Assert.Equal(1d, mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackTop()]);
            Assert.Equal(Math.Tan(ST0Value), mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackPointer(Register.ST1)]);
        }
    }
}
