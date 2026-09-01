using Iced.Intel;
using Xunit;
using static Iced.Intel.AssemblerRegisters;

namespace MBBSEmu.Tests.CPU
{
    public class SHLD_Tests : CpuTestBase
    {
        //Counts 1, 16 and 32 are the three values where filling from the bottom of
        //the source coincides with filling from the top, so rows using counts such
        //as 4 and 8 with an asymmetric source are what actually pin the fill down
        [Theory]
        [InlineData(0xFFFFFFFF, 0x11111111, 16, 0xFFFF1111, true, false, true, false)]
        [InlineData(0xFFFFFFFF, 0x11111111, 32, 0xFFFFFFFF, false, false, false, false)]
        [InlineData(0x00001111, 0x11110000, 16, 0x11111111, false, false, false, false)]
        [InlineData(0xFFFF0000, 0xFFFF0000, 16, 0x0000FFFF, true, false, false, false)]
        [InlineData(0x7FFFFFFF, 0xFFFFFFFF, 1, 0xFFFFFFFF, false, true, true, false)]
        [InlineData(0x80000000, 0x00000000, 1, 0x00000000, true, true, false, true)]
        [InlineData(0x40000000, 0x00000000, 1, 0x80000000, false, true, true, false)] // Sign change with a source operand that is not itself negative
        [InlineData(0x12345678, 0x9ABCDEF0, 8, 0x3456789A, false, false, false, false)] // Top 8 bits of the source fill, not the bottom 8
        [InlineData(0x11111111, 0xF0000000, 4, 0x1111111F, true, false, false, false)]
        [InlineData(0x80000000, 0x00FF0000, 8, 0x00000000, false, false, false, true)] // Source bits that are not in the top 8 must not appear
        public void SHLD_EAX_EBX_IMM8(uint eaxValue, uint ebxValue, byte count, uint expectedValue, bool carryFlag, bool overflowFlag, bool signFlag, bool zeroFlag)
        {
            Reset();
            mbbsEmuCpuRegisters.EAX = eaxValue;
            mbbsEmuCpuRegisters.EBX = ebxValue;

            var instructions = new Assembler(16);
            instructions.shld(eax, ebx, count);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            //Verify Results
            Assert.Equal(expectedValue, mbbsEmuCpuRegisters.EAX);
            Assert.Equal(carryFlag, mbbsEmuCpuRegisters.CarryFlag);
            Assert.Equal(overflowFlag, mbbsEmuCpuRegisters.OverflowFlag);
            Assert.Equal(signFlag, mbbsEmuCpuRegisters.SignFlag);
            Assert.Equal(zeroFlag, mbbsEmuCpuRegisters.ZeroFlag);
        }

        [Theory]
        [InlineData(0xFFFFFFFF, 0x11111111, 16, 0xFFFF1111, true, false, true, false)]
        [InlineData(0xFFFFFFFF, 0x11111111, 32, 0xFFFFFFFF, false, false, false, false)]
        [InlineData(0x00001111, 0x11110000, 16, 0x11111111, false, false, false, false)]
        [InlineData(0xFFFF0000, 0xFFFF0000, 16, 0x0000FFFF, true, false, false, false)]
        [InlineData(0x7FFFFFFF, 0xFFFFFFFF, 1, 0xFFFFFFFF, false, true, true, false)]
        [InlineData(0x80000000, 0x00000000, 1, 0x00000000, true, true, false, true)]
        [InlineData(0x40000000, 0x00000000, 1, 0x80000000, false, true, true, false)] // Sign change with a source operand that is not itself negative
        [InlineData(0x12345678, 0x9ABCDEF0, 8, 0x3456789A, false, false, false, false)] // Top 8 bits of the source fill, not the bottom 8
        [InlineData(0x11111111, 0xF0000000, 4, 0x1111111F, true, false, false, false)]
        [InlineData(0x80000000, 0x00FF0000, 8, 0x00000000, false, false, false, true)] // Source bits that are not in the top 8 must not appear
        public void SHLD_EAX_EBX_CL(uint eaxValue, uint ebxValue, byte count, uint expectedValue, bool carryFlag, bool overflowFlag, bool signFlag, bool zeroFlag)
        {
            Reset();
            mbbsEmuCpuRegisters.EAX = eaxValue;
            mbbsEmuCpuRegisters.EBX = ebxValue;
            mbbsEmuCpuRegisters.CL = count;

            var instructions = new Assembler(16);
            instructions.shld(eax, ebx, cl);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            //Verify Results
            Assert.Equal(expectedValue, mbbsEmuCpuRegisters.EAX);
            Assert.Equal(carryFlag, mbbsEmuCpuRegisters.CarryFlag);
            Assert.Equal(overflowFlag, mbbsEmuCpuRegisters.OverflowFlag);
            Assert.Equal(signFlag, mbbsEmuCpuRegisters.SignFlag);
            Assert.Equal(zeroFlag, mbbsEmuCpuRegisters.ZeroFlag);
        }

        [Theory]
        [InlineData(0x1234, 0xABCD, 4, 0x234A, true, false, false, false)]
        [InlineData(0x8000, 0x00FF, 8, 0x0000, false, false, false, true)]
        [InlineData(0x4000, 0x0000, 1, 0x8000, false, true, true, false)]
        [InlineData(0xFFFF, 0x1111, 16, 0x1111, true, false, false, false)]
        public void SHLD_AX_BX_IMM8(ushort axValue, ushort bxValue, byte count, ushort expectedValue, bool carryFlag, bool overflowFlag, bool signFlag, bool zeroFlag)
        {
            Reset();
            mbbsEmuCpuRegisters.AX = axValue;
            mbbsEmuCpuRegisters.BX = bxValue;

            var instructions = new Assembler(16);
            instructions.shld(ax, bx, count);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            //Verify Results
            Assert.Equal(expectedValue, mbbsEmuCpuRegisters.AX);
            Assert.Equal(carryFlag, mbbsEmuCpuRegisters.CarryFlag);
            Assert.Equal(overflowFlag, mbbsEmuCpuRegisters.OverflowFlag);
            Assert.Equal(signFlag, mbbsEmuCpuRegisters.SignFlag);
            Assert.Equal(zeroFlag, mbbsEmuCpuRegisters.ZeroFlag);
        }

        /// <summary>
        ///     A count that masks to zero must leave the destination and every flag
        ///     alone, so the flags are set going in to tell "untouched" apart from
        ///     "recomputed and happened to clear"
        /// </summary>
        [Theory]
        [InlineData(0)]
        [InlineData(32)]
        public void SHLD_MaskedCountZero_ChangesNothing(byte count)
        {
            Reset();
            mbbsEmuCpuRegisters.EAX = 0x12345678;
            mbbsEmuCpuRegisters.EBX = 0xFFFFFFFF;
            mbbsEmuCpuRegisters.CarryFlag = true;
            mbbsEmuCpuRegisters.OverflowFlag = true;
            mbbsEmuCpuRegisters.SignFlag = true;
            mbbsEmuCpuRegisters.ZeroFlag = true;

            var instructions = new Assembler(16);
            instructions.shld(eax, ebx, count);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(0x12345678u, mbbsEmuCpuRegisters.EAX);
            Assert.True(mbbsEmuCpuRegisters.CarryFlag);
            Assert.True(mbbsEmuCpuRegisters.OverflowFlag);
            Assert.True(mbbsEmuCpuRegisters.SignFlag);
            Assert.True(mbbsEmuCpuRegisters.ZeroFlag);
        }
    }
}
