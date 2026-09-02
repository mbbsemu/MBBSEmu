using Iced.Intel;
using Xunit;

namespace MBBSEmu.Tests.CPU
{
    public class FLDLN2_Tests : CpuTestBase
    {
        [Fact]
        public void FLDLN2_Test()
        {
            Reset();

            var instructions = new Assembler(16);
            instructions.fldln2();
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(0.6931471805599453d, mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackTop()]);
        }
    }
}
