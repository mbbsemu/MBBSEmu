using Iced.Intel;
using Xunit;

namespace MBBSEmu.Tests.CPU
{
    public class FCLEX_Tests : CpuTestBase
    {
        [Fact]
        public void FCLEX_Test()
        {
            Reset();
            mbbsEmuCpuRegisters.Fpu.StatusWord = 0x3F; //Set all 6 Error Flags

            var instructions = new Assembler(16);
            instructions.fclex();
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();
            mbbsEmuCpuCore.Tick();

            Assert.Equal(0, mbbsEmuCpuRegisters.Fpu.StatusWord);
        }
    }
}
