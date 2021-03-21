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
        [InlineData(0xFFFF, 0x7FFF)]
        public void SHR_AX_CarryFlag_SignFlag(ushort axValue, ushort axExpectedValue)
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
            Assert.True(mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.OF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.SF));
        }

        [Theory]
        [InlineData(0x7FFF, 0xFFFE)]
        public void SHR_AX_OverflowFlag_SignFlag(ushort axValue, ushort axExpectedValue)
        {
            Reset();

            mbbsEmuCpuRegisters.AX = axValue;

            var instructions = new Assembler(16);
            instructions.shl(ax, 1);
            CreateCodeSegment(instructions);

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Results
            Assert.Equal(axExpectedValue, mbbsEmuCpuRegisters.AX);

            //Verify Flags
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.CF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.ZF));
            Assert.True(mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.OF));
            Assert.True(mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.SF));
        }
    }
}
