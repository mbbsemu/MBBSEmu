using Iced.Intel;
using Xunit;

namespace MBBSEmu.Tests.CPU
{
    public class FPREM_Tests : CpuTestBase
    {
        [Theory]
        [InlineData(5.3d, 2d)]
        [InlineData(-5.3d, 2d)]
        [InlineData(10d, 3d)]
        public void FPREM_Test(double ST0Value, double ST1Value)
        {
            Reset();

            mbbsEmuCpuRegisters.Fpu.SetStackTop(1);
            mbbsEmuCpuCore.FpuStack[1] = ST0Value; //ST0
            mbbsEmuCpuCore.FpuStack[0] = ST1Value; //ST1

            var instructions = new Assembler(16);
            instructions.fprem();
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(ST0Value % ST1Value, mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackTop()]);
        }
    }
}
