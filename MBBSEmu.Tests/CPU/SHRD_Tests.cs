using Iced.Intel;
using Xunit;
using static Iced.Intel.AssemblerRegisters;

namespace MBBSEmu.Tests.CPU
{
    public class SHRD_Tests : CpuTestBase
    {
        [Theory]
        [InlineData(0x12345678, 0x9ABCDEF0, 8, 0xF0123456, false, false, true, false)] // Bottom 8 bits of the source fill from the top
        [InlineData(0x11111111, 0x0000000F, 4, 0xF1111111, false, false, true, false)]
        [InlineData(0x00000001, 0x00000000, 1, 0x00000000, true, false, false, true)]
        [InlineData(0x00000000, 0x00000001, 1, 0x80000000, false, true, true, false)] // Source supplies the new sign bit, so OF is set
        [InlineData(0xFFFFFFFF, 0x00000000, 16, 0x0000FFFF, true, false, false, false)]
        [InlineData(0x12345678, 0xFFFFFFFF, 32, 0x12345678, false, false, false, false)] // Masked to a count of zero
        public void SHRD_EAX_EBX_IMM8(uint eaxValue, uint ebxValue, byte count, uint expectedValue, bool carryFlag, bool overflowFlag, bool signFlag, bool zeroFlag)
        {
            Reset();
            mbbsEmuCpuRegisters.EAX = eaxValue;
            mbbsEmuCpuRegisters.EBX = ebxValue;

            var instructions = new Assembler(16);
            instructions.shrd(eax, ebx, count);
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
        [InlineData(0x12345678, 0x9ABCDEF0, 8, 0xF0123456, false, false, true, false)]
        [InlineData(0x11111111, 0x0000000F, 4, 0xF1111111, false, false, true, false)]
        [InlineData(0x00000001, 0x00000000, 1, 0x00000000, true, false, false, true)]
        [InlineData(0x00000000, 0x00000001, 1, 0x80000000, false, true, true, false)]
        [InlineData(0xFFFFFFFF, 0x00000000, 16, 0x0000FFFF, true, false, false, false)]
        [InlineData(0x12345678, 0xFFFFFFFF, 32, 0x12345678, false, false, false, false)]
        public void SHRD_EAX_EBX_CL(uint eaxValue, uint ebxValue, byte count, uint expectedValue, bool carryFlag, bool overflowFlag, bool signFlag, bool zeroFlag)
        {
            Reset();
            mbbsEmuCpuRegisters.EAX = eaxValue;
            mbbsEmuCpuRegisters.EBX = ebxValue;
            mbbsEmuCpuRegisters.CL = count;

            var instructions = new Assembler(16);
            instructions.shrd(eax, ebx, cl);
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
        [InlineData(0x1234, 0xABCD, 4, 0xD123, false, false, true, false)]
        [InlineData(0x00FF, 0x0000, 8, 0x0000, true, false, false, true)]
        [InlineData(0x0000, 0x0001, 1, 0x8000, false, true, true, false)] // Source supplies the new sign bit, so OF is set
        [InlineData(0xFFFF, 0x0000, 8, 0x00FF, true, false, false, false)]
        public void SHRD_AX_BX_IMM8(ushort axValue, ushort bxValue, byte count, ushort expectedValue, bool carryFlag, bool overflowFlag, bool signFlag, bool zeroFlag)
        {
            Reset();
            mbbsEmuCpuRegisters.AX = axValue;
            mbbsEmuCpuRegisters.BX = bxValue;

            var instructions = new Assembler(16);
            instructions.shrd(ax, bx, count);
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
        public void SHRD_MaskedCountZero_ChangesNothing(byte count)
        {
            Reset();
            mbbsEmuCpuRegisters.EAX = 0x12345678;
            mbbsEmuCpuRegisters.EBX = 0xFFFFFFFF;
            mbbsEmuCpuRegisters.CarryFlag = true;
            mbbsEmuCpuRegisters.OverflowFlag = true;
            mbbsEmuCpuRegisters.SignFlag = true;
            mbbsEmuCpuRegisters.ZeroFlag = true;

            var instructions = new Assembler(16);
            instructions.shrd(eax, ebx, count);
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
