using Iced.Intel;
using Xunit;

namespace MBBSEmu.Tests.CPU
{
    public class POPF_Tests : CpuTestBase
    {
        [Fact]
        public void POPF_Test()
        {
            Reset();
            mbbsEmuProtectedModeMemoryCore.AddSegment(0);
            mbbsEmuCpuRegisters.F = 0;
            mbbsEmuCpuCore.Push(0xFFFF);

            var instructions = new Assembler(16);

            instructions.popf();
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(0xFFFF, mbbsEmuCpuRegisters.F);
        }
    }
}
