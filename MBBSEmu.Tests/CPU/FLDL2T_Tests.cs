using Iced.Intel;
using Xunit;

namespace MBBSEmu.Tests.CPU
{
    public class FLDL2T_Tests : CpuTestBase
    {
        [Fact]
        public void FLDL2T_Test()
        {
            Reset();

            var instructions = new Assembler(16);
            instructions.fldl2t();
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(3.3219280948873623d, mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackTop()]);
        }
    }
}
