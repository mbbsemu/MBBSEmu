using Iced.Intel;
using System;
using Xunit;
using static Iced.Intel.AssemblerRegisters;

namespace MBBSEmu.Tests.CPU
{
    public class ROR_Tests : CpuTestBase
    {
        [Fact]
        public void ROR_AX_IMM16_1()
        {
            Reset();
            mbbsEmuCpuRegisters.AX = 2;

            var instructions = new Assembler(16);
            instructions.ror(ax, 1);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(2 >> 1, mbbsEmuCpuRegisters.AX);
        }

        [Fact]
        public void ROR_AX_IMM16_CF_OF()
        {
            Reset();
            mbbsEmuCpuRegisters.AX = 1;

            var instructions = new Assembler(16);
            instructions.ror(ax, 1);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(0x8000, mbbsEmuCpuRegisters.AX);
            Assert.True(mbbsEmuCpuRegisters.CarryFlag);
            Assert.True(mbbsEmuCpuRegisters.OverflowFlag);
        }

        [Fact]
        public void ROR_AX_IMM16_OF()
        {
            Reset();
            mbbsEmuCpuRegisters.AX = 0x8000;

            var instructions = new Assembler(16);
            instructions.ror(ax, 1);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(0x8000 >> 1, mbbsEmuCpuRegisters.AX);
            Assert.False(mbbsEmuCpuRegisters.CarryFlag);
            Assert.True(mbbsEmuCpuRegisters.OverflowFlag);
        }

        [Fact]
        public void ROR_AL_IMM8_1()
        {
            Reset();
            mbbsEmuCpuRegisters.AL = 2;

            var instructions = new Assembler(16);
            instructions.ror(al, 1);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(2 >> 1, mbbsEmuCpuRegisters.AL);
        }

        [Fact]
        public void ROR_AL_IMM8_CF_OF()
        {
            Reset();
            mbbsEmuCpuRegisters.AL = 1;

            var instructions = new Assembler(16);
            instructions.ror(al, 1);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(0x80, mbbsEmuCpuRegisters.AX);
            Assert.True(mbbsEmuCpuRegisters.CarryFlag);
            Assert.True(mbbsEmuCpuRegisters.OverflowFlag);
        }

        [Fact]
        public void ROR_AL_IMM8_OF()
        {
            Reset();
            mbbsEmuCpuRegisters.AL = 0x80;

            var instructions = new Assembler(16);
            instructions.ror(al, 1);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(0x80 >> 1, mbbsEmuCpuRegisters.AX);
            Assert.False(mbbsEmuCpuRegisters.CarryFlag);
            Assert.True(mbbsEmuCpuRegisters.OverflowFlag);
        }

        [Fact]
        public void ROR_M8_IMM8_1()
        {
            Reset();
            mbbsEmuCpuRegisters.DS = 2;
            CreateDataSegment(new byte[] { 2 }, 2);

            var instructions = new Assembler(16);
            instructions.ror(__byte_ptr[0], 1);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(2 >> 1, mbbsEmuMemoryCore.GetByte(2, 0));
        }

        [Fact]
        public void ROR_M16_IMM8_1()
        {
            Reset();
            mbbsEmuCpuRegisters.DS = 2;
            CreateDataSegment(BitConverter.GetBytes((ushort)2), 2);

            var instructions = new Assembler(16);
            instructions.ror(__word_ptr[0], 1);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(2 >> 1, mbbsEmuMemoryCore.GetByte(2, 0));
        }

        [Fact]
        public void ROR_M8_CL_1()
        {
            Reset();
            mbbsEmuCpuRegisters.DS = 2;
            mbbsEmuCpuRegisters.CL = 1;
            CreateDataSegment(new byte[] { 2 }, 2);

            var instructions = new Assembler(16);
            instructions.ror(__byte_ptr[0], cl);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(2 >> 1, mbbsEmuMemoryCore.GetByte(2, 0));
        }

        [Fact]
        public void ROR_M8_IMM8_7()
        {
            Reset();
            mbbsEmuCpuRegisters.DS = 2;
            CreateDataSegment(new byte[] { 0x80 }, 2);

            var instructions = new Assembler(16);
            instructions.ror(__byte_ptr[0], 7);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(0x80 >> 7, mbbsEmuMemoryCore.GetByte(2, 0));
        }

        [Theory]
        [InlineData(0x81, 4, 0x18, false)]
        [InlineData(0x03, 2, 0xC0, true)]
        public void ROR_AL_MultiBit(byte alValue, byte count, byte alExpectedValue, bool carry)
        {
            Reset();
            mbbsEmuCpuRegisters.AL = alValue;

            var instructions = new Assembler(16);
            instructions.ror(al, count);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(alExpectedValue, mbbsEmuCpuRegisters.AL);
            Assert.Equal(carry, mbbsEmuCpuRegisters.CarryFlag);
        }

        [Theory]
        [InlineData(0x81, 9, 0xC0, true)] // count masked to 5 bits, then mod 8: 9 ≡ 1
        [InlineData(0x81, 8, 0x81, true)] // full width: identity, CF = MSB of result
        [InlineData(0x80, 33, 0x40, false)] // 33 & 0x1F = 1
        public void ROR_AL_CL(byte alValue, byte clValue, byte alExpectedValue, bool carry)
        {
            Reset();
            mbbsEmuCpuRegisters.AL = alValue;
            mbbsEmuCpuRegisters.CL = clValue;

            var instructions = new Assembler(16);
            instructions.ror(al, cl);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(alExpectedValue, mbbsEmuCpuRegisters.AL);
            Assert.Equal(carry, mbbsEmuCpuRegisters.CarryFlag);
        }

        [Theory]
        [InlineData(0x0002, 17, 0x0001, false)] // 17 ≡ 1 mod 16
        [InlineData(0x0001, 17, 0x8000, true)]
        [InlineData(0xAAAA, 16, 0xAAAA, true)] // full rotation is identity; CF = MSB of result
        public void ROR_AX_CL(ushort axValue, byte clValue, ushort axExpectedValue, bool carry)
        {
            Reset();
            mbbsEmuCpuRegisters.AX = axValue;
            mbbsEmuCpuRegisters.CL = clValue;

            var instructions = new Assembler(16);
            instructions.ror(ax, cl);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(axExpectedValue, mbbsEmuCpuRegisters.AX);
            Assert.Equal(carry, mbbsEmuCpuRegisters.CarryFlag);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(32)] // masks to 0
        public void ROR_AX_CL_MaskedZeroCount_LeavesValueAndFlags(byte clValue)
        {
            Reset();
            mbbsEmuCpuRegisters.AX = 0x8001;
            mbbsEmuCpuRegisters.CL = clValue;
            mbbsEmuCpuRegisters.CarryFlag = true;
            mbbsEmuCpuRegisters.OverflowFlag = true;

            var instructions = new Assembler(16);
            instructions.ror(ax, cl);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(0x8001, mbbsEmuCpuRegisters.AX);
            Assert.True(mbbsEmuCpuRegisters.CarryFlag);
            Assert.True(mbbsEmuCpuRegisters.OverflowFlag);
        }
    }
}
