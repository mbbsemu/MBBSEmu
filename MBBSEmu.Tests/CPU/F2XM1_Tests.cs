using Iced.Intel;
using MBBSEmu.CPU;
using MBBSEmu.Extensions;
using System;
using Xunit;

namespace MBBSEmu.Tests.CPU
{
    public class F2XM1_Tests : CpuTestBase
    {
        [Theory]
        [InlineData(1d)]
        [InlineData(-1d)]
        [InlineData(0.5d)]
        [InlineData(0d)]
        public void F2XM1_Test(double ST0Value)
        {
            Reset();

            mbbsEmuCpuRegisters.Fpu.SetStackTop(0);
            mbbsEmuCpuCore.FpuStack[0] = ST0Value;

            var instructions = new Assembler(16);
            instructions.f2xm1();
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(Math.Pow(2, ST0Value) - 1, mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackTop()]);
        }

        /// <summary>
        ///     A NaN is made quiet (IE only if it was signalling) and an unsupported encoding gives the real indefinite
        ///     (IE); C1 is cleared, C2 left as it was.
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
        public void F2XM1_NaN(ushort ST0SignExponent, ulong ST0Significand, ushort expectedSignExponent,
            ulong expectedSignificand, ushort expectedExceptions)
        {
            Reset();

            mbbsEmuCpuRegisters.Fpu.SetStackTop(1);
            mbbsEmuCpuCore.FpuStack[1] = new Float80(ST0SignExponent, ST0Significand);
            mbbsEmuCpuRegisters.Fpu.SetFlag(EnumFpuStatusFlags.Code1);
            mbbsEmuCpuRegisters.Fpu.SetFlag(EnumFpuStatusFlags.Code2);

            var instructions = new Assembler(16);
            instructions.f2xm1();
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(1, mbbsEmuCpuRegisters.Fpu.GetStackTop());
            Assert.Equal(new Float80(expectedSignExponent, expectedSignificand), mbbsEmuCpuCore.FpuStack[1]);
            Assert.Equal(expectedExceptions, mbbsEmuCpuRegisters.Fpu.StatusWord & 0x3F);
            Assert.False(mbbsEmuCpuRegisters.Fpu.StatusWord.IsFlagSet((ushort)EnumFpuStatusFlags.Code1));
            Assert.True(mbbsEmuCpuRegisters.Fpu.StatusWord.IsFlagSet((ushort)EnumFpuStatusFlags.Code2));
        }
    }
}
