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
            var originalSP = mbbsEmuCpuRegisters.SP;
            mbbsEmuCpuCore.Push(0xFFFF);

            var instructions = new Assembler(16);

            instructions.popf();
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(0xFFFF, mbbsEmuCpuRegisters.F);
            Assert.Equal(originalSP, mbbsEmuCpuRegisters.SP);
        }

        [Fact]
        public void POPFD_Test()
        {
            Reset();
            mbbsEmuProtectedModeMemoryCore.AddSegment(0);
            mbbsEmuCpuRegisters.F = 0xFFFF;
            var originalSP = mbbsEmuCpuRegisters.SP;
            mbbsEmuCpuCore.Push(0xFFFFFFFF);

            var instructions = new Assembler(16);

            instructions.popfd();
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            // so the reason this is 0x0000FFFF instead of 0xFFFFFFFF is because EF isn't a full 32
            // bit register, but rather we fake it to be by extending F to 32 bits.
            Assert.Equal(0x0000FFFF, mbbsEmuCpuRegisters.F);
            Assert.Equal(originalSP, mbbsEmuCpuRegisters.SP);
        }
    }
}
