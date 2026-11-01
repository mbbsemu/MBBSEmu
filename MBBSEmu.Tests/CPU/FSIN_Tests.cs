using Iced.Intel;
using MBBSEmu.CPU;
using MBBSEmu.Extensions;
using System;
using Xunit;

namespace MBBSEmu.Tests.CPU
{
    public class FSIN_Tests : CpuTestBase
    {
        [Theory]
        [InlineData(1d, 0.8414709848078965)]
        [InlineData(-1d, -0.8414709848078965)]
        [InlineData(double.NaN, double.NaN)]
        [InlineData(0, 0)]
        public void FSIN_Tets(double ST0Value, double expectedValue)
        {
            Reset();

            mbbsEmuCpuRegisters.Fpu.ControlWord = 0;
            mbbsEmuCpuRegisters.Fpu.SetStackTop(0);
            mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackTop()] = ST0Value; //ST0

            var instructions = new Assembler(16);
            instructions.fsin();
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(expectedValue, mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackTop()]);
            Assert.Equal(0, mbbsEmuCpuRegisters.Fpu.ControlWord);
        }

        /// <summary>
        ///     |ST(0)| >= 2^63 is out of range: C2 set, C1 cleared, ST(0) and the stack unchanged, no exception (an x87
        ///     coprocessor)
        /// </summary>
        [Theory]
        [InlineData((ushort)0x403E, 0x8000000000000000UL)] //2^63
        [InlineData((ushort)0xC03E, 0x8000000000000000UL)] //-2^63
        [InlineData((ushort)0x43FF, 0xFFFFFFFFFFFFF800UL)] //beyond double's range
        public void FSIN_OutOfRange_SetsC2(ushort ST0SignExponent, ulong ST0Significand)
        {
            Reset();

            mbbsEmuCpuRegisters.Fpu.SetStackTop(1);
            mbbsEmuCpuCore.FpuStack[1] = new Float80(ST0SignExponent, ST0Significand);
            mbbsEmuCpuRegisters.Fpu.SetFlag(EnumFpuStatusFlags.Code1);

            var instructions = new Assembler(16);
            instructions.fsin();
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(new Float80(ST0SignExponent, ST0Significand), mbbsEmuCpuCore.FpuStack[1]);
            Assert.Equal(1, mbbsEmuCpuRegisters.Fpu.GetStackTop());
            Assert.True(mbbsEmuCpuRegisters.Fpu.StatusWord.IsFlagSet((ushort)EnumFpuStatusFlags.Code2));
            Assert.False(mbbsEmuCpuRegisters.Fpu.StatusWord.IsFlagSet((ushort)EnumFpuStatusFlags.Code1));
            Assert.Equal(0, mbbsEmuCpuRegisters.Fpu.StatusWord & 0x3F);
        }

        /// <summary>
        ///     The largest value below 2^63 is in range: C2 cleared (an x87 coprocessor)
        /// </summary>
        [Fact]
        public void FSIN_LargestInRange_ClearsC2()
        {
            Reset();

            mbbsEmuCpuRegisters.Fpu.SetStackTop(1);
            mbbsEmuCpuCore.FpuStack[1] = new Float80(0x403D, 0xFFFFFFFFFFFFFFFF);
            mbbsEmuCpuRegisters.Fpu.SetFlag(EnumFpuStatusFlags.Code2);

            var instructions = new Assembler(16);
            instructions.fsin();
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(1, mbbsEmuCpuRegisters.Fpu.GetStackTop());
            Assert.False(mbbsEmuCpuRegisters.Fpu.StatusWord.IsFlagSet((ushort)EnumFpuStatusFlags.Code2));
        }

        /// <summary>
        ///     An infinity is an invalid operand: ST(0) becomes the real indefinite, IE, C1 and C2 cleared (an x87
        ///     coprocessor)
        /// </summary>
        [Theory]
        [InlineData(double.PositiveInfinity)]
        [InlineData(double.NegativeInfinity)]
        public void FSIN_Infinity_IsInvalid(double ST0Value)
        {
            Reset();

            mbbsEmuCpuRegisters.Fpu.SetStackTop(1);
            mbbsEmuCpuCore.FpuStack[1] = ST0Value;
            mbbsEmuCpuRegisters.Fpu.SetFlag(EnumFpuStatusFlags.Code1);
            mbbsEmuCpuRegisters.Fpu.SetFlag(EnumFpuStatusFlags.Code2);

            var instructions = new Assembler(16);
            instructions.fsin();
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(1, mbbsEmuCpuRegisters.Fpu.GetStackTop());
            Assert.Equal(Float80.Indefinite, mbbsEmuCpuCore.FpuStack[1]);
            Assert.Equal(0x01, mbbsEmuCpuRegisters.Fpu.StatusWord & 0x3F);
            Assert.False(mbbsEmuCpuRegisters.Fpu.StatusWord.IsFlagSet((ushort)EnumFpuStatusFlags.Code1));
            Assert.False(mbbsEmuCpuRegisters.Fpu.StatusWord.IsFlagSet((ushort)EnumFpuStatusFlags.Code2));
        }

        /// <summary>
        ///     A NaN is made quiet (IE only if it was signalling) and an unsupported encoding gives the real indefinite
        ///     (IE); C1 and C2 are cleared.
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
        public void FSIN_NaN(ushort ST0SignExponent, ulong ST0Significand, ushort expectedSignExponent,
            ulong expectedSignificand, ushort expectedExceptions)
        {
            Reset();

            mbbsEmuCpuRegisters.Fpu.SetStackTop(1);
            mbbsEmuCpuCore.FpuStack[1] = new Float80(ST0SignExponent, ST0Significand);
            mbbsEmuCpuRegisters.Fpu.SetFlag(EnumFpuStatusFlags.Code1);
            mbbsEmuCpuRegisters.Fpu.SetFlag(EnumFpuStatusFlags.Code2);

            var instructions = new Assembler(16);
            instructions.fsin();
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(1, mbbsEmuCpuRegisters.Fpu.GetStackTop());
            Assert.Equal(new Float80(expectedSignExponent, expectedSignificand), mbbsEmuCpuCore.FpuStack[1]);
            Assert.Equal(expectedExceptions, mbbsEmuCpuRegisters.Fpu.StatusWord & 0x3F);
            Assert.False(mbbsEmuCpuRegisters.Fpu.StatusWord.IsFlagSet((ushort)EnumFpuStatusFlags.Code1));
            Assert.False(mbbsEmuCpuRegisters.Fpu.StatusWord.IsFlagSet((ushort)EnumFpuStatusFlags.Code2));
        }
    }
}
