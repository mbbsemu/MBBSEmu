using Iced.Intel;
using Xunit;

namespace MBBSEmu.Tests.CPU
{
    public class FXTRACT_Tests : CpuTestBase
    {
        [Theory]
        [InlineData(8d, 3d, 1d)]
        [InlineData(1.5d, 0d, 1.5d)]
        [InlineData(0.5d, -1d, 1d)]
        [InlineData(1d, 0d, 1d)]
        public void FXTRACT_Test(double ST0Value, double expectedExponent, double expectedSignificand)
        {
            Reset();

            mbbsEmuCpuRegisters.Fpu.SetStackTop(0);
            mbbsEmuCpuCore.FpuStack[0] = ST0Value;

            var instructions = new Assembler(16);
            instructions.fxtract();
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            //ST(0) becomes the significand, ST(1) becomes the exponent
            Assert.Equal(expectedSignificand, mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackTop()]);
            Assert.Equal(expectedExponent, mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackPointer(Register.ST1)]);
        }
    }
}
