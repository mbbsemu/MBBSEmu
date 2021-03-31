using Iced.Intel;
using Xunit;
using static Iced.Intel.AssemblerRegisters;

namespace MBBSEmu.Tests.CPU
{
    public class PUSHF_Tests : CpuTestBase
    {
        [Fact]
        public void PUSHF_Test()
        {
            Reset();
            mbbsEmuProtectedModeMemoryCore.AddSegment(0);
            mbbsEmuCpuRegisters.OverflowFlag = true;
            mbbsEmuCpuRegisters.AX = 0xFFFF;
            var originalSP = mbbsEmuCpuRegisters.SP;

            var instructions = new Assembler(16);

            instructions.pushf();
            instructions.pop(ax);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();
            Assert.Equal(originalSP - 2, mbbsEmuCpuRegisters.SP);

            mbbsEmuCpuCore.Tick();
            Assert.Equal(0x1234, mbbsEmuCpuRegisters.AX);
            Assert.Equal(originalSP, mbbsEmuCpuRegisters.SP);
        }

        [Fact]
        public void PUSHFD_Test()
        {
            Reset();
            mbbsEmuProtectedModeMemoryCore.AddSegment(0);
            mbbsEmuCpuRegisters.OverflowFlag = true;
            mbbsEmuCpuRegisters.EAX = 0xFFFFFFFF;
            var originalSP = mbbsEmuCpuRegisters.SP;

            var instructions = new Assembler(16);

            instructions.pushfd();
            instructions.pop(eax);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();
            Assert.Equal(originalSP - 4, mbbsEmuCpuRegisters.SP);

            mbbsEmuCpuCore.Tick();
            Assert.Equal(0x00001234u, mbbsEmuCpuRegisters.EAX);
            Assert.Equal(originalSP, mbbsEmuCpuRegisters.SP);
        }
    }
}
