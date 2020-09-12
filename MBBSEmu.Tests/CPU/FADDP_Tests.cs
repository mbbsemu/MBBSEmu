using Iced.Intel;
using Xunit;
using static Iced.Intel.AssemblerRegisters;

namespace MBBSEmu.Tests.CPU
{
    public class FADDP_Tests : CpuTestBase
    {
        [Theory]
        [InlineData(0.5, 1.5)]
        [InlineData(float.MaxValue, 1)]
        [InlineData(float.MinValue, float.MaxValue)]
        [InlineData(0, 0)]
        public void FADDP_Test(float ST0Value, float ST1Value)
        {
            Reset();

            mbbsEmuCpuRegisters.Fpu.SetStackTop(1);
            mbbsEmuCpuCore.FpuStack[1] = ST0Value; //ST0
            mbbsEmuCpuCore.FpuStack[0] = ST1Value; //ST1

            var instructions = new Assembler(16);
            instructions.faddp(st1, st0);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            var expectedValue = unchecked(ST0Value + ST1Value);

            Assert.Equal(expectedValue, mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackTop()]);
        }
    }
}
