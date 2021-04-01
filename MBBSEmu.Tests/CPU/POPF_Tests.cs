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
            mbbsEmuCpuRegisters.F = 0xED1;
            var originalSP = mbbsEmuCpuRegisters.SP;
            mbbsEmuCpuCore.Push(0xFFFF);

            var instructions = new Assembler(16);

            instructions.popf();
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(0x0ED1, mbbsEmuCpuRegisters.F);
            Assert.Equal(originalSP, mbbsEmuCpuRegisters.SP);
        }

        [Fact]
        public void POPFD_Test()
        {
            Reset();
            mbbsEmuProtectedModeMemoryCore.AddSegment(0);
            mbbsEmuCpuRegisters.F = 0x0ED1;
            var originalSP = mbbsEmuCpuRegisters.SP;
            mbbsEmuCpuCore.Push(0xFFFFFFFF);

            var instructions = new Assembler(16);

            instructions.popfd();
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            // subset of all the flags
            Assert.Equal(0x00000ED1, mbbsEmuCpuRegisters.F);
            Assert.Equal(originalSP, mbbsEmuCpuRegisters.SP);
        }
    }
}
