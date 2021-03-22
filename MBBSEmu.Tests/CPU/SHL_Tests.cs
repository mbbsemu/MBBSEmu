using Iced.Intel;
using MBBSEmu.CPU;
using MBBSEmu.Extensions;
using Xunit;
using static Iced.Intel.AssemblerRegisters;

namespace MBBSEmu.Tests.CPU
{
    public class SHL_Tests : CpuTestBase
    {
        [Theory]
        [InlineData(0xFFFF, 0xFFFE)]
        public void SHL_AX_CarryFlag_SignFlag(ushort axValue, ushort axExpectedValue)
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
            Assert.True(mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.CF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.ZF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.OF));
            Assert.True(mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.SF));
        }

        [Theory]
        [InlineData(0x7FFF, 0xFFFE)]
        public void SHL_AX_OverflowFlag_SignFlag(ushort axValue, ushort axExpectedValue)
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

        [Theory]
        [InlineData(0xFFFFFFFF, 0xFFFFFFFE)]
        public void SHL_EAX_CarryFlag_SignFlag(uint eaxValue, uint axExpectedValue)
        {
            Reset();

            mbbsEmuCpuRegisters.EAX = eaxValue;

            var instructions = new Assembler(16);
            instructions.shl(eax, 1);
            CreateCodeSegment(instructions);

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Results
            Assert.Equal(axExpectedValue, mbbsEmuCpuRegisters.EAX);

            //Verify Flags
            Assert.True(mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.CF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.ZF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.OF));
            Assert.True(mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.SF));
        }

        [Theory]
        [InlineData(0x7FFFFFFF, 0xFFFFFFFE)]
        public void SHL_EAX_OverflowFlag_SignFlag(uint eaxValue, uint axExpectedValue)
        {
            Reset();

            mbbsEmuCpuRegisters.EAX = eaxValue;

            var instructions = new Assembler(16);
            instructions.shl(eax, 1);
            CreateCodeSegment(instructions);

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Results
            Assert.Equal(axExpectedValue, mbbsEmuCpuRegisters.EAX);

            //Verify Flags
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.CF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.ZF));
            Assert.True(mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.OF));
            Assert.True(mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.SF));
        }

        [Fact]
        public void SHL_AH_Big_Shift()
        {
            Reset();

            mbbsEmuCpuRegisters.AH = 0xFF;

            var instructions = new Assembler(16);
            instructions.shl(ah, 35); // will clamp to 3
            CreateCodeSegment(instructions);

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Results
            Assert.Equal(0xF8, mbbsEmuCpuRegisters.AH);

            //Verify Flags
            Assert.True(mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.CF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.ZF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.OF));
            Assert.True(mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.SF));
        }

        [Fact]
        public void SHL_AX_Big_Shift()
        {
            Reset();

            mbbsEmuCpuRegisters.AX = 0xFFFF;

            var instructions = new Assembler(16);
            instructions.shl(ax, 35); // will clamp to 3
            CreateCodeSegment(instructions);

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Results
            Assert.Equal(0xFFF8, mbbsEmuCpuRegisters.AX);

            //Verify Flags
            Assert.True(mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.CF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.ZF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.OF));
            Assert.True(mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.SF));
        }

        [Fact]
        public void SHL_EAX_Big_Shift()
        {
            Reset();

            mbbsEmuCpuRegisters.EAX = 0xFFFFFFFF;

            var instructions = new Assembler(16);
            instructions.shl(eax, 35); // will clamp to 3
            CreateCodeSegment(instructions);

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Results
            Assert.Equal(0xFFFFFFF8, mbbsEmuCpuRegisters.EAX);

            //Verify Flags
            Assert.True(mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.CF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.ZF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.OF));
            Assert.True(mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.SF));
        }
    }
}
