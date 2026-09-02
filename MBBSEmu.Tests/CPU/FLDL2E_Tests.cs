using Iced.Intel;
using Xunit;

namespace MBBSEmu.Tests.CPU
{
    public class FLDL2E_Tests : CpuTestBase
    {
        [Fact]
        public void FLDL2E_Test()
        {
            Reset();

            var instructions = new Assembler(16);
            instructions.fldl2e();
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(1.4426950408889634d, mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackTop()]);
        }
    }
}
