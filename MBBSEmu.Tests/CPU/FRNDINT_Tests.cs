using Iced.Intel;
using Xunit;

namespace MBBSEmu.Tests.CPU
{
    public class FRNDINT_Tests : CpuTestBase
    {
        [Theory]
        [InlineData(2.1d, 2d)]
        [InlineData(0.1d, 0d)]
        [InlineData(1.9d, 2d)]
        [InlineData(-1.9d, -2d)]
        [InlineData(0.5d, 1d)]
        [InlineData(0.49999999d, 0d)]
        public void FRNDINT_Test(double ST0Value, double expectedValue)
        {
            Reset();
            mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackTop()] = ST0Value;

            var instructions = new Assembler(16);
            instructions.frndint();
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(expectedValue, mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackTop()]);
        }
    }
}
