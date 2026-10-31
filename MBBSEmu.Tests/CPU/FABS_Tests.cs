using Iced.Intel;
using MBBSEmu.CPU;
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

        /// <summary>
        ///     FABS clears the sign of a NaN as well: double.NaN is the negative quiet NaN (the real indefinite,
        ///     FFFF C000000000000000) and comes back positive (coprocessor: FABS of FFFFC000000000000000 gives
        ///     7FFFC000000000000000, no exception)
        /// </summary>
        [Fact]
        public void FABS_NaN_Test()
        {
            Reset();
            mbbsEmuCpuRegisters.Fpu.SetStackTop(0);
            mbbsEmuCpuCore.FpuStack[0] = double.NaN;

            var instructions = new Assembler(16);
            instructions.fabs();
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(new Float80(0x7FFF, 0xC000000000000000), mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackTop()]);
            Assert.Equal(0, mbbsEmuCpuRegisters.Fpu.StatusWord & 0x3F);
        }
    }
}
