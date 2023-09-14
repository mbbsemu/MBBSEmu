using Iced.Intel;
using System;
using Xunit;
using static Iced.Intel.AssemblerRegisters;

namespace MBBSEmu.Tests.CPU
{
    public  class RCR_Tests : CpuTestBase
    {
        [Theory]
        [InlineData(0xF, 1, 0x7, true)]
        [InlineData(0xE, 1, 0x7, false)]
        [InlineData(0x1FF, 1, 0xFF, true)]
        [InlineData(0x1FE, 1, 0xFF, false)]
        [InlineData(0x3C, 2, 0xF, false)]
        [InlineData(0x3E, 2, 0xF, true)]
        [InlineData(0xFFFF, 2, 0xBFFF, true)]
        public void RCR_AX_IMM16_CF_CLEAR(ushort axValue, byte bitsToRotate, ushort expectedValue,
            bool expectedCFValue)
        {
            Reset();
            mbbsEmuCpuRegisters.AX = axValue;

            var instructions = new Assembler(16);
            instructions.rcr(ax, bitsToRotate);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(expectedValue, mbbsEmuCpuRegisters.AX);
            Assert.Equal(expectedCFValue, mbbsEmuCpuRegisters.CarryFlag);
        }

        [Theory]
        [InlineData(0xF, 1, 0x8007, true)]
        [InlineData(0xE, 1, 0x8007, false)]
        [InlineData(0x1FF, 1, 0x80FF, true)]
        [InlineData(0x1FE, 1, 0x80FF, false)]
        [InlineData(0x3C, 2, 0x400F, false)]
        [InlineData(0x3E, 2, 0x400F, true)]
        [InlineData(0xFFFF, 2, 0xFFFF, true)]
        public void RCR_AX_IMM16_CF_SET(ushort axValue, byte bitsToRotate, ushort expectedValue, bool expectedCFValue)
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
        }

        [Theory]
        [InlineData(0xF, 1, 0x7, true)]
        [InlineData(0xE, 1, 0x7, false)]
        public void RCR_AH_IMM8_CF_CLEAR(byte ahValue, byte bitsToRotate, byte expectedValue, bool expectedCFValue)
        {
            Reset();
            mbbsEmuCpuRegisters.AH = ahValue;

            var instructions = new Assembler(16);
            instructions.rcr(ah, bitsToRotate);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(expectedValue, mbbsEmuCpuRegisters.AH);
            Assert.Equal(expectedCFValue, mbbsEmuCpuRegisters.CarryFlag);
        }

        [Theory]
        [InlineData(0xF, 1, 0x87, true)]
        [InlineData(0xE, 1, 0x87, false)]
        public void RCR_AH_IMM8_CF_SET(byte ahValue, byte bitsToRotate, ushort expectedValue, bool expectedCFValue)
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
        }

        [Theory]
        [InlineData(0xF, 1, 0x7, true)]
        [InlineData(0xE, 1, 0x7, false)]
        [InlineData(0x1FF, 1, 0xFF, true)]
        [InlineData(0x1FE, 1, 0xFF, false)]
        [InlineData(0x3C, 2, 0xF, false)]
        [InlineData(0x3E, 2, 0xF, true)]
        [InlineData(0xFFFF, 2, 0xBFFF, true)]
        public void RCR_M16_IMM16_1(ushort memoryValue, byte bitsToRotate, ushort expectedValue,
            bool expectedCFValue)
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
        }
    }
}
