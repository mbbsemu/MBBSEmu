using Iced.Intel;
using MBBSEmu.CPU;
using MBBSEmu.Extensions;
using System;
using Xunit;

namespace MBBSEmu.Tests.CPU
{
    public class FPTAN_Tests : CpuTestBase
    {
        [Theory]
        [InlineData(1d)]
        [InlineData(-1d)]
        [InlineData(0d)]
        public void FPTAN_Test(double ST0Value)
        {
            Reset();

            mbbsEmuCpuRegisters.Fpu.SetStackTop(0);
            mbbsEmuCpuCore.FpuStack[0] = ST0Value;

            var instructions = new Assembler(16);
            instructions.fptan();
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            //ST(0) becomes 1.0, ST(1) becomes the tangent
            Assert.Equal(1d, mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackTop()]);
            Assert.Equal(Math.Tan(ST0Value), mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackPointer(Register.ST1)]);
        }

        /// <summary>
        ///     |ST(0)| >= 2^63 is out of range: C2 set, ST(0) and the stack unchanged, no exception (an x87 coprocessor)
        /// </summary>
        [Theory]
        [InlineData((ushort)0x403E, 0x8000000000000000UL)] //2^63
        [InlineData((ushort)0xC03E, 0x8000000000000000UL)] //-2^63
        [InlineData((ushort)0x43FF, 0xFFFFFFFFFFFFF800UL)] //beyond double's range
        public void FPTAN_OutOfRange_SetsC2(ushort ST0SignExponent, ulong ST0Significand)
        {
            Reset();

            mbbsEmuCpuRegisters.Fpu.SetStackTop(1);
            mbbsEmuCpuCore.FpuStack[1] = new Float80(ST0SignExponent, ST0Significand);

            var instructions = new Assembler(16);
            instructions.fptan();
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(new Float80(ST0SignExponent, ST0Significand), mbbsEmuCpuCore.FpuStack[1]);
            Assert.Equal(1, mbbsEmuCpuRegisters.Fpu.GetStackTop());
            Assert.True(mbbsEmuCpuRegisters.Fpu.StatusWord.IsFlagSet((ushort)EnumFpuStatusFlags.Code2));
            Assert.Equal(0, mbbsEmuCpuRegisters.Fpu.StatusWord & 0x3F);
        }

        /// <summary>
        ///     The largest value below 2^63 is in range: C2 cleared, the value pushed (an x87 coprocessor)
        /// </summary>
        [Fact]
        public void FPTAN_LargestInRange_ClearsC2()
        {
            Reset();

            mbbsEmuCpuRegisters.Fpu.SetStackTop(1);
            mbbsEmuCpuCore.FpuStack[1] = new Float80(0x403D, 0xFFFFFFFFFFFFFFFF);
            mbbsEmuCpuRegisters.Fpu.SetFlag(EnumFpuStatusFlags.Code2);

            var instructions = new Assembler(16);
            instructions.fptan();
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(2, mbbsEmuCpuRegisters.Fpu.GetStackTop());
            Assert.False(mbbsEmuCpuRegisters.Fpu.StatusWord.IsFlagSet((ushort)EnumFpuStatusFlags.Code2));
        }
    }
}
