using Iced.Intel;
using Xunit;

namespace MBBSEmu.Tests.CPU
{
    public class IRET_Tests : CpuTestBase
    {
        [Fact]
        public void IRET_Test()
        {
            Reset();
            mbbsEmuMemoryCore.AddSegment(0);
            var entrySP = mbbsEmuCpuRegisters.SP;
            mbbsEmuCpuCore.Push(0xFFFF);
            mbbsEmuCpuCore.Push(0xFFFE);
            mbbsEmuCpuCore.Push(0xFFFD);

            var instructions = new Assembler(16);

            instructions.iret();
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(0xFFFD, mbbsEmuCpuRegisters.IP);
            Assert.Equal(0xFFFE, mbbsEmuCpuRegisters.CS);
            Assert.Equal(0xFFFF, mbbsEmuCpuRegisters.F);
            Assert.Equal(entrySP, mbbsEmuCpuRegisters.SP);
        }
    }
}
