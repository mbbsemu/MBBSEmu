using Iced.Intel;
using Xunit;
using static Iced.Intel.AssemblerRegisters;

namespace MBBSEmu.Tests.CPU
{
    public class SAR_Tests : CpuTestBase
    {
        [Theory]
        [InlineData(0x8000, 0xC000, false)] // sign fills bit 14
        [InlineData(0x0004, 0x0002, false)]
        [InlineData(0x0005, 0x0002, true)]
        [InlineData(0xFFFF, 0xFFFF, true)]
        public void SAR_AX_1(ushort axValue, ushort axExpectedValue, bool carry)
        {
            Reset();

            mbbsEmuCpuRegisters.AX = axValue;

            var instructions = new Assembler(16);
            instructions.sar(ax, 1);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(axExpectedValue, mbbsEmuCpuRegisters.AX);
            Assert.Equal(carry, mbbsEmuCpuRegisters.CarryFlag);
            //SAR by 1 always clears OF
            Assert.False(mbbsEmuCpuRegisters.OverflowFlag);
            Assert.Equal(axExpectedValue >= 0x8000, mbbsEmuCpuRegisters.SignFlag);
        }

        [Theory]
        [InlineData(0x91A0, 2, 0xE468, false)] // every vacated bit sign-filled, not just bit 15
        [InlineData(0xFFFF, 4, 0xFFFF, true)]
        [InlineData(0x8000, 15, 0xFFFF, false)]
        [InlineData(0x8000, 16, 0xFFFF, true)] // fully shifted out: all sign bits, CF = old bit 15
        [InlineData(0x7FFF, 4, 0x07FF, true)] // positive operands keep logical behavior
        [InlineData(0x0100, 8, 0x0001, false)]
        public void SAR_AX_MultiBit(ushort axValue, byte count, ushort axExpectedValue, bool carry)
        {
            Reset();

            mbbsEmuCpuRegisters.AX = axValue;

            var instructions = new Assembler(16);
            instructions.sar(ax, count);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(axExpectedValue, mbbsEmuCpuRegisters.AX);
            Assert.Equal(carry, mbbsEmuCpuRegisters.CarryFlag);
            Assert.Equal(axExpectedValue >= 0x8000, mbbsEmuCpuRegisters.SignFlag);
            Assert.Equal(axExpectedValue == 0, mbbsEmuCpuRegisters.ZeroFlag);
        }

        [Theory]
        [InlineData(0xA0, 2, 0xE8, false)]
        [InlineData(0x81, 1, 0xC0, true)]
        [InlineData(0xFF, 8, 0xFF, true)] // fully shifted out: all sign bits, CF = old bit 7
        [InlineData(0x7F, 3, 0x0F, true)]
        [InlineData(0x01, 1, 0x00, true)]
        public void SAR_AL_MultiBit(byte alValue, byte count, byte alExpectedValue, bool carry)
        {
            Reset();

            mbbsEmuCpuRegisters.AL = alValue;

            var instructions = new Assembler(16);
            instructions.sar(al, count);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(alExpectedValue, mbbsEmuCpuRegisters.AL);
            Assert.Equal(carry, mbbsEmuCpuRegisters.CarryFlag);
            Assert.Equal(alExpectedValue >= 0x80, mbbsEmuCpuRegisters.SignFlag);
            Assert.Equal(alExpectedValue == 0, mbbsEmuCpuRegisters.ZeroFlag);
        }

        [Fact]
        public void SAR_AX_Big_Shift()
        {
            Reset();

            mbbsEmuCpuRegisters.AX = 0xF000;

            var instructions = new Assembler(16);
            instructions.sar(ax, 35); // will clamp to 3
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(0xFE00, mbbsEmuCpuRegisters.AX);
            Assert.False(mbbsEmuCpuRegisters.CarryFlag);
            Assert.True(mbbsEmuCpuRegisters.SignFlag);
            Assert.False(mbbsEmuCpuRegisters.ZeroFlag);
        }
    }
}
