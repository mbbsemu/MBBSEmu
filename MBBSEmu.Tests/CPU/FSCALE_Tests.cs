using Iced.Intel;
using Xunit;

namespace MBBSEmu.Tests.CPU
{
    public class FSCALE_Tests : CpuTestBase
    {
        [Theory]
        [InlineData(double.NaN, double.NaN, double.NaN)]
        [InlineData(double.NegativeInfinity, double.NegativeInfinity, double.NaN)]
        [InlineData(double.PositiveInfinity, double.NegativeInfinity, double.NaN)]
        [InlineData(double.NegativeInfinity, double.PositiveInfinity, double.NegativeInfinity)]
        [InlineData(0, double.PositiveInfinity, double.NaN)]
        [InlineData(0, double.NegativeInfinity, 0)]
        [InlineData(double.PositiveInfinity, 0, double.PositiveInfinity)]
        [InlineData(4, 2, 16)]
        [InlineData(4, 4, 64)]
        [InlineData(-16, 8, -4096)]
        public void FSCALE_Test(double ST0Value, double ST1Value, double expectedValue)
        {
            Reset();

            mbbsEmuCpuRegisters.Fpu.SetStackTop(1);
            mbbsEmuCpuCore.FpuStack[1] = ST0Value; //ST0
            mbbsEmuCpuCore.FpuStack[0] = ST1Value; //ST1

            var instructions = new Assembler(16);
            instructions.fscale();
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(expectedValue, mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackTop()]);
        }
    }
}
