using Iced.Intel;
using Xunit;
using static Iced.Intel.AssemblerRegisters;

namespace MBBSEmu.Tests.CPU
{
    public class FXCH_Tests : CpuTestBase
    {
        [Theory]
        [InlineData(2d, 5d)]
        [InlineData(-1d, -5d)]
        [InlineData(0d, 0d)]
        [InlineData(.005d, .00100d)]
        [InlineData(float.MaxValue, float.MinValue)]
        public void FXCH_Test(float ST0Value, float ST1Value)
        {
            Reset();

            mbbsEmuCpuRegisters.Fpu.SetStackTop(1);
            mbbsEmuCpuCore.FpuStack[1] = ST0Value; //ST0
            mbbsEmuCpuCore.FpuStack[0] = ST1Value; //ST1

            var instructions = new Assembler(16);
            instructions.fxch(st0, st1);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            //Check to see if they swapped
            Assert.Equal(ST0Value, mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackPointer(Register.ST1)]);
            Assert.Equal(ST1Value, mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackPointer(Register.ST0)]);
        }
    }
}
