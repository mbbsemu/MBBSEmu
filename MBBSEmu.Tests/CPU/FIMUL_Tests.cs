using Iced.Intel;
using Xunit;
using static Iced.Intel.AssemblerRegisters;

namespace MBBSEmu.Tests.CPU
{
    public class FIMUL_Tests : CpuTestBase
    {
        [Theory]
        [InlineData(1.5d, (short)10, 15d)]
        [InlineData(2d, (short)-3, -6d)]
        [InlineData(-2.25d, short.MinValue, 73728d)]
        public void FIMUL_Test_M16(double ST0Value, short source, double expected)
        {
            Reset();
            mbbsEmuProtectedModeMemoryCore.AddSegment(2);
            mbbsEmuCpuRegisters.DS = 2;
            mbbsEmuMemoryCore.SetWord(2, 0, (ushort)source);
            mbbsEmuCpuRegisters.Fpu.SetStackTop(0);
            mbbsEmuCpuCore.FpuStack[0] = ST0Value;

            var instructions = new Assembler(16);
            instructions.fimul(__word_ptr[0]);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(expected, mbbsEmuCpuCore.FpuStack[0]);
            Assert.Equal(0, mbbsEmuCpuRegisters.Fpu.GetStackTop());
        }

        [Theory]
        [InlineData(0.5d, 100000, 50000d)]
        [InlineData(3d, -70000, -210000d)]
        public void FIMUL_Test_M32(double ST0Value, int source, double expected)
        {
            Reset();
            mbbsEmuProtectedModeMemoryCore.AddSegment(2);
            mbbsEmuCpuRegisters.DS = 2;
            mbbsEmuMemoryCore.SetArray(2, 0, System.BitConverter.GetBytes(source));
            mbbsEmuCpuRegisters.Fpu.SetStackTop(0);
            mbbsEmuCpuCore.FpuStack[0] = ST0Value;

            var instructions = new Assembler(16);
            instructions.fimul(__dword_ptr[0]);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(expected, mbbsEmuCpuCore.FpuStack[0]);
        }
    }
}
