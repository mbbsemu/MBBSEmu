using Iced.Intel;
using System;
using Xunit;
using static Iced.Intel.AssemblerRegisters;

namespace MBBSEmu.Tests.CPU
{
    public class ROR_Tests : CpuTestBase
    {
        [Fact]
        public void ROR_AX_1()
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
        public void ROR_AX_CF_OF()
        {
            Reset();
            mbbsEmuCpuRegisters.AX = 1;


            var instructions = new Assembler(16);
            instructions.ror(ax, 1);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(0x8000, mbbsEmuCpuRegisters.AX);
            Assert.True(mbbsEmuCpuRegisters.F.IsFlagSet(MBBSEmu.CPU.EnumFlags.CF));
            Assert.True(mbbsEmuCpuRegisters.F.IsFlagSet(MBBSEmu.CPU.EnumFlags.OF));
        }

        [Fact]
        public void ROR_AX_OF()
        {
            Reset();
            mbbsEmuCpuRegisters.AX = 0x8000;


            var instructions = new Assembler(16);
            instructions.ror(ax, 1);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(0x8000 >> 1, mbbsEmuCpuRegisters.AX);
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet(MBBSEmu.CPU.EnumFlags.CF));
            Assert.True(mbbsEmuCpuRegisters.F.IsFlagSet(MBBSEmu.CPU.EnumFlags.OF));
        }

        [Fact]
        public void ROR_AL_1()
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
        public void ROR_AL_CF_OF()
        {
            Reset();
            mbbsEmuCpuRegisters.AL = 1;


            var instructions = new Assembler(16);
            instructions.ror(al, 1);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(0x80, mbbsEmuCpuRegisters.AX);
            Assert.True(mbbsEmuCpuRegisters.F.IsFlagSet(MBBSEmu.CPU.EnumFlags.CF));
            Assert.True(mbbsEmuCpuRegisters.F.IsFlagSet(MBBSEmu.CPU.EnumFlags.OF));
        }

        [Fact]
        public void ROR_AL_OF()
        {
            Reset();
            mbbsEmuCpuRegisters.AL = 0x80;


            var instructions = new Assembler(16);
            instructions.ror(al, 1);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(0x80 >> 1, mbbsEmuCpuRegisters.AX);
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet(MBBSEmu.CPU.EnumFlags.CF));
            Assert.True(mbbsEmuCpuRegisters.F.IsFlagSet(MBBSEmu.CPU.EnumFlags.OF));
        }
    }
}
