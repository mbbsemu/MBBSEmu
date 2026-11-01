using Iced.Intel;
using MBBSEmu.CPU;
using System;
using Xunit;
using static Iced.Intel.AssemblerRegisters;

namespace MBBSEmu.Tests.CPU
{
    public class FST_Tests : CpuTestBase
    {
        [Theory]
        [InlineData(double.MaxValue)]
        [InlineData(double.MinValue)]
        [InlineData(0.0d)]
        [InlineData(double.NaN)]
        public void FST_Test_ST1(double ST0Value)
        {
            Reset();
            mbbsEmuCpuRegisters.Fpu.SetStackTop(1);
            mbbsEmuCpuCore.FpuStack[1] = ST0Value;

            var instructions = new Assembler(16);
            instructions.fst(st1);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(ST0Value, mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackPointer(Register.ST1)]);
            Assert.Equal(1, mbbsEmuCpuRegisters.Fpu.GetStackTop());
        }

        [Theory]
        [InlineData(double.MaxValue)]
        [InlineData(double.MinValue)]
        [InlineData(0.0d)]
        [InlineData(double.NaN)]
        public void FST_Test_M32(double ST0Value)
        {
            Reset();
            CreateDataSegment(new ReadOnlySpan<byte>(), 2);
            mbbsEmuCpuRegisters.DS = 2;
            mbbsEmuCpuRegisters.Fpu.SetStackTop(0);
            mbbsEmuCpuCore.FpuStack[0] = ST0Value;

            var instructions = new Assembler(16);
            instructions.fst(__dword_ptr[0]);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal((float)ST0Value, BitConverter.ToSingle(mbbsEmuMemoryCore.GetArray(2, 0, 4)));
            Assert.Equal(0, mbbsEmuCpuRegisters.Fpu.GetStackTop());
        }

        [Theory]
        [InlineData(double.MaxValue)]
        [InlineData(double.MinValue)]
        [InlineData(0.0d)]
        [InlineData(double.NaN)]
        public void FST_Test_M64(double ST0Value)
        {
            Reset();
            CreateDataSegment(new ReadOnlySpan<byte>(), 2);
            mbbsEmuCpuRegisters.DS = 2;
            mbbsEmuCpuRegisters.Fpu.SetStackTop(0);
            mbbsEmuCpuCore.FpuStack[0] = ST0Value;

            var instructions = new Assembler(16);
            instructions.fst(__qword_ptr[0]);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(ST0Value, BitConverter.ToDouble(mbbsEmuMemoryCore.GetArray(2, 0, 8)));
            Assert.Equal(0, mbbsEmuCpuRegisters.Fpu.GetStackTop());
        }

        /// <summary>
        ///     FST m64 and m32 round by the rounding control and report in the status word: twice the largest double
        ///     overflows to +infinity (OE, PE, C1); 1/3 to single precision is inexact and rounded up (PE, C1)
        ///     (coprocessor)
        /// </summary>
        [Theory]
        [InlineData((ushort)0x43FF, 0xFFFFFFFFFFFFF800UL, 8, 0x7FF0000000000000UL, 0x0228)]
        [InlineData((ushort)0x3FFD, 0xAAAAAAAAAAAAAAABUL, 4, 0x3EAAAAABUL, 0x0220)]
        public void FST_Rounding_StatusWord(ushort signExponent, ulong significand, int size, ulong expectedBits,
            ushort expectedStatus)
        {
            Reset();

            CreateDataSegment(new ReadOnlySpan<byte>(), 2);
            mbbsEmuCpuRegisters.DS = 2;
            mbbsEmuCpuRegisters.Fpu.SetStackTop(0);
            mbbsEmuCpuCore.FpuStack[0] = new Float80(signExponent, significand);

            var instructions = new Assembler(16);
            if (size == 8)
                instructions.fst(__qword_ptr[0]);
            else
                instructions.fst(__dword_ptr[0]);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            var stored = size == 8 ? BitConverter.ToUInt64(mbbsEmuMemoryCore.GetArray(2, 0, 8)) : BitConverter.ToUInt32(mbbsEmuMemoryCore.GetArray(2, 0, 4));
            Assert.Equal(expectedBits, stored);
            Assert.Equal(expectedStatus, mbbsEmuCpuRegisters.Fpu.StatusWord & 0x023F);
        }
    }
}
