using Iced.Intel;
using MBBSEmu.CPU;
using MBBSEmu.Extensions;
using System;
using Xunit;

namespace MBBSEmu.Tests.CPU
{
    public class FCOS_Tests : CpuTestBase
    {
        [Theory]
        [InlineData(1d, 0.5403023058681398, false)]
        [InlineData(-1d, 0.5403023058681398, false)]
        [InlineData(double.NaN, double.NaN, false)]
        [InlineData(double.PositiveInfinity, double.PositiveInfinity, true)]
        [InlineData(0, 1, false)]
        [InlineData(double.NegativeInfinity, double.NegativeInfinity, true)]
        public void FCOS_Tets(double ST0Value, double expectedValue, bool throwsException)
        {
            Reset();

            mbbsEmuCpuRegisters.Fpu.ControlWord = 0;
            mbbsEmuCpuRegisters.Fpu.SetStackTop(0);
            mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackTop()] = ST0Value; //ST0

            var instructions = new Assembler(16);
            instructions.fcos();
            CreateCodeSegment(instructions);

            if (throwsException)
            {
                Assert.Throws<ArgumentOutOfRangeException>(mbbsEmuCpuCore.Tick);
            }
            else
            {
                mbbsEmuCpuCore.Tick();
                Assert.Equal(expectedValue, mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackTop()]);
                Assert.Equal(0, mbbsEmuCpuRegisters.Fpu.ControlWord);
            }

        }

        /// <summary>
        ///     |ST(0)| >= 2^63 is out of range: C2 set, ST(0) and the stack unchanged, no exception (an x87 coprocessor)
        /// </summary>
        [Theory]
        [InlineData((ushort)0x403E, 0x8000000000000000UL)] //2^63
        [InlineData((ushort)0xC03E, 0x8000000000000000UL)] //-2^63
        [InlineData((ushort)0x43FF, 0xFFFFFFFFFFFFF800UL)] //beyond double's range
        public void FCOS_OutOfRange_SetsC2(ushort ST0SignExponent, ulong ST0Significand)
        {
            Reset();

            mbbsEmuCpuRegisters.Fpu.SetStackTop(1);
            mbbsEmuCpuCore.FpuStack[1] = new Float80(ST0SignExponent, ST0Significand);

            var instructions = new Assembler(16);
            instructions.fcos();
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
        public void FCOS_LargestInRange_ClearsC2()
        {
            Reset();

            mbbsEmuCpuRegisters.Fpu.SetStackTop(1);
            mbbsEmuCpuCore.FpuStack[1] = new Float80(0x403D, 0xFFFFFFFFFFFFFFFF);
            mbbsEmuCpuRegisters.Fpu.SetFlag(EnumFpuStatusFlags.Code2);

            var instructions = new Assembler(16);
            instructions.fcos();
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(1, mbbsEmuCpuRegisters.Fpu.GetStackTop());
            Assert.False(mbbsEmuCpuRegisters.Fpu.StatusWord.IsFlagSet((ushort)EnumFpuStatusFlags.Code2));
        }
    }
}
