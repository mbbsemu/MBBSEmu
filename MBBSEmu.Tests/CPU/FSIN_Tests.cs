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
        ///     |ST(0)| >= 2^63 is out of range: C2 set, ST(0) and the stack unchanged, no exception (an x87 coprocessor)
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

            var instructions = new Assembler(16);
            instructions.fsin();
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(new Float80(ST0SignExponent, ST0Significand), mbbsEmuCpuCore.FpuStack[1]);
            Assert.Equal(1, mbbsEmuCpuRegisters.Fpu.GetStackTop());
            Assert.True(mbbsEmuCpuRegisters.Fpu.StatusWord.IsFlagSet((ushort)EnumFpuStatusFlags.Code2));
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
        ///     An infinity is an invalid operand: ST(0) becomes the real indefinite, IE, C2 cleared (an x87
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
            mbbsEmuCpuRegisters.Fpu.SetFlag(EnumFpuStatusFlags.Code2);

            var instructions = new Assembler(16);
            instructions.fsin();
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(1, mbbsEmuCpuRegisters.Fpu.GetStackTop());
            Assert.Equal(Float80.Indefinite, mbbsEmuCpuCore.FpuStack[1]);
            Assert.Equal(0x01, mbbsEmuCpuRegisters.Fpu.StatusWord & 0x3F);
            Assert.False(mbbsEmuCpuRegisters.Fpu.StatusWord.IsFlagSet((ushort)EnumFpuStatusFlags.Code2));
        }
    }
}
