using Iced.Intel;
using Xunit;

namespace MBBSEmu.Tests.CPU
{
    public class FLDLG2_Tests : CpuTestBase
    {
        [Fact]
        public void FLDLG2_Test()
        {
            Reset();

            var instructions = new Assembler(16);
            instructions.fldlg2();
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(0.3010299956639812d, mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackTop()]);
        }
    }
}
