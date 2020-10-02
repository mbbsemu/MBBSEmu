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
        [InlineData(float.NaN, 1)]
        [InlineData(-2.5, 3.5)]
        [InlineData(-2.5, -1.0)]
        public void FADDP_Test_Float(float ST0Value, float ST1Value)
        {
            Reset();

            mbbsEmuCpuRegisters.Fpu.SetStackTop(1);
            mbbsEmuCpuCore.FpuStack[1] = ST0Value; //ST0
            mbbsEmuCpuCore.FpuStack[0] = ST1Value; //ST1

            var instructions = new Assembler(16);
            instructions.faddp(st1, st0);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            var expectedValue = ST0Value + ST1Value;

            Assert.Equal(expectedValue, mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackTop()]);
        }

        [Theory]
        [InlineData(0.5, 1.5)]
        [InlineData(double.MaxValue, 1)]
        [InlineData(double.MinValue, double.MaxValue)]
        [InlineData(0, 0)]
        [InlineData(double.NaN, 1)]
        [InlineData(-2.5, 3.5)]
        [InlineData(-2.5, -1.0)]
        public void FADDP_Test_Double(double ST0Value, double ST1Value)
        {
            Reset();

            mbbsEmuCpuRegisters.Fpu.SetStackTop(1);
            mbbsEmuCpuCore.FpuStack[1] = ST0Value; //ST0
            mbbsEmuCpuCore.FpuStack[0] = ST1Value; //ST1

            var instructions = new Assembler(16);
            instructions.faddp(st1, st0);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            var expectedValue = ST0Value + ST1Value;

            Assert.Equal(expectedValue, mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackTop()]);
        }
    }
}
