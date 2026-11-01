using Iced.Intel;
using MBBSEmu.CPU;
using System;
using Xunit;
using static Iced.Intel.AssemblerRegisters;

namespace MBBSEmu.Tests.CPU
{
    /// <summary>
    ///     FIST stores ST(0) as an integer, rounded by the rounding control, and does not pop: ST(0) and the stack top
    ///     stay as they were. Expected values: an x87 coprocessor.
    /// </summary>
    public class FIST_Tests : CpuTestBase
    {
        private const ushort FPU_STATUSWORD_EXCEPTION_MASK = 0x3F;

        [Theory]
        [InlineData(31337d, (short)31337, 0)]
        [InlineData(31337.5d, (short)31338, 0x20)] //Ties to even, PE
        [InlineData(31338.5d, (short)31338, 0x20)]
        [InlineData(-2.5d, (short)-2, 0x20)]
        [InlineData(32767.4d, short.MaxValue, 0x20)]
        [InlineData(32768d, short.MinValue, 1)] //Integer indefinite, IE
        [InlineData(-32768d, short.MinValue, 0)]
        [InlineData(-32769d, short.MinValue, 1)] //Integer indefinite, IE
        [InlineData(double.NaN, short.MinValue, 1)] //Integer indefinite, IE
        public void FIST_Test_M16(double ST0Value, short expectedValue, ushort expectedStatusWord)
        {
            Reset();

            mbbsEmuCpuRegisters.DS = 2;
            mbbsEmuProtectedModeMemoryCore.AddSegment(2);
            mbbsEmuCpuRegisters.Fpu.SetStackTop(1);
            mbbsEmuCpuCore.FpuStack[1] = ST0Value;

            var instructions = new Assembler(16);
            instructions.fist(__word_ptr[0]);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(expectedValue, (short)mbbsEmuMemoryCore.GetWord(2, 0));
            Assert.Equal(1, mbbsEmuCpuRegisters.Fpu.GetStackTop());
            Assert.Equal((Float80)ST0Value, mbbsEmuCpuCore.FpuStack[1]);
            Assert.Equal(expectedStatusWord, mbbsEmuCpuRegisters.Fpu.StatusWord & FPU_STATUSWORD_EXCEPTION_MASK);
        }

        [Theory]
        [InlineData(31337.1d, 31337, 0x20)] //Inexact: PE
        [InlineData(2147483647d, int.MaxValue, 0)]
        [InlineData(2147483648d, int.MinValue, 1)] //Integer indefinite, IE
        [InlineData(-2147483648d, int.MinValue, 0)]
        [InlineData(-2147483648.5d, int.MinValue, 0x20)] //Ties to even, PE
        [InlineData(0.5d, 0, 0x20)]
        [InlineData(double.NaN, int.MinValue, 1)] //Integer indefinite, IE
        public void FIST_Test_M32(double ST0Value, int expectedValue, ushort expectedStatusWord)
        {
            Reset();

            mbbsEmuCpuRegisters.DS = 2;
            mbbsEmuProtectedModeMemoryCore.AddSegment(2);
            mbbsEmuCpuRegisters.Fpu.SetStackTop(1);
            mbbsEmuCpuCore.FpuStack[1] = ST0Value;

            var instructions = new Assembler(16);
            instructions.fist(__dword_ptr[0]);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(expectedValue, BitConverter.ToInt32(mbbsEmuMemoryCore.GetArray(2, 0, 4)));
            Assert.Equal(1, mbbsEmuCpuRegisters.Fpu.GetStackTop());
            Assert.Equal((Float80)ST0Value, mbbsEmuCpuCore.FpuStack[1]);
            Assert.Equal(expectedStatusWord, mbbsEmuCpuRegisters.Fpu.StatusWord & FPU_STATUSWORD_EXCEPTION_MASK);
        }
    }
}
