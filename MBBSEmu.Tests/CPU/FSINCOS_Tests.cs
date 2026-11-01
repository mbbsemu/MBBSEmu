using Iced.Intel;
using MBBSEmu.CPU;
using MBBSEmu.Extensions;
using System;
using Xunit;

namespace MBBSEmu.Tests.CPU
{
    public class FSINCOS_Tests : CpuTestBase
    {
        [Theory]
        [InlineData(1d)]
        [InlineData(-1d)]
        [InlineData(0d)]
        public void FSINCOS_Test(double ST0Value)
        {
            Reset();

            mbbsEmuCpuRegisters.Fpu.SetStackTop(0);
            mbbsEmuCpuCore.FpuStack[0] = ST0Value;

            var instructions = new Assembler(16);
            instructions.fsincos();
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            //ST(0) becomes cosine, ST(1) becomes sine
            Assert.Equal(Math.Cos(ST0Value), mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackTop()]);
            Assert.Equal(Math.Sin(ST0Value), mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackPointer(Register.ST1)]);
        }

        /// <summary>
        ///     |ST(0)| >= 2^63 is out of range: C2 set, C1 cleared, ST(0) and the stack unchanged, no exception (an x87
        ///     coprocessor)
        /// </summary>
        [Theory]
        [InlineData((ushort)0x403E, 0x8000000000000000UL)] //2^63
        [InlineData((ushort)0xC03E, 0x8000000000000000UL)] //-2^63
        [InlineData((ushort)0x43FF, 0xFFFFFFFFFFFFF800UL)] //beyond double's range
        public void FSINCOS_OutOfRange_SetsC2(ushort ST0SignExponent, ulong ST0Significand)
        {
            Reset();

            mbbsEmuCpuRegisters.Fpu.SetStackTop(1);
            mbbsEmuCpuCore.FpuStack[1] = new Float80(ST0SignExponent, ST0Significand);
            mbbsEmuCpuRegisters.Fpu.SetFlag(EnumFpuStatusFlags.Code1);

            var instructions = new Assembler(16);
            instructions.fsincos();
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(new Float80(ST0SignExponent, ST0Significand), mbbsEmuCpuCore.FpuStack[1]);
            Assert.Equal(1, mbbsEmuCpuRegisters.Fpu.GetStackTop());
            Assert.True(mbbsEmuCpuRegisters.Fpu.StatusWord.IsFlagSet((ushort)EnumFpuStatusFlags.Code2));
            Assert.False(mbbsEmuCpuRegisters.Fpu.StatusWord.IsFlagSet((ushort)EnumFpuStatusFlags.Code1));
            Assert.Equal(0, mbbsEmuCpuRegisters.Fpu.StatusWord & 0x3F);
        }

        /// <summary>
        ///     The largest value below 2^63 is in range: C2 cleared, the value pushed (an x87 coprocessor)
        /// </summary>
        [Fact]
        public void FSINCOS_LargestInRange_ClearsC2()
        {
            Reset();

            mbbsEmuCpuRegisters.Fpu.SetStackTop(1);
            mbbsEmuCpuCore.FpuStack[1] = new Float80(0x403D, 0xFFFFFFFFFFFFFFFF);
            mbbsEmuCpuRegisters.Fpu.SetFlag(EnumFpuStatusFlags.Code2);

            var instructions = new Assembler(16);
            instructions.fsincos();
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(2, mbbsEmuCpuRegisters.Fpu.GetStackTop());
            Assert.False(mbbsEmuCpuRegisters.Fpu.StatusWord.IsFlagSet((ushort)EnumFpuStatusFlags.Code2));
        }

        /// <summary>
        ///     An infinity is an invalid operand: ST(0) and the pushed value become the real indefinite, IE, C1 and C2
        ///     cleared (an x87 coprocessor)
        /// </summary>
        [Theory]
        [InlineData(double.PositiveInfinity)]
        [InlineData(double.NegativeInfinity)]
        public void FSINCOS_Infinity_IsInvalid(double ST0Value)
        {
            Reset();

            mbbsEmuCpuRegisters.Fpu.SetStackTop(1);
            mbbsEmuCpuCore.FpuStack[1] = ST0Value;
            mbbsEmuCpuRegisters.Fpu.SetFlag(EnumFpuStatusFlags.Code1);
            mbbsEmuCpuRegisters.Fpu.SetFlag(EnumFpuStatusFlags.Code2);

            var instructions = new Assembler(16);
            instructions.fsincos();
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(2, mbbsEmuCpuRegisters.Fpu.GetStackTop());
            Assert.Equal(Float80.Indefinite, mbbsEmuCpuCore.FpuStack[2]);
            Assert.Equal(Float80.Indefinite, mbbsEmuCpuCore.FpuStack[1]);
            Assert.Equal(0x01, mbbsEmuCpuRegisters.Fpu.StatusWord & 0x3F);
            Assert.False(mbbsEmuCpuRegisters.Fpu.StatusWord.IsFlagSet((ushort)EnumFpuStatusFlags.Code1));
            Assert.False(mbbsEmuCpuRegisters.Fpu.StatusWord.IsFlagSet((ushort)EnumFpuStatusFlags.Code2));
        }

        /// <summary>
        ///     A NaN is made quiet (IE only if it was signalling) and an unsupported encoding gives the real indefinite
        ///     (IE), in ST(0) and pushed again; C1 and C2 are cleared.
        ///     Expected values: an x87 coprocessor, with C0 to C3 set beforehand.
        /// </summary>
        [Theory]
        [InlineData((ushort)0x7FFF, 0xC000000000000001UL, (ushort)0x7FFF, 0xC000000000000001UL, (ushort)0x00)] //+quiet NaN
        [InlineData((ushort)0xFFFF, 0xE000000000000000UL, (ushort)0xFFFF, 0xE000000000000000UL, (ushort)0x00)] //-quiet NaN, larger significand
        [InlineData((ushort)0x7FFF, 0xA000000000000000UL, (ushort)0x7FFF, 0xE000000000000000UL, (ushort)0x01)] //+signalling NaN
        [InlineData((ushort)0xFFFF, 0x8000000000000001UL, (ushort)0xFFFF, 0xC000000000000001UL, (ushort)0x01)] //-signalling NaN, smaller significand
        [InlineData((ushort)0x4000, 0x4000000000000000UL, (ushort)0xFFFF, 0xC000000000000000UL, (ushort)0x01)] //an unnormal
        [InlineData((ushort)0x7FFF, 0x0000000000000000UL, (ushort)0xFFFF, 0xC000000000000000UL, (ushort)0x01)] //a pseudo-infinity
        [InlineData((ushort)0x7FFF, 0x4000000000000000UL, (ushort)0xFFFF, 0xC000000000000000UL, (ushort)0x01)] //a pseudo-NaN
        public void FSINCOS_NaN(ushort ST0SignExponent, ulong ST0Significand, ushort expectedSignExponent,
            ulong expectedSignificand, ushort expectedExceptions)
        {
            Reset();

            mbbsEmuCpuRegisters.Fpu.SetStackTop(1);
            mbbsEmuCpuCore.FpuStack[1] = new Float80(ST0SignExponent, ST0Significand);
            mbbsEmuCpuRegisters.Fpu.SetFlag(EnumFpuStatusFlags.Code1);
            mbbsEmuCpuRegisters.Fpu.SetFlag(EnumFpuStatusFlags.Code2);

            var instructions = new Assembler(16);
            instructions.fsincos();
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(2, mbbsEmuCpuRegisters.Fpu.GetStackTop());
            Assert.Equal(new Float80(expectedSignExponent, expectedSignificand), mbbsEmuCpuCore.FpuStack[2]);
            Assert.Equal(new Float80(expectedSignExponent, expectedSignificand), mbbsEmuCpuCore.FpuStack[1]);
            Assert.Equal(expectedExceptions, mbbsEmuCpuRegisters.Fpu.StatusWord & 0x3F);
            Assert.False(mbbsEmuCpuRegisters.Fpu.StatusWord.IsFlagSet((ushort)EnumFpuStatusFlags.Code1));
            Assert.False(mbbsEmuCpuRegisters.Fpu.StatusWord.IsFlagSet((ushort)EnumFpuStatusFlags.Code2));
        }

        /// <summary>
        ///     |ST(0)| < 2^-68, zeros and denormals included: the sine is ST(0) itself, normalized, the cosine 1.
        ///     PE unless ST(0) is zero, DE for a denormal, UE when the sine is one; C1 and C2 cleared.
        ///     Expected values: an x87 coprocessor, with C1 and C2 set beforehand.
        /// </summary>
        [Theory]
        [InlineData((ushort)0x0000, 0x0000000000000001UL, (ushort)0x0000, 0x0000000000000001UL, (ushort)0x32)] //the smallest denormal
        [InlineData((ushort)0x8000, 0x0000000000000001UL, (ushort)0x8000, 0x0000000000000001UL, (ushort)0x32)] //the smallest negative denormal
        [InlineData((ushort)0x0000, 0x4000000000000000UL, (ushort)0x0000, 0x4000000000000000UL, (ushort)0x32)] //a denormal
        [InlineData((ushort)0x0000, 0x7FFFFFFFFFFFFFFFUL, (ushort)0x0000, 0x7FFFFFFFFFFFFFFFUL, (ushort)0x32)] //the largest denormal
        [InlineData((ushort)0x0000, 0x8000000000000000UL, (ushort)0x0001, 0x8000000000000000UL, (ushort)0x22)] //a pseudo-denormal (normalized)
        [InlineData((ushort)0x0001, 0x8000000000000000UL, (ushort)0x0001, 0x8000000000000000UL, (ushort)0x20)] //the smallest normal
        [InlineData((ushort)0x3FBA, 0xFFFFFFFFFFFFFFFFUL, (ushort)0x3FBA, 0xFFFFFFFFFFFFFFFFUL, (ushort)0x20)] //just below 2^-68
        [InlineData((ushort)0xBFBA, 0x8000000000000000UL, (ushort)0xBFBA, 0x8000000000000000UL, (ushort)0x20)] //-2^-69
        [InlineData((ushort)0x3000, 0xC000000000000000UL, (ushort)0x3000, 0xC000000000000000UL, (ushort)0x20)] //1.5 * 2^-4095, below double's range
        [InlineData((ushort)0x0000, 0x0000000000000000UL, (ushort)0x0000, 0x0000000000000000UL, (ushort)0x00)] //+0
        [InlineData((ushort)0x8000, 0x0000000000000000UL, (ushort)0x8000, 0x0000000000000000UL, (ushort)0x00)] //-0
        public void FSINCOS_Tiny(ushort ST0SignExponent, ulong ST0Significand, ushort expectedSignExponent,
            ulong expectedSignificand, ushort expectedExceptions)
        {
            Reset();

            mbbsEmuCpuRegisters.Fpu.SetStackTop(1);
            mbbsEmuCpuCore.FpuStack[1] = new Float80(ST0SignExponent, ST0Significand);
            mbbsEmuCpuRegisters.Fpu.SetFlag(EnumFpuStatusFlags.Code1);
            mbbsEmuCpuRegisters.Fpu.SetFlag(EnumFpuStatusFlags.Code2);

            var instructions = new Assembler(16);
            instructions.fsincos();
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(2, mbbsEmuCpuRegisters.Fpu.GetStackTop());
            Assert.Equal(Float80.One, mbbsEmuCpuCore.FpuStack[2]);
            Assert.Equal(new Float80(expectedSignExponent, expectedSignificand), mbbsEmuCpuCore.FpuStack[1]);
            Assert.Equal(expectedExceptions, mbbsEmuCpuRegisters.Fpu.StatusWord & 0x3F);
            Assert.False(mbbsEmuCpuRegisters.Fpu.StatusWord.IsFlagSet((ushort)EnumFpuStatusFlags.Code1));
            Assert.False(mbbsEmuCpuRegisters.Fpu.StatusWord.IsFlagSet((ushort)EnumFpuStatusFlags.Code2));
        }
    }
}
