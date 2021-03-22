using Iced.Intel;
using MBBSEmu.CPU;
using MBBSEmu.Extensions;
using Xunit;
using static Iced.Intel.AssemblerRegisters;

namespace MBBSEmu.Tests.CPU
{
    public class SHR_Tests : CpuTestBase
    {
        [Theory]
        [InlineData(0xFFFF, 0x7FFF, true)]
        [InlineData(0x7FFF, 0x3FFF, false)]
        [InlineData(0x3FFF, 0x1FFF, false)]
        public void SHR_AX_CarryFlag_SignFlag(ushort axValue, ushort axExpectedValue, bool overflow)
        {
            Reset();

            mbbsEmuCpuRegisters.AX = axValue;

            var instructions = new Assembler(16);
            instructions.shr(ax, 1);
            CreateCodeSegment(instructions);

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Results
            Assert.Equal(axExpectedValue, mbbsEmuCpuRegisters.AX);

            //Verify Flags
            Assert.True(mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.CF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.ZF));
            Assert.Equal(overflow, mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.OF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.SF));
        }

        [Theory]
        [InlineData(0x7FFF, 0x3FFF)]
        public void SHR_AX_OverflowFlag_SignFlag(ushort axValue, ushort axExpectedValue)
        {
            Reset();

            mbbsEmuCpuRegisters.AX = axValue;

            var instructions = new Assembler(16);
            instructions.shr(ax, 1);
            CreateCodeSegment(instructions);

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Results
            Assert.Equal(axExpectedValue, mbbsEmuCpuRegisters.AX);

            //Verify Flags
            Assert.True(mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.CF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.ZF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.OF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.SF));
        }

        [Fact]
        public void SHR_AX_Big_Shift()
        {
            Reset();

            mbbsEmuCpuRegisters.AX = 0xFFFF;

            var instructions = new Assembler(16);
            instructions.shr(ax, 35); // will clamp to 3
            CreateCodeSegment(instructions);

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Results
            Assert.Equal(0x1FFF, mbbsEmuCpuRegisters.AX);

            //Verify Flags
            Assert.True(mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.CF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.ZF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.OF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.SF));
        }

        [Fact]
        public void SHR_AH_Big_Shift()
        {
            Reset();

            mbbsEmuCpuRegisters.AH = 0xFF;

            var instructions = new Assembler(16);
            instructions.shr(ah, 35); // will clamp to 3
            CreateCodeSegment(instructions);

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Results
            Assert.Equal(0x1F, mbbsEmuCpuRegisters.AH);

            //Verify Flags
            Assert.True(mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.CF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.ZF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.OF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.SF));
        }
    }
}
