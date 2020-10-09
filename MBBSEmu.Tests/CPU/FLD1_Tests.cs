using Iced.Intel;
using Xunit;

namespace MBBSEmu.Tests.CPU
{
    public class FLD1_Tests : CpuTestBase
    {
        [Fact]
        public void FLD1_Test()
        {
            Reset();

            var instructions = new Assembler(16);
            instructions.fld1();
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(1d, mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackTop()]);
        }
    }
}
