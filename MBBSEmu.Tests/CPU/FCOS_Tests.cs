using Iced.Intel;
using System;
using Xunit;

namespace MBBSEmu.Tests.CPU
{
    public class FCOS_Tests : CpuTestBase
    {
        [Theory]
        [InlineData(1d, 0.5403023058681398, false)]
        [InlineData(-1d, 0.5403023058681398, false)]
        [InlineData(double.NaN, double.NaN, false)]
        [InlineData(double.PositiveInfinity, double.PositiveInfinity, true)]
        [InlineData(0, 1, false)]
        [InlineData(double.NegativeInfinity, double.NegativeInfinity, true)]
        public void FCOS_Tets(double ST0Value, double expectedValue, bool throwsException)
        {
            Reset();

            mbbsEmuCpuRegisters.Fpu.ControlWord = 0;
            mbbsEmuCpuRegisters.Fpu.SetStackTop(0);
            mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackTop()] = ST0Value; //ST0

            var instructions = new Assembler(16);
            instructions.fcos();
            CreateCodeSegment(instructions);

            if (throwsException)
            {
                Assert.Throws<ArgumentOutOfRangeException>(mbbsEmuCpuCore.Tick);
            }
            else
            {
                mbbsEmuCpuCore.Tick();
                Assert.Equal(expectedValue, mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackTop()]);
                Assert.Equal(0, mbbsEmuCpuRegisters.Fpu.ControlWord);
            }

        }
    }
}
