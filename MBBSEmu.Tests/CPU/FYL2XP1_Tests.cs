using Iced.Intel;
using MBBSEmu.CPU;
using MBBSEmu.Extensions;
using System;
using Xunit;

namespace MBBSEmu.Tests.CPU
{
    public class FYL2XP1_Tests : CpuTestBase
    {
        [Theory]
        [InlineData(0.25d, 1d)]
        [InlineData(-0.5d, 3d)]
        [InlineData(0d, 1d)]
        [InlineData(-1d, 5d)] // (ST0 + 1.0) == 0, ST1 nonzero -> signed Infinity, still valid
        public void FYL2XP1_Test(double ST0Value, double ST1Value)
        {
            Reset();

            mbbsEmuCpuRegisters.Fpu.SetStackTop(1);
            mbbsEmuCpuCore.FpuStack[1] = ST0Value; //ST0
            mbbsEmuCpuCore.FpuStack[0] = ST1Value; //ST1

            var instructions = new Assembler(16);
            instructions.fyl2xp1();
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(ST1Value * Math.Log2(ST0Value + 1.0), mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackTop()]);
        }

        [Theory]
        [InlineData(-4d, 5d)] // (ST0 + 1.0) negative is an invalid operand
        [InlineData(double.NaN, 5d)]
        [InlineData(4d, double.NaN)]
        [InlineData(-1d, 0d)] // (ST0 + 1.0) == 0, ST1 == 0 -> indeterminate
        public void FYL2XP1_InvalidOperand_ReturnsNaNAndSetsInvalidOperationFlag(double ST0Value, double ST1Value)
        {
            Reset();

            mbbsEmuCpuRegisters.Fpu.SetStackTop(1);
            mbbsEmuCpuCore.FpuStack[1] = ST0Value; //ST0
            mbbsEmuCpuCore.FpuStack[0] = ST1Value; //ST1

            var instructions = new Assembler(16);
            instructions.fyl2xp1();
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
        [InlineData((ushort)0x3FFD, 0x8000000000000000UL, (ushort)0x43FF, 0x8000000000000000UL, (ushort)0x43FD, 0xA4D3C25E68DC57F2UL, (ushort)0)] //2^1024 * log2(1.25)
        [InlineData((ushort)0xBFFD, 0x8000000000000000UL, (ushort)0x444B, 0x8000000000000000UL, (ushort)0xC449, 0xD47FCB8C0852F0C1UL, (ushort)0)] //2^1100 * log2(0.75)
        public void FYL2XP1_OutsideDoubleRange(ushort ST0SignExponent, ulong ST0Significand, ushort ST1SignExponent,
            ulong ST1Significand, ushort expectedSignExponent, ulong expectedSignificand, ushort expectedExceptions)
        {
            Reset();

            mbbsEmuCpuRegisters.Fpu.SetStackTop(1);
            mbbsEmuCpuCore.FpuStack[1] = new Float80(ST0SignExponent, ST0Significand); //ST0
            mbbsEmuCpuCore.FpuStack[0] = new Float80(ST1SignExponent, ST1Significand); //ST1

            var instructions = new Assembler(16);
            instructions.fyl2xp1();
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            var result = mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackTop()];
            Assert.Equal(expectedSignExponent, result.SignExponent);
            Assert.InRange((long)(result.Significand - expectedSignificand), -4096, 4096);
            Assert.Equal(expectedExceptions, mbbsEmuCpuRegisters.Fpu.StatusWord & 0x05);
        }

        /// <summary>
        ///     log2(1 + ST(0)) keeps a small ST(0)'s precision, down to values far below double's range. Expected
        ///     values: an x87 coprocessor (ST(1) = 1), compared to 52 bits (the logarithm is computed through double).
        /// </summary>
        [Theory]
        [InlineData((ushort)0x3FFC, 0xCCCCCCCCCCCCCCCDUL, (ushort)0x3FFD, 0x86AC72158ED0B74DUL)] //0.2
        [InlineData((ushort)0xBFFC, 0xCCCCCCCCCCCCCCCDUL, (ushort)0xBFFD, 0xA4D3C25E68DC57F2UL)] //-0.2
        [InlineData((ushort)0x3FF5, 0xB6477178EF394F37UL, (ushort)0x3FF6, 0x83652CDCEC9B7F32UL)] //about 2^-10
        [InlineData((ushort)0xBFE1, 0xB66EBCC7E82E3634UL, (ushort)0xBFE2, 0x8398E763819F1483UL)] //about -2^-30
        [InlineData((ushort)0x3FCA, 0xD00783641DDA6556UL, (ushort)0x3FCB, 0x960FBB808D430D7CUL)] //about 2^-53
        [InlineData((ushort)0xBFC3, 0xB6E398B3F3F87A21UL, (ushort)0xBFC4, 0x83ED3328BF4A6E2AUL)] //about -2^-60
        [InlineData((ushort)0x3FB0, 0xDE0ED9839A28F9ABUL, (ushort)0x3FB1, 0xA02E558454961795UL)] //about 2^-79
        [InlineData((ushort)0x3BCD, 0x9D91F85E26426F56UL, (ushort)0x3BCD, 0xE35363B966A5A729UL)] //about 2^-1074
        [InlineData((ushort)0xBBB3, 0x8665E9C0B3E9FCB8UL, (ushort)0xBBB3, 0xC1E53D7DDDA563CAUL)] //about -2^-1100
        [InlineData((ushort)0x017F, 0xF41A9C6D0A765C73UL, (ushort)0x0180, 0xB0157283D20E9813UL)] //about 2^-16000
        public void FYL2XP1_SmallOperand(ushort ST0SignExponent, ulong ST0Significand, ushort expectedSignExponent,
            ulong expectedSignificand)
        {
            Reset();

            mbbsEmuCpuRegisters.Fpu.SetStackTop(1);
            mbbsEmuCpuCore.FpuStack[1] = new Float80(ST0SignExponent, ST0Significand); //ST0
            mbbsEmuCpuCore.FpuStack[0] = Float80.One; //ST1

            var instructions = new Assembler(16);
            instructions.fyl2xp1();
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
        [InlineData((ushort)0x007F, (ushort)0x3FFD, 0x8000000000000000UL, (ushort)0x3FFF, 0x8000000000000000UL, (ushort)0x3FFD, 0xA4D3C25E68DC57F2UL)] //PC 24: log2(1.25)
        [InlineData((ushort)0x027F, (ushort)0x3FFD, 0x8000000000000000UL, (ushort)0x3FFF, 0x8000000000000000UL, (ushort)0x3FFD, 0xA4D3C25E68DC57F2UL)] //PC 53: log2(1.25)
        [InlineData((ushort)0x0C7F, (ushort)0x3FFD, 0x8000000000000000UL, (ushort)0x3FFF, 0x8000000000000000UL, (ushort)0x3FFD, 0xA4D3C25E68DC57F2UL)] //PC 24, toward zero: log2(1.25)
        [InlineData((ushort)0x007F, (ushort)0xBFFD, 0x8000000000000000UL, (ushort)0x3FFF, 0x8000000000000000UL, (ushort)0xBFFD, 0xD47FCB8C0852F0C1UL)] //PC 24: log2(0.75)
        public void FYL2XP1_IgnoresPrecisionControl(ushort controlWord, ushort ST0SignExponent, ulong ST0Significand,
            ushort ST1SignExponent, ulong ST1Significand, ushort expectedSignExponent, ulong expectedSignificand)
        {
            Reset();

            mbbsEmuCpuRegisters.Fpu.ControlWord = controlWord;
            mbbsEmuCpuRegisters.Fpu.SetStackTop(1);
            mbbsEmuCpuCore.FpuStack[1] = new Float80(ST0SignExponent, ST0Significand); //ST0
            mbbsEmuCpuCore.FpuStack[0] = new Float80(ST1SignExponent, ST1Significand); //ST1

            var instructions = new Assembler(16);
            instructions.fyl2xp1();
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            var result = mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackTop()];
            Assert.Equal(expectedSignExponent, result.SignExponent);
            Assert.InRange((long)(result.Significand - expectedSignificand), -4096, 4096);
        }

        /// <summary>
        ///     A zero or infinite ST(1) gives an exact signed zero or infinity, with no precision exception (an x87
        ///     coprocessor: the result and the exception flags exactly)
        /// </summary>
        [Theory]
        [InlineData((ushort)0x3FFC, 0xCCCCCCCCCCCCCCCDUL, (ushort)0x0000, 0x0000000000000000UL, (ushort)0x0000, 0x0000000000000000UL, (ushort)0x0000)] //+0 * log2(1.2)
        [InlineData((ushort)0x3FFC, 0xCCCCCCCCCCCCCCCDUL, (ushort)0x8000, 0x0000000000000000UL, (ushort)0x8000, 0x0000000000000000UL, (ushort)0x0000)] //-0 * log2(1.2)
        [InlineData((ushort)0x3FFC, 0xCCCCCCCCCCCCCCCDUL, (ushort)0x7FFF, 0x8000000000000000UL, (ushort)0x7FFF, 0x8000000000000000UL, (ushort)0x0000)] //+infinity * log2(1.2)
        [InlineData((ushort)0x3FFC, 0xCCCCCCCCCCCCCCCDUL, (ushort)0xFFFF, 0x8000000000000000UL, (ushort)0xFFFF, 0x8000000000000000UL, (ushort)0x0000)] //-infinity * log2(1.2)
        [InlineData((ushort)0xBFFC, 0xCCCCCCCCCCCCCCCDUL, (ushort)0x0000, 0x0000000000000000UL, (ushort)0x8000, 0x0000000000000000UL, (ushort)0x0000)] //+0 * log2(0.8) = -0
        [InlineData((ushort)0xBFFC, 0xCCCCCCCCCCCCCCCDUL, (ushort)0x7FFF, 0x8000000000000000UL, (ushort)0xFFFF, 0x8000000000000000UL, (ushort)0x0000)] //+infinity * log2(0.8) = -infinity
        [InlineData((ushort)0x0000, 0x0000000000000000UL, (ushort)0x7FFF, 0x8000000000000000UL, (ushort)0xFFFF, 0xC000000000000000UL, (ushort)0x0001)] //+infinity * log2(1): invalid
        [InlineData((ushort)0x0000, 0x0000000000000001UL, (ushort)0x0000, 0x0000000000000000UL, (ushort)0x0000, 0x0000000000000000UL, (ushort)0x0002)] //+0 * log2(1 + denormal), DE
        [InlineData((ushort)0x8000, 0x0000000000000001UL, (ushort)0x7FFF, 0x8000000000000000UL, (ushort)0xFFFF, 0x8000000000000000UL, (ushort)0x0002)] //+infinity * log2(1 - denormal), DE
        public void FYL2XP1_ZeroOrInfiniteST1(ushort ST0SignExponent, ulong ST0Significand, ushort ST1SignExponent,
            ulong ST1Significand, ushort expectedSignExponent, ulong expectedSignificand, ushort expectedExceptions)
        {
            Reset();

            mbbsEmuCpuRegisters.Fpu.SetStackTop(1);
            mbbsEmuCpuCore.FpuStack[1] = new Float80(ST0SignExponent, ST0Significand); //ST0
            mbbsEmuCpuCore.FpuStack[0] = new Float80(ST1SignExponent, ST1Significand); //ST1

            var instructions = new Assembler(16);
            instructions.fyl2xp1();
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(new Float80(expectedSignExponent, expectedSignificand),
                mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackTop()]);
            Assert.Equal(expectedExceptions, mbbsEmuCpuRegisters.Fpu.StatusWord & 0x3F);
        }
    }
}
