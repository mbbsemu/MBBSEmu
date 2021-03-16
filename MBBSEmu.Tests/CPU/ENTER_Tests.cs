using Iced.Intel;
using Xunit;

namespace MBBSEmu.Tests.CPU
{
    public class ENTER_Tests : CpuTestBase
    {
        [Theory]
        [InlineData(0)]
        [InlineData(0x10)]
        [InlineData(0xFF)]
        public void ENTER_Test(ushort frameSize)
        {
            Reset();

            mbbsEmuProtectedModeMemoryCore.AddSegment(0); //ENTER relies on the stack

            var instructions = new Assembler(16);
            instructions.enter(frameSize, 0);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(mbbsEmuCpuCore.Registers.BP - frameSize, mbbsEmuCpuCore.Registers.SP);
        }
    }
}
