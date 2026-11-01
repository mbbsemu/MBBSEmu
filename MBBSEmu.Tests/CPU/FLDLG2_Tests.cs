using Iced.Intel;
using MBBSEmu.CPU;
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

            //log10(2) to 64 bits, rounded to nearest (the coprocessor's FLDLG2 under the default control word)
            Assert.Equal(new Float80(0x3FFD, 0x9A209A84FBCFF799), mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackTop()]);
        }
    }
}
