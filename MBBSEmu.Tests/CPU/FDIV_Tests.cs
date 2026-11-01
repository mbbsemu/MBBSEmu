using Iced.Intel;
using MBBSEmu.CPU;
using System;
using Xunit;
using static Iced.Intel.AssemblerRegisters;

namespace MBBSEmu.Tests.CPU
{
    public class FDIV_Tests : CpuTestBase
    {
        [Theory]
        [InlineData(2, 2)]
        [InlineData(1, .5)]
        [InlineData(0, 0)]
        public void FDIV_Test_M32(float initialST0Value, float valueToDivide)
        {
            Reset();

            CreateDataSegment(new ReadOnlySpan<byte>(), 2);
            mbbsEmuMemoryCore.SetArray(2, 0, BitConverter.GetBytes(valueToDivide));
            mbbsEmuCpuRegisters.DS = 2;
            mbbsEmuCpuRegisters.Fpu.SetStackTop(0);
            mbbsEmuCpuCore.FpuStack[0] = initialST0Value;

            var instructions = new Assembler(16);
            instructions.fdiv(__dword_ptr[0]);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            var expectedValue = initialST0Value / valueToDivide;

            Assert.Equal(expectedValue, mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackTop()]);
        }

        [Theory]
        [InlineData(2, 2)]
        [InlineData(1, .5)]
        [InlineData(0, 0)]
        public void FDIV_Test_M64(double initialST0Value, double valueToDivide)
        {
            Reset();

            CreateDataSegment(new ReadOnlySpan<byte>(), 2);
            mbbsEmuMemoryCore.SetArray(2, 0, BitConverter.GetBytes(valueToDivide));
            mbbsEmuCpuRegisters.DS = 2;
            mbbsEmuCpuRegisters.Fpu.SetStackTop(0);
            mbbsEmuCpuCore.FpuStack[0] = initialST0Value;

            var instructions = new Assembler(16);
            instructions.fdiv(__qword_ptr[0]);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            var expectedValue = initialST0Value / valueToDivide;

            Assert.Equal(expectedValue, mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackTop()]);
        }

        /// <summary>
        ///     FDIV ST(0),ST(1) of 1 and -1 by 3 under every precision control (24, 53, 64 bits) and rounding control:
        ///     the quotient is rounded once to the precision, and C1 is set when its magnitude was rounded up
        ///     (each expected value and status is what a coprocessor gave for the same control word)
        /// </summary>
        [Theory]
        [InlineData(0x007F, 1d, (ushort)0x3FFD, 0xAAAAAB0000000000UL, 0x0220)]
        [InlineData(0x007F, -1d, (ushort)0xBFFD, 0xAAAAAB0000000000UL, 0x0220)]
        [InlineData(0x047F, 1d, (ushort)0x3FFD, 0xAAAAAA0000000000UL, 0x0020)]
        [InlineData(0x047F, -1d, (ushort)0xBFFD, 0xAAAAAB0000000000UL, 0x0220)]
        [InlineData(0x087F, 1d, (ushort)0x3FFD, 0xAAAAAB0000000000UL, 0x0220)]
        [InlineData(0x087F, -1d, (ushort)0xBFFD, 0xAAAAAA0000000000UL, 0x0020)]
        [InlineData(0x0C7F, 1d, (ushort)0x3FFD, 0xAAAAAA0000000000UL, 0x0020)]
        [InlineData(0x0C7F, -1d, (ushort)0xBFFD, 0xAAAAAA0000000000UL, 0x0020)]
        [InlineData(0x027F, 1d, (ushort)0x3FFD, 0xAAAAAAAAAAAAA800UL, 0x0020)]
        [InlineData(0x027F, -1d, (ushort)0xBFFD, 0xAAAAAAAAAAAAA800UL, 0x0020)]
        [InlineData(0x067F, 1d, (ushort)0x3FFD, 0xAAAAAAAAAAAAA800UL, 0x0020)]
        [InlineData(0x067F, -1d, (ushort)0xBFFD, 0xAAAAAAAAAAAAB000UL, 0x0220)]
        [InlineData(0x0A7F, 1d, (ushort)0x3FFD, 0xAAAAAAAAAAAAB000UL, 0x0220)]
        [InlineData(0x0A7F, -1d, (ushort)0xBFFD, 0xAAAAAAAAAAAAA800UL, 0x0020)]
        [InlineData(0x0E7F, 1d, (ushort)0x3FFD, 0xAAAAAAAAAAAAA800UL, 0x0020)]
        [InlineData(0x0E7F, -1d, (ushort)0xBFFD, 0xAAAAAAAAAAAAA800UL, 0x0020)]
        [InlineData(0x037F, 1d, (ushort)0x3FFD, 0xAAAAAAAAAAAAAAABUL, 0x0220)]
        [InlineData(0x037F, -1d, (ushort)0xBFFD, 0xAAAAAAAAAAAAAAABUL, 0x0220)]
        [InlineData(0x077F, 1d, (ushort)0x3FFD, 0xAAAAAAAAAAAAAAAAUL, 0x0020)]
        [InlineData(0x077F, -1d, (ushort)0xBFFD, 0xAAAAAAAAAAAAAAABUL, 0x0220)]
        [InlineData(0x0B7F, 1d, (ushort)0x3FFD, 0xAAAAAAAAAAAAAAABUL, 0x0220)]
        [InlineData(0x0B7F, -1d, (ushort)0xBFFD, 0xAAAAAAAAAAAAAAAAUL, 0x0020)]
        [InlineData(0x0F7F, 1d, (ushort)0x3FFD, 0xAAAAAAAAAAAAAAAAUL, 0x0020)]
        [InlineData(0x0F7F, -1d, (ushort)0xBFFD, 0xAAAAAAAAAAAAAAAAUL, 0x0020)]
        public void FDIV_PrecisionAndRoundingControl(ushort controlWord, double dividend, ushort expectedSignExponent,
            ulong expectedSignificand, ushort expectedStatus)
        {
            Reset();

            mbbsEmuCpuRegisters.Fpu.ControlWord = controlWord;
            mbbsEmuCpuRegisters.Fpu.SetStackTop(1);
            mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackPointer(Register.ST0)] = dividend;
            mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackPointer(Register.ST1)] = 3d;

            var instructions = new Assembler(16);
            instructions.fdiv(st0, st1);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(new Float80(expectedSignExponent, expectedSignificand), mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackTop()]);
            Assert.Equal(expectedStatus, mbbsEmuCpuRegisters.Fpu.StatusWord & 0x023F);
        }

        /// <summary>
        ///     The exception flags go to the status word (the control word keeps its masks): 1/0 gives +infinity and
        ///     ZE, 0/0 the real indefinite and IE (coprocessor)
        /// </summary>
        [Theory]
        [InlineData(1d, (ushort)0x7FFF, 0x8000000000000000UL, 0x0004)]
        [InlineData(0d, (ushort)0xFFFF, 0xC000000000000000UL, 0x0001)]
        public void FDIV_ByZero_StatusWord(double dividend, ushort expectedSignExponent, ulong expectedSignificand,
            ushort expectedStatus)
        {
            Reset();

            mbbsEmuCpuRegisters.Fpu.SetStackTop(1);
            mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackPointer(Register.ST0)] = dividend;
            mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackPointer(Register.ST1)] = 0d;

            var instructions = new Assembler(16);
            instructions.fdiv(st0, st1);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(new Float80(expectedSignExponent, expectedSignificand), mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackTop()]);
            Assert.Equal(expectedStatus, mbbsEmuCpuRegisters.Fpu.StatusWord & 0x3F);
            Assert.Equal(0x37F, mbbsEmuCpuRegisters.Fpu.ControlWord);
        }
    }
}
