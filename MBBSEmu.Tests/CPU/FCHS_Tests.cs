using Iced.Intel;
using Xunit;

namespace MBBSEmu.Tests.CPU
{
    public class FCHS_Tests : CpuTestBase
    {
        [Theory]
        [InlineData(-1, 1)]
        [InlineData(1, -1)]
        public void FCHS_Test(double ST0Value, double expectedValue)
        {
            Reset();

            mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackTop()] = ST0Value;

            var instructions = new Assembler(16);
            instructions.fchs();
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(expectedValue, mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackTop()]);
        }
    }
}