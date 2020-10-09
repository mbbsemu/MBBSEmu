using Iced.Intel;
using Xunit;

namespace MBBSEmu.Tests.CPU
{
    public class FSQRT_Tests : CpuTestBase
    {
        [Theory]
        [InlineData(9d, 3d, 0)]
        [InlineData(double.NaN, double.NaN, 0)]
        [InlineData(double.PositiveInfinity, double.PositiveInfinity, 0)]
        [InlineData(0, 0, 0)]
        [InlineData(-1d, -1d, 1)]
        [InlineData(double.NegativeInfinity, double.NegativeInfinity, 1)]
        public void FSQRT_Tets(double ST0Value, double expectedValue, ushort expectedControlWord)
        {
            Reset();

            mbbsEmuCpuRegisters.Fpu.ControlWord = 0;
            mbbsEmuCpuRegisters.Fpu.SetStackTop(0);
            mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackTop()] = ST0Value; //ST0

            var instructions = new Assembler(16);
            instructions.fsqrt();
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(expectedValue, mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackTop()]);
            Assert.Equal(expectedControlWord, mbbsEmuCpuRegisters.Fpu.ControlWord);
        }
    }
}
