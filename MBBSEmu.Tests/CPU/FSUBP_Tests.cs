using Iced.Intel;
using Xunit;
using static Iced.Intel.AssemblerRegisters;

namespace MBBSEmu.Tests.CPU
{
    public class FSUBP_Tests : CpuTestBase
    {
        [Theory]
        [InlineData(0.5, 1.5)]
        [InlineData(float.MaxValue, 1)]
        //[InlineData(float.MinValue, float.MaxValue)] -- Rouding Issue from double->flat, returns -inf vs. calculation below
        [InlineData(0, 0)]
        [InlineData(float.NaN, 1)]
        [InlineData(-2.5, 3.5)]
        [InlineData(-2.5, -1.0)]
        public void FSUBP_Test_Float(float ST1Value, float ST0Value)
        {
            Reset();

            mbbsEmuCpuRegisters.Fpu.SetStackTop(1);
            mbbsEmuCpuCore.FpuStack[1] = ST0Value; //ST0
            mbbsEmuCpuCore.FpuStack[0] = ST1Value; //ST1

            var instructions = new Assembler(16);
            instructions.fsubp(st1, st0);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            var expectedValue = ST1Value - ST0Value;

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
        public void FSUBP_Test_Double(double ST1Value, double ST0Value)
        {
            Reset();

            mbbsEmuCpuRegisters.Fpu.SetStackTop(1);
            mbbsEmuCpuCore.FpuStack[1] = ST0Value; //ST0
            mbbsEmuCpuCore.FpuStack[0] = ST1Value; //ST1

            var instructions = new Assembler(16);
            instructions.fsubp(st1, st0);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            var expectedValue = ST1Value - ST0Value;

            Assert.Equal(expectedValue, mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackTop()]);
        }
    }
}