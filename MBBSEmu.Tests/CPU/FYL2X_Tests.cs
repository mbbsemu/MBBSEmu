using Iced.Intel;
using MBBSEmu.CPU;
using MBBSEmu.Extensions;
using System;
using Xunit;

namespace MBBSEmu.Tests.CPU
{
    public class FYL2X_Tests : CpuTestBase
    {
        [Theory]
        [InlineData(8d, 1d)]
        [InlineData(2d, 3d)]
        [InlineData(100d, 1d)]
        [InlineData(0d, 5d)]
        [InlineData(0d, -5d)]
        [InlineData(double.PositiveInfinity, 5d)]
        [InlineData(double.PositiveInfinity, -5d)]
        public void FYL2X_Test(double ST0Value, double ST1Value)
        {
            Reset();

            mbbsEmuCpuRegisters.Fpu.SetStackTop(1);
            mbbsEmuCpuCore.FpuStack[1] = ST0Value; //ST0
            mbbsEmuCpuCore.FpuStack[0] = ST1Value; //ST1

            var instructions = new Assembler(16);
            instructions.fyl2x();
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(ST1Value * Math.Log2(ST0Value), mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackTop()]);
        }

        [Theory]
        [InlineData(-4d, 5d)] // negative ST(0) is an invalid operand
        [InlineData(double.NaN, 5d)]
        [InlineData(4d, double.NaN)]
        [InlineData(0d, 0d)] // 0 * log2(0) == 0 * -Infinity, indeterminate
        [InlineData(double.PositiveInfinity, 0d)] // 0 * log2(+Infinity) == 0 * +Infinity, indeterminate
        public void FYL2X_InvalidOperand_ReturnsNaNAndSetsInvalidOperationFlag(double ST0Value, double ST1Value)
        {
            Reset();

            mbbsEmuCpuRegisters.Fpu.SetStackTop(1);
            mbbsEmuCpuCore.FpuStack[1] = ST0Value; //ST0
            mbbsEmuCpuCore.FpuStack[0] = ST1Value; //ST1

            var instructions = new Assembler(16);
            instructions.fyl2x();
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(double.NaN, mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackTop()]);
            Assert.True(mbbsEmuCpuRegisters.Fpu.StatusWord.IsFlagSet((ushort)EnumFpuStatusFlags.InvalidOperationException));
        }

        /// <summary>
        ///     Operands outside double's range. Expected values: an x87 coprocessor, compared to 52 bits (the logarithm
        ///     is computed through double); the IE and ZE flags exactly.
        /// </summary>
        [Theory]
        [InlineData((ushort)0x3FFF, 0xA000000000000000UL, (ushort)0x43FF, 0x8000000000000000UL, (ushort)0x43FD, 0xA4D3C25E68DC57F2UL, (ushort)0)] //2^1024 * log2(1.25)
        [InlineData((ushort)0x5387, 0x8000000000000000UL, (ushort)0x3FFF, 0x8000000000000000UL, (ushort)0x400B, 0x9C40000000000000UL, (ushort)0)] //log2(2^5000) = 5000
        [InlineData((ushort)0x2C77, 0xC000000000000000UL, (ushort)0x3FFF, 0x8000000000000000UL, (ushort)0xC00B, 0x9C3B51FF2E30214CUL, (ushort)0)] //log2(1.5 * 2^-5000)
        [InlineData((ushort)0x4001, 0x8000000000000000UL, (ushort)0x7E7F, 0x8000000000000000UL, (ushort)0x7E80, 0x8000000000000000UL, (ushort)0)] //2^16000 * log2(4) = 2^16001
        [InlineData((ushort)0x0000, 0x0000000000000000UL, (ushort)0x47CF, 0x8000000000000000UL, (ushort)0xFFFF, 0x8000000000000000UL, (ushort)0x04)] //2^2000 * log2(0): -infinity, ZE
        [InlineData((ushort)0x3FFF, 0x8000000000000000UL, (ushort)0x7FFF, 0x8000000000000000UL, (ushort)0xFFFF, 0xC000000000000000UL, (ushort)0x01)] //+infinity * log2(1): invalid, IE
        public void FYL2X_OutsideDoubleRange(ushort ST0SignExponent, ulong ST0Significand, ushort ST1SignExponent,
            ulong ST1Significand, ushort expectedSignExponent, ulong expectedSignificand, ushort expectedExceptions)
        {
            Reset();

            mbbsEmuCpuRegisters.Fpu.SetStackTop(1);
            mbbsEmuCpuCore.FpuStack[1] = new Float80(ST0SignExponent, ST0Significand); //ST0
            mbbsEmuCpuCore.FpuStack[0] = new Float80(ST1SignExponent, ST1Significand); //ST1

            var instructions = new Assembler(16);
            instructions.fyl2x();
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            var result = mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackTop()];
            Assert.Equal(expectedSignExponent, result.SignExponent);
            Assert.InRange((long)(result.Significand - expectedSignificand), -4096, 4096);
            Assert.Equal(expectedExceptions, mbbsEmuCpuRegisters.Fpu.StatusWord & 0x05);
        }

        /// <summary>
        ///     ST(0) next to 1, whose logarithm rounding ST(0) to double would lose. Expected values: an x87
        ///     coprocessor, compared to 52 bits; log2(1) is +0 under every rounding control.
        /// </summary>
        [Theory]
        [InlineData((ushort)0x037F, (ushort)0x3FFF, 0x8000000000000001UL, (ushort)0x403E, 0x8000000000000000UL, (ushort)0x3FFF, 0xB8AA3B295C17F0BBUL)] //2^63 * log2(1 + 2^-63)
        [InlineData((ushort)0x037F, (ushort)0x3FFE, 0xFFFFFFFFFFFFFFFFUL, (ushort)0x403F, 0x8000000000000000UL, (ushort)0xBFFF, 0xB8AA3B295C17F0BCUL)] //2^64 * log2(1 - 2^-64)
        [InlineData((ushort)0x037F, (ushort)0x3FFF, 0x8000000000000400UL, (ushort)0x3FFF, 0x8000000000000000UL, (ushort)0x3FCA, 0xB8AA3B295C17EDD9UL)] //log2(1 + 2^-53)
        [InlineData((ushort)0x037F, (ushort)0x3FFF, 0x8000010000000000UL, (ushort)0x3FFF, 0x8000000000000000UL, (ushort)0x3FE8, 0xB8AA3A70B1DDBD98UL)] //log2(1 + 2^-23)
        [InlineData((ushort)0x037F, (ushort)0x3FFE, 0xFFFFFFFFFFFFF000UL, (ushort)0x47CF, 0x8000000000000000UL, (ushort)0xC79B, 0xB8AA3B295C17F681UL)] //2^2000 * log2(1 - 2^-52)
        [InlineData((ushort)0x037F, (ushort)0x3FFE, 0xC000000000000000UL, (ushort)0x3FFF, 0x8000000000000000UL, (ushort)0xBFFD, 0xD47FCB8C0852F0C1UL)] //log2(0.75)
        [InlineData((ushort)0x037F, (ushort)0x3FFF, 0xF333333333333333UL, (ushort)0x3FFF, 0x8000000000000000UL, (ushort)0x3FFE, 0xED0E4C42D061E84EUL)] //log2(1.9)
        [InlineData((ushort)0x037F, (ushort)0x3FFE, 0x8000000000000000UL, (ushort)0x3FFF, 0x8000000000000000UL, (ushort)0xBFFF, 0x8000000000000000UL)] //log2(0.5) = -1
        [InlineData((ushort)0x037F, (ushort)0x3FFF, 0x8000000000000000UL, (ushort)0xBFFF, 0xC000000000000000UL, (ushort)0x8000, 0x0000000000000000UL)] //-1.5 * log2(1) = -0
        [InlineData((ushort)0x077F, (ushort)0x3FFF, 0x8000000000000000UL, (ushort)0x3FFF, 0xC000000000000000UL, (ushort)0x0000, 0x0000000000000000UL)] //1.5 * log2(1) = +0, rounding down
        [InlineData((ushort)0x077F, (ushort)0x3FFF, 0x8000000000000000UL, (ushort)0xBFFF, 0xC000000000000000UL, (ushort)0x8000, 0x0000000000000000UL)] //-1.5 * log2(1) = -0, rounding down
        public void FYL2X_NearOne(ushort controlWord, ushort ST0SignExponent, ulong ST0Significand,
            ushort ST1SignExponent, ulong ST1Significand, ushort expectedSignExponent, ulong expectedSignificand)
        {
            Reset();

            mbbsEmuCpuRegisters.Fpu.ControlWord = controlWord;
            mbbsEmuCpuRegisters.Fpu.SetStackTop(1);
            mbbsEmuCpuCore.FpuStack[1] = new Float80(ST0SignExponent, ST0Significand); //ST0
            mbbsEmuCpuCore.FpuStack[0] = new Float80(ST1SignExponent, ST1Significand); //ST1

            var instructions = new Assembler(16);
            instructions.fyl2x();
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            var result = mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackTop()];
            Assert.Equal(expectedSignExponent, result.SignExponent);
            Assert.InRange((long)(result.Significand - expectedSignificand), -4096, 4096);
        }

        /// <summary>
        ///     The transcendentals ignore the precision control: the result keeps 64 bits, rounded to the rounding
        ///     control. Expected values: an x87 coprocessor, compared to 52 bits.
        /// </summary>
        [Theory]
        [InlineData((ushort)0x007F, (ushort)0x4000, 0x8000000000000000UL, (ushort)0x3FFF, 0x8000000200000000UL, (ushort)0x3FFF, 0x8000000200000000UL)] //PC 24: (1 + 2^-30) * log2(2)
        [InlineData((ushort)0x027F, (ushort)0x4000, 0x8000000000000000UL, (ushort)0x3FFF, 0x8000000200000000UL, (ushort)0x3FFF, 0x8000000200000000UL)] //PC 53: (1 + 2^-30) * log2(2)
        [InlineData((ushort)0x0C7F, (ushort)0x4000, 0x8000000000000000UL, (ushort)0x3FFF, 0x8000000200000000UL, (ushort)0x3FFF, 0x8000000200000000UL)] //PC 24, toward zero: (1 + 2^-30) * log2(2)
        [InlineData((ushort)0x007F, (ushort)0x4000, 0xC000000000000000UL, (ushort)0x3FFF, 0x8000000200000000UL, (ushort)0x3FFF, 0xCAE00D20296B7844UL)] //PC 24: (1 + 2^-30) * log2(3)
        [InlineData((ushort)0x007F, (ushort)0x3FFF, 0x8000000000000001UL, (ushort)0x403E, 0x8000000000000000UL, (ushort)0x3FFF, 0xB8AA3B295C17F0BBUL)] //PC 24: 2^63 * log2(1 + 2^-63)
        public void FYL2X_IgnoresPrecisionControl(ushort controlWord, ushort ST0SignExponent, ulong ST0Significand,
            ushort ST1SignExponent, ulong ST1Significand, ushort expectedSignExponent, ulong expectedSignificand)
        {
            Reset();

            mbbsEmuCpuRegisters.Fpu.ControlWord = controlWord;
            mbbsEmuCpuRegisters.Fpu.SetStackTop(1);
            mbbsEmuCpuCore.FpuStack[1] = new Float80(ST0SignExponent, ST0Significand); //ST0
            mbbsEmuCpuCore.FpuStack[0] = new Float80(ST1SignExponent, ST1Significand); //ST1

            var instructions = new Assembler(16);
            instructions.fyl2x();
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            var result = mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackTop()];
            Assert.Equal(expectedSignExponent, result.SignExponent);
            Assert.InRange((long)(result.Significand - expectedSignificand), -4096, 4096);
        }
    }
}
