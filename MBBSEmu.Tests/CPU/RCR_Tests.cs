using Iced.Intel;
using System;
using Xunit;
using static Iced.Intel.AssemblerRegisters;

namespace MBBSEmu.Tests.CPU
{
    public class RCR_Tests : CpuTestBase
    {
        [Theory]
        [InlineData(0xF, 1, 0x7, true, false)]
        [InlineData(0xE, 1, 0x7, false, false)]
        [InlineData(0x6, 1, 0x3, false, false)]
        [InlineData(0x1FF, 1, 0xFF, true, false)]
        [InlineData(0x1FE, 1, 0xFF, false, false)]
        [InlineData(0x3C, 2, 0xF, false, false)]
        [InlineData(0x3E, 2, 0xF, true, false)]
        [InlineData(0xFFFF, 1, 0x7FFF, true, true)]
        [InlineData(0xFFFF, 2, 0xBFFF, true, false)]
        public void RCR_AX_IMM16_CF_CLEAR(ushort axValue, byte bitsToRotate, ushort expectedValue,
            bool expectedCFValue, bool expectedOFValue)
        {
            Reset();
            mbbsEmuCpuRegisters.AX = axValue;

            var instructions = new Assembler(16);
            instructions.rcr(ax, bitsToRotate);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(expectedValue, mbbsEmuCpuRegisters.AX);
            Assert.Equal(expectedCFValue, mbbsEmuCpuRegisters.CarryFlag);
            Assert.Equal(expectedOFValue, mbbsEmuCpuRegisters.OverflowFlag);
        }

        [Theory]
        [InlineData(0xF, 1, 0x8007, true, true)]
        [InlineData(0xF, 2, 0xC003, true, false)]
        [InlineData(0xE, 1, 0x8007, false, true)]
        [InlineData(0x1FF, 1, 0x80FF, true, true)]
        [InlineData(0x1FE, 1, 0x80FF, false, true)]
        [InlineData(0x3C, 2, 0x400F, false, false)]
        [InlineData(0x3E, 2, 0x400F, true, false)]
        [InlineData(0xFFFF, 2, 0xFFFF, true, false)]
        [InlineData(0xFFFF, 1, 0xFFFF, true, false)]
        public void RCR_AX_IMM16_CF_SET(ushort axValue, byte bitsToRotate, ushort expectedValue, bool expectedCFValue, bool expectedOFValue)
        {
            Reset();
            mbbsEmuCpuRegisters.AX = axValue;
            mbbsEmuCpuRegisters.CarryFlag = true;

            var instructions = new Assembler(16);
            instructions.rcr(ax, bitsToRotate);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(expectedValue, mbbsEmuCpuRegisters.AX);
            Assert.Equal(expectedCFValue, mbbsEmuCpuRegisters.CarryFlag);
            Assert.Equal(expectedOFValue, mbbsEmuCpuRegisters.OverflowFlag);
        }

        [Theory]
        [InlineData(0xF, 1, 0x7, true, false)]
        [InlineData(0xFF, 1, 0x7F, true, true)]
        [InlineData(0xFF, 2, 0xBF, true, false)]
        [InlineData(0x0, 1, 0x0, false, false)]
        [InlineData(0xE, 1, 0x7, false, false)]
        public void RCR_AH_IMM8_CF_CLEAR(byte ahValue, byte bitsToRotate, byte expectedValue, bool expectedCFValue, bool expectedOFValue)
        {
            Reset();
            mbbsEmuCpuRegisters.AH = ahValue;

            var instructions = new Assembler(16);
            instructions.rcr(ah, bitsToRotate);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(expectedValue, mbbsEmuCpuRegisters.AH);
            Assert.Equal(expectedCFValue, mbbsEmuCpuRegisters.CarryFlag);
            Assert.Equal(expectedOFValue, mbbsEmuCpuRegisters.OverflowFlag);
        }

        [Theory]
        [InlineData(0xF, 1, 0x87, true, true)]
        [InlineData(0x7F, 1, 0xBF, true, true)]
        [InlineData(0xFF, 2, 0xFF, true, false)]
        [InlineData(0x0, 1, 0x80, false, true)]
        [InlineData(0xE, 1, 0x87, false, true)]
        public void RCR_AH_IMM8_CF_SET(byte ahValue, byte bitsToRotate, ushort expectedValue, bool expectedCFValue, bool expectedOFValue)
        {
            Reset();
            mbbsEmuCpuRegisters.AH = ahValue;
            mbbsEmuCpuRegisters.CarryFlag = true;

            var instructions = new Assembler(16);
            instructions.rcr(ah, bitsToRotate);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(expectedValue, mbbsEmuCpuRegisters.AH);
            Assert.Equal(expectedCFValue, mbbsEmuCpuRegisters.CarryFlag);
            Assert.Equal(expectedOFValue, mbbsEmuCpuRegisters.OverflowFlag);
        }

        [Theory]
        [InlineData(0xF, 1, 0x7, true, false)]
        [InlineData(0xE, 1, 0x7, false, false)]
        [InlineData(0x6, 1, 0x3, false, false)]
        [InlineData(0x1FF, 1, 0xFF, true, false)]
        [InlineData(0x1FE, 1, 0xFF, false, false)]
        [InlineData(0x3C, 2, 0xF, false, false)]
        [InlineData(0x3E, 2, 0xF, true, false)]
        [InlineData(0xFFFF, 1, 0x7FFF, true, true)]
        [InlineData(0xFFFF, 2, 0xBFFF, true, false)]
        public void RCR_M16_IMM16_1(ushort memoryValue, byte bitsToRotate, ushort expectedValue,
            bool expectedCFValue, bool expectedOFValue)
        {
            Reset();
            mbbsEmuCpuRegisters.DS = 2;
            CreateDataSegment(BitConverter.GetBytes(memoryValue), 2);

            var instructions = new Assembler(16);
            instructions.rcr(__word_ptr[0], bitsToRotate);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(expectedValue, mbbsEmuMemoryCore.GetWord(2, 0));
            Assert.Equal(expectedCFValue, mbbsEmuCpuRegisters.CarryFlag);
            Assert.Equal(expectedOFValue, mbbsEmuCpuRegisters.OverflowFlag);
        }
    }
}
