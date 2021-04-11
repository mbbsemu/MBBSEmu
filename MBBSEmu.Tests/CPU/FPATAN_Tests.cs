using Iced.Intel;
using Xunit;

namespace MBBSEmu.Tests.CPU
{
    public class FPATAN_Tests : CpuTestBase
    {
        [Theory]
        [InlineData(1d, 2d, 1.1071487177940904d)]
        [InlineData(1d, 3d, 1.2490457723982544d)]
        [InlineData(double.NegativeInfinity, double.PositiveInfinity, 2.356194490192345d)]
        [InlineData(double.NaN, double.NaN, double.NaN)]
        [InlineData(double.NegativeInfinity, double.NaN, double.NaN)]
        [InlineData(1d, double.NaN, double.NaN)]
        [InlineData(double.NaN, 1d, double.NaN)] // Switch correct, math.Atan2 result still double.NaN
        [InlineData(double.NaN, -1d, double.NaN)] // Switch correct, math.Atan2 result still double.NaN
        [InlineData(0d, 0d, 0d)]
        public void FPATAN_Test(double ST0ValueX, double ST1ValueY, double expectedValue)
        {
            Reset();

            mbbsEmuCpuRegisters.Fpu.SetStackTop(1);
            mbbsEmuCpuCore.FpuStack[1] = ST0ValueX; //ST0
            mbbsEmuCpuCore.FpuStack[0] = ST1ValueY; //ST1

            var instructions = new Assembler(16);
            instructions.fpatan();
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(expectedValue, mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackTop()]);
        }
    }
}
