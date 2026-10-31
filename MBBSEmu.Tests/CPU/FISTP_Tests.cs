using Iced.Intel;
using MBBSEmu.CPU;
using System;
using Xunit;
using static Iced.Intel.AssemblerRegisters;

namespace MBBSEmu.Tests.CPU
{
    public class FISTP_Tests : CpuTestBase
    {
        private const ushort FPU_STATUSWORD_EXCEPTION_MASK = 0x3F;

        [Theory]
        [InlineData((double)(short.MaxValue), short.MaxValue, 0)]
        [InlineData((double)(short.MaxValue + 1), short.MinValue, 1)] //Integer indefinite, IE
        [InlineData((double)(short.MinValue), short.MinValue, 0)]
        [InlineData((double)(short.MinValue - 1), short.MinValue, 1)] //Integer indefinite, IE
        [InlineData((double)31337, 31337, 0)]
        [InlineData(-0.0d, 0, 0)]
        [InlineData(double.NaN, short.MinValue, 1)] //Integer indefinite, IE
        public void FISTP_Test_M16_DS(double ST0Value, short expectedvalue, ushort expectedStatusWord)
        {
            Reset();

            mbbsEmuCpuRegisters.DS = 2;
            mbbsEmuProtectedModeMemoryCore.AddSegment(2);
            mbbsEmuCpuRegisters.Fpu.SetStackTop(1);
            mbbsEmuCpuCore.FpuStack[1] = ST0Value;

            var instructions = new Assembler(16);
            instructions.fistp(__word_ptr[0]);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(expectedvalue, (short)mbbsEmuMemoryCore.GetWord(2, 0));
            Assert.Equal(0, mbbsEmuCpuRegisters.Fpu.GetStackTop());
            Assert.Equal(expectedStatusWord, mbbsEmuCpuRegisters.Fpu.StatusWord & FPU_STATUSWORD_EXCEPTION_MASK);
        }

        [Theory]
        [InlineData((double)(int.MaxValue), int.MaxValue, 0)]
        [InlineData(31337.1d, 31337, 0x20)] //Inexact: PE
        [InlineData((double)(int.MinValue), int.MinValue, 0)]
        [InlineData(-0.0d, 0, 0)]
        [InlineData(double.NaN, int.MinValue, 1)] //Integer indefinite, IE
        public void FISTP_Test_M32_DS(double ST0Value, int expectedvalue, ushort expectedStatusWord)
        {
            Reset();

            mbbsEmuCpuRegisters.DS = 2;
            mbbsEmuProtectedModeMemoryCore.AddSegment(2);
            mbbsEmuCpuRegisters.Fpu.SetStackTop(1);
            mbbsEmuCpuCore.FpuStack[1] = ST0Value;

            var instructions = new Assembler(16);
            instructions.fistp(__dword_ptr[0]);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(expectedvalue, BitConverter.ToInt32(mbbsEmuMemoryCore.GetArray(2, 0, 4)));
            Assert.Equal(0, mbbsEmuCpuRegisters.Fpu.GetStackTop());
            Assert.Equal(expectedStatusWord, mbbsEmuCpuRegisters.Fpu.StatusWord & FPU_STATUSWORD_EXCEPTION_MASK);
        }

        /// <summary>
        ///     FISTP m64 stores all 64 bits (the 8087 and later have it)
        /// </summary>
        [Theory]
        [InlineData((double)(long.MinValue), long.MinValue, 0)] //-2^63 fits a 64-bit integer
        [InlineData(-0.0d, 0, 0)]
        [InlineData(double.NaN, long.MinValue, 1)] //Integer indefinite, IE
        public void FISTP_Test_M64_DS(double ST0Value, long expectedvalue, ushort expectedStatusWord)
        {
            Reset();

            mbbsEmuCpuRegisters.DS = 2;
            mbbsEmuProtectedModeMemoryCore.AddSegment(2);
            mbbsEmuCpuRegisters.Fpu.SetStackTop(1);
            mbbsEmuCpuCore.FpuStack[1] = ST0Value;

            var instructions = new Assembler(16);
            instructions.fistp(__qword_ptr[0]);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(expectedvalue, BitConverter.ToInt64(mbbsEmuMemoryCore.GetArray(2, 0, 8)));
            Assert.Equal(0, mbbsEmuCpuRegisters.Fpu.GetStackTop());
            Assert.Equal(expectedStatusWord, mbbsEmuCpuRegisters.Fpu.StatusWord & FPU_STATUSWORD_EXCEPTION_MASK);
        }

        [Theory]
        [InlineData((double)(short.MaxValue), short.MaxValue, 0)]
        [InlineData((double)(short.MaxValue + 1), short.MinValue, 1)] //Integer indefinite, IE
        [InlineData((double)(short.MinValue), short.MinValue, 0)]
        [InlineData((double)(short.MinValue - 1), short.MinValue, 1)] //Integer indefinite, IE
        [InlineData((double)31337, 31337, 0)]
        [InlineData(-0.0d, 0, 0)]
        [InlineData(double.NaN, short.MinValue, 1)] //Integer indefinite, IE
        public void FISTP_Test_M16_SS(double ST0Value, short expectedvalue, ushort expectedStatusWord)
        {
            Reset();

            mbbsEmuCpuRegisters.SS = 0;
            mbbsEmuProtectedModeMemoryCore.AddSegment(0);
            mbbsEmuCpuRegisters.Fpu.SetStackTop(1);
            mbbsEmuCpuCore.FpuStack[1] = ST0Value;

            var instructions = new Assembler(16);
            instructions.fistp(__word_ptr.ss[0]);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(expectedvalue, (short)mbbsEmuMemoryCore.GetWord(mbbsEmuCpuRegisters.SS, 0));
            Assert.Equal(0, mbbsEmuCpuRegisters.Fpu.GetStackTop());
            Assert.Equal(expectedStatusWord, mbbsEmuCpuRegisters.Fpu.StatusWord & FPU_STATUSWORD_EXCEPTION_MASK);
        }

        [Theory]
        [InlineData((double)(int.MaxValue), int.MaxValue, 0)]
        [InlineData(31337.1d, 31337, 0x20)] //Inexact: PE
        [InlineData((double)(int.MinValue), int.MinValue, 0)]
        [InlineData(-0.0d, 0, 0)]
        [InlineData(double.NaN, int.MinValue, 1)] //Integer indefinite, IE
        public void FISTP_Test_M32_SS(double ST0Value, int expectedvalue, ushort expectedStatusWord)
        {
            Reset();

            mbbsEmuCpuRegisters.SS = 0;
            mbbsEmuProtectedModeMemoryCore.AddSegment(0);
            mbbsEmuCpuRegisters.Fpu.SetStackTop(1);
            mbbsEmuCpuCore.FpuStack[1] = ST0Value;

            var instructions = new Assembler(16);
            instructions.fistp(__dword_ptr.ss[0]);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(expectedvalue, BitConverter.ToInt32(mbbsEmuMemoryCore.GetArray(mbbsEmuCpuRegisters.SS, 0, 4)));
            Assert.Equal(0, mbbsEmuCpuRegisters.Fpu.GetStackTop());
            Assert.Equal(expectedStatusWord, mbbsEmuCpuRegisters.Fpu.StatusWord & FPU_STATUSWORD_EXCEPTION_MASK);
        }

        [Theory]
        [InlineData((double)(long.MinValue), long.MinValue, 0)] //-2^63 fits a 64-bit integer
        [InlineData(-0.0d, 0, 0)]
        [InlineData(double.NaN, long.MinValue, 1)] //Integer indefinite, IE
        public void FISTP_Test_M64_SS(double ST0Value, long expectedvalue, ushort expectedStatusWord)
        {
            Reset();

            mbbsEmuCpuRegisters.SS = 0;
            mbbsEmuProtectedModeMemoryCore.AddSegment(0);
            mbbsEmuCpuRegisters.Fpu.SetStackTop(1);
            mbbsEmuCpuCore.FpuStack[1] = ST0Value;

            var instructions = new Assembler(16);
            instructions.fistp(__qword_ptr.ss[0]);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(expectedvalue, BitConverter.ToInt64(mbbsEmuMemoryCore.GetArray(mbbsEmuCpuRegisters.SS, 0, 8)));
            Assert.Equal(0, mbbsEmuCpuRegisters.Fpu.GetStackTop());
            Assert.Equal(expectedStatusWord, mbbsEmuCpuRegisters.Fpu.StatusWord & FPU_STATUSWORD_EXCEPTION_MASK);
        }

        /// <summary>
        ///     FISTP m64 with rounding control "chop" (Borland's conversion of a floating-point value to long and
        ///     unsigned long keeps the low 32 bits of it): values from 2^31 to 2^32 and beyond 32 bits are stored
        ///     whole; beyond 64 bits the integer indefinite with IE (coprocessor)
        /// </summary>
        [Theory]
        [InlineData(2147483648.0d, 0x0000000080000000UL, 0x0000)]
        [InlineData(3000000000.5d, 0x00000000B2D05E00UL, 0x0020)]
        [InlineData(4294967295.75d, 0x00000000FFFFFFFFUL, 0x0020)]
        [InlineData(4294967296.0d, 0x0000000100000000UL, 0x0000)]
        [InlineData(4294967297.0d, 0x0000000100000001UL, 0x0000)]
        [InlineData(-1.5d, 0xFFFFFFFFFFFFFFFFUL, 0x0020)]
        [InlineData(1e19d, 0x8000000000000000UL, 0x0001)]
        public void FISTP_Test_M64_Chop(double ST0Value, ulong expectedValue, ushort expectedStatusWord)
        {
            Reset();

            mbbsEmuCpuRegisters.DS = 2;
            mbbsEmuProtectedModeMemoryCore.AddSegment(2);
            mbbsEmuCpuRegisters.Fpu.ControlWord = 0x0F7F;
            mbbsEmuCpuRegisters.Fpu.SetStackTop(1);
            mbbsEmuCpuCore.FpuStack[1] = ST0Value;

            var instructions = new Assembler(16);
            instructions.fistp(__qword_ptr[0]);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(expectedValue, BitConverter.ToUInt64(mbbsEmuMemoryCore.GetArray(2, 0, 8)));
            Assert.Equal(0, mbbsEmuCpuRegisters.Fpu.GetStackTop());
            Assert.Equal(expectedStatusWord, mbbsEmuCpuRegisters.Fpu.StatusWord & 0x023F);
        }
    }
}
