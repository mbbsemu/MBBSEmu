using Iced.Intel;
using Xunit;
using static Iced.Intel.AssemblerRegisters;

namespace MBBSEmu.Tests.CPU
{
    public class FIDIV_Tests : CpuTestBase
    {
        [Theory]
        [InlineData(15d, (short)10, 1.5d)]
        [InlineData(-6d, (short)-3, 2d)]
        [InlineData(73728d, short.MinValue, -2.25d)]
        public void FIDIV_Test_M16(double ST0Value, short source, double expected)
        {
            Reset();
            mbbsEmuProtectedModeMemoryCore.AddSegment(2);
            mbbsEmuCpuRegisters.DS = 2;
            mbbsEmuMemoryCore.SetWord(2, 0, (ushort)source);
            mbbsEmuCpuRegisters.Fpu.SetStackTop(0);
            mbbsEmuCpuCore.FpuStack[0] = ST0Value;

            var instructions = new Assembler(16);
            instructions.fidiv(__word_ptr[0]);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(expected, mbbsEmuCpuCore.FpuStack[0]);
            Assert.Equal(0, mbbsEmuCpuRegisters.Fpu.GetStackTop());
        }

        [Theory]
        [InlineData(50000d, 100000, 0.5d)]
        [InlineData(-210000d, -70000, 3d)]
        public void FIDIV_Test_M32(double ST0Value, int source, double expected)
        {
            Reset();
            mbbsEmuProtectedModeMemoryCore.AddSegment(2);
            mbbsEmuCpuRegisters.DS = 2;
            mbbsEmuMemoryCore.SetArray(2, 0, System.BitConverter.GetBytes(source));
            mbbsEmuCpuRegisters.Fpu.SetStackTop(0);
            mbbsEmuCpuCore.FpuStack[0] = ST0Value;

            var instructions = new Assembler(16);
            instructions.fidiv(__dword_ptr[0]);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(expected, mbbsEmuCpuCore.FpuStack[0]);
        }
    }
}
