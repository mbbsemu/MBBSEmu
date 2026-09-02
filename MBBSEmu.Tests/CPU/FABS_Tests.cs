using Iced.Intel;
using Xunit;

namespace MBBSEmu.Tests.CPU
{
    public class FABS_Tests : CpuTestBase
    {
        [Theory]
        [InlineData(-5d, 5d)]
        [InlineData(5d, 5d)]
        [InlineData(0d, 0d)]
        [InlineData(double.NegativeInfinity, double.PositiveInfinity)]
        [InlineData(double.NaN, double.NaN)]
        public void FABS_Test(double ST0Value, double expectedValue)
        {
            Reset();

            mbbsEmuCpuRegisters.Fpu.SetStackTop(0);
            mbbsEmuCpuCore.FpuStack[0] = ST0Value;

            var instructions = new Assembler(16);
            instructions.fabs();
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(expectedValue, mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackTop()]);
        }
    }
}
