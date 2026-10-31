using Iced.Intel;
using MBBSEmu.CPU;
using Xunit;
using static Iced.Intel.AssemblerRegisters;

namespace MBBSEmu.Tests.CPU
{
    public class FDIVRP_Tests : CpuTestBase
    {
        [Theory]
        [InlineData(2, 2)]
        [InlineData(1, .5)]
        [InlineData(0, 0)]
        public void FDIVRP_ST1_Test(float ST0Value, float ST1Value)
        {
            Reset();

            mbbsEmuCpuRegisters.Fpu.SetStackTop(1);
            mbbsEmuCpuCore.FpuStack[1] = ST0Value; //ST0
            mbbsEmuCpuCore.FpuStack[0] = ST1Value; //ST1

            var instructions = new Assembler(16);
            instructions.fdivrp(st1, st0);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            var expectedValue = ST0Value / ST1Value;

            Assert.Equal(expectedValue, mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackTop()]);
            Assert.Equal(0, mbbsEmuCpuRegisters.Fpu.GetStackTop());
        }

        [Theory]
        [InlineData(2, 2)]
        [InlineData(1, .5)]
        [InlineData(0, 0)]
        public void FDIVRP_STi_Test(float ST0Value, float STiValue)
        {
            Reset();

            mbbsEmuCpuRegisters.Fpu.SetStackTop(2);
            mbbsEmuCpuCore.FpuStack[2] = ST0Value; //ST0
            mbbsEmuCpuCore.FpuStack[1] = 0d; //ST1
            mbbsEmuCpuCore.FpuStack[0] = STiValue; //ST2

            var instructions = new Assembler(16);
            instructions.fdivrp(st2, st0);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            var expectedValue = ST0Value / STiValue;

            //FDIVRP ST(2),ST(0) leaves the result in ST(2), which the pop makes ST(1); the old ST(1) (zero here)
            //becomes ST(0) (coprocessor)
            Assert.Equal(expectedValue, mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackPointer(Register.ST1)]);
            Assert.Equal(Float80.Zero, mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackTop()]);
            Assert.Equal(1, mbbsEmuCpuRegisters.Fpu.GetStackTop());
        }
    }
}
