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
    }
}
