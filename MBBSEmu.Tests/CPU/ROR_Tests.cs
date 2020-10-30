using Iced.Intel;
using System;
using MBBSEmu.Extensions;
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
            Assert.True(mbbsEmuCpuRegisters.F.IsFlagSet((ushort)MBBSEmu.CPU.EnumFlags.CF));
            Assert.True(mbbsEmuCpuRegisters.F.IsFlagSet((ushort)MBBSEmu.CPU.EnumFlags.OF));
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
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet((ushort)MBBSEmu.CPU.EnumFlags.CF));
            Assert.True(mbbsEmuCpuRegisters.F.IsFlagSet((ushort)MBBSEmu.CPU.EnumFlags.OF));
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
            Assert.True(mbbsEmuCpuRegisters.F.IsFlagSet((ushort)MBBSEmu.CPU.EnumFlags.CF));
            Assert.True(mbbsEmuCpuRegisters.F.IsFlagSet((ushort)MBBSEmu.CPU.EnumFlags.OF));
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
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet((ushort)MBBSEmu.CPU.EnumFlags.CF));
            Assert.True(mbbsEmuCpuRegisters.F.IsFlagSet((ushort)MBBSEmu.CPU.EnumFlags.OF));
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

            Assert.Equal(2 >> 1, mbbsEmuMemoryCore.GetByte(2,0));
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
            instructions.ror(__byte_ptr[0],cl);
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
    }
}
