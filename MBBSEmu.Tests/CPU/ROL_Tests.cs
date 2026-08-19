using Iced.Intel;
using Xunit;
using static Iced.Intel.AssemblerRegisters;

namespace MBBSEmu.Tests.CPU
{
    public class ROL_Tests : CpuTestBase
    {
        [Theory]
        [InlineData(0x8000, 0x0001, true, true)] // MSB wraps to LSB; OF = MSB(result) != CF
        [InlineData(0x4000, 0x8000, false, true)]
        [InlineData(0x2000, 0x4000, false, false)]
        [InlineData(0x0001, 0x0002, false, false)]
        public void ROL_AX_1(ushort axValue, ushort axExpectedValue, bool carry, bool overflow)
        {
            Reset();
            mbbsEmuCpuRegisters.AX = axValue;

            var instructions = new Assembler(16);
            instructions.rol(ax, 1);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(axExpectedValue, mbbsEmuCpuRegisters.AX);
            Assert.Equal(carry, mbbsEmuCpuRegisters.CarryFlag);
            Assert.Equal(overflow, mbbsEmuCpuRegisters.OverflowFlag);
        }

        [Theory]
        [InlineData(0x8001, 4, 0x0018, false)]
        [InlineData(0xC000, 2, 0x0003, true)]
        [InlineData(0xAAAA, 8, 0xAAAA, false)]
        [InlineData(0x0001, 16, 0x0001, true)] // full rotation is identity; CF = LSB of result
        public void ROL_AX_MultiBit(ushort axValue, byte count, ushort axExpectedValue, bool carry)
        {
            Reset();
            mbbsEmuCpuRegisters.AX = axValue;

            var instructions = new Assembler(16);
            instructions.rol(ax, count);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(axExpectedValue, mbbsEmuCpuRegisters.AX);
            Assert.Equal(carry, mbbsEmuCpuRegisters.CarryFlag);
        }

        [Theory]
        [InlineData(0x80, 1, 0x01, true, true)]
        [InlineData(0x01, 1, 0x02, false, false)]
        public void ROL_AL_1(byte alValue, byte count, byte alExpectedValue, bool carry, bool overflow)
        {
            Reset();
            mbbsEmuCpuRegisters.AL = alValue;

            var instructions = new Assembler(16);
            instructions.rol(al, count);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(alExpectedValue, mbbsEmuCpuRegisters.AL);
            Assert.Equal(carry, mbbsEmuCpuRegisters.CarryFlag);
            Assert.Equal(overflow, mbbsEmuCpuRegisters.OverflowFlag);
        }

        [Theory]
        [InlineData(0x81, 4, 0x18, false)]
        [InlineData(0xC0, 2, 0x03, true)]
        public void ROL_AL_MultiBit(byte alValue, byte count, byte alExpectedValue, bool carry)
        {
            Reset();
            mbbsEmuCpuRegisters.AL = alValue;

            var instructions = new Assembler(16);
            instructions.rol(al, count);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(alExpectedValue, mbbsEmuCpuRegisters.AL);
            Assert.Equal(carry, mbbsEmuCpuRegisters.CarryFlag);
        }

        [Theory]
        [InlineData(0x81, 9, 0x03, true)] // count masked to 5 bits, then mod 8: 9 ≡ 1
        [InlineData(0x81, 8, 0x81, true)] // full width: identity, CF = LSB of result
        [InlineData(0x80, 33, 0x01, true)] // 33 & 0x1F = 1
        public void ROL_AL_CL(byte alValue, byte clValue, byte alExpectedValue, bool carry)
        {
            Reset();
            mbbsEmuCpuRegisters.AL = alValue;
            mbbsEmuCpuRegisters.CL = clValue;

            var instructions = new Assembler(16);
            instructions.rol(al, cl);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(alExpectedValue, mbbsEmuCpuRegisters.AL);
            Assert.Equal(carry, mbbsEmuCpuRegisters.CarryFlag);
        }

        [Theory]
        [InlineData(0x0001, 17, 0x0002, false)] // 17 ≡ 1 mod 16
        [InlineData(0x8000, 17, 0x0001, true)]
        public void ROL_AX_CL(ushort axValue, byte clValue, ushort axExpectedValue, bool carry)
        {
            Reset();
            mbbsEmuCpuRegisters.AX = axValue;
            mbbsEmuCpuRegisters.CL = clValue;

            var instructions = new Assembler(16);
            instructions.rol(ax, cl);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(axExpectedValue, mbbsEmuCpuRegisters.AX);
            Assert.Equal(carry, mbbsEmuCpuRegisters.CarryFlag);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(32)] // masks to 0
        public void ROL_AX_CL_MaskedZeroCount_LeavesValueAndFlags(byte clValue)
        {
            Reset();
            mbbsEmuCpuRegisters.AX = 0x8001;
            mbbsEmuCpuRegisters.CL = clValue;
            mbbsEmuCpuRegisters.CarryFlag = true;
            mbbsEmuCpuRegisters.OverflowFlag = true;

            var instructions = new Assembler(16);
            instructions.rol(ax, cl);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(0x8001, mbbsEmuCpuRegisters.AX);
            Assert.True(mbbsEmuCpuRegisters.CarryFlag);
            Assert.True(mbbsEmuCpuRegisters.OverflowFlag);
        }
    }
}
