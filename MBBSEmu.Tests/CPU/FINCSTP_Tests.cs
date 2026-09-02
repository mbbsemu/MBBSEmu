using Iced.Intel;
using Xunit;

namespace MBBSEmu.Tests.CPU
{
    public class FINCSTP_Tests : CpuTestBase
    {
        [Fact]
        public void FINCSTP_Test()
        {
            Reset();

            mbbsEmuCpuRegisters.Fpu.SetStackTop(1);
            mbbsEmuCpuCore.FpuStack[1] = 111d; //ST0
            mbbsEmuCpuCore.FpuStack[0] = 222d; //ST1

            var instructions = new Assembler(16);
            instructions.fincstp();
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            //old ST(1) is now ST(0)
            Assert.Equal(222d, mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackTop()]);
        }
    }
}
