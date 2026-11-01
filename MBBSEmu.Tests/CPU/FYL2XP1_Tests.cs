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
    }
}
