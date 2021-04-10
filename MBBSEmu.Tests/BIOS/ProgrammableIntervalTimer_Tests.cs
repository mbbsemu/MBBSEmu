using FluentAssertions;
using Iced.Intel;
using MBBSEmu.BIOS;
using MBBSEmu.Tests.CPU;
using System;
using Xunit;
using static Iced.Intel.AssemblerRegisters;

namespace MBBSEmu.Tests.BIOS
{
    public class ProgrammableIntervalTimer_Tests : CpuTestBase
    {
        [Theory]
        [InlineData(0x40)]
        [InlineData(0x41)]
        [InlineData(0x42)]
        public void CantWriteToInvalidPorts(byte port)
        {
            Reset();

            var instructions = new Assembler(16);
            instructions.@out(port, al);
            instructions.hlt();

            CreateCodeSegment(instructions);

            Action action = () => mbbsEmuCpuCore.Tick();

            action.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void CantSetBCIDMode()
        {
            Reset();

            mbbsEmuCpuRegisters.AL = 1;

            var instructions = new Assembler(16);
            instructions.@out(0x43, al);
            instructions.hlt();

            CreateCodeSegment(instructions);

            Action action = () => mbbsEmuCpuCore.Tick();

            action.Should().Throw<ArgumentException>();
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        public void ReadLowByteOnly(byte channel)
        {
            Reset();

            mbbsEmuCpuRegisters.AL = (byte)((1 << 4) | (channel << 6)); // lo byte only
            fakeClock.CurrentTick = 0.25;
            var expected = (byte)(int)(ProgrammableIntervalTimer.FREQUENCY * (1.0 - fakeClock.CurrentTick));

            var instructions = new Assembler(16);
            instructions.@out(0x43, al);
            instructions.@in(al, (byte)(0x40 + channel));
            instructions.mov(ah, al);
            instructions.@in(al, (byte)(0x40 + channel));
            instructions.hlt();

            CreateCodeSegment(instructions);

            while (!mbbsEmuCpuRegisters.Halt)
                mbbsEmuCpuCore.Tick();

            mbbsEmuCpuRegisters.AL.Should().Be(expected);
            mbbsEmuCpuRegisters.AH.Should().Be(expected);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        public void ReadHighByteOnly(byte channel)
        {
            Reset();

            mbbsEmuCpuRegisters.AL = (byte)((2 << 4) | (channel << 6));
            fakeClock.CurrentTick = 0.25;
            var expected = (ushort)(int)(ProgrammableIntervalTimer.FREQUENCY * (1.0 - fakeClock.CurrentTick));

            var instructions = new Assembler(16);
            instructions.@out(0x43, al);
            instructions.@in(al, (byte)(0x40 + channel));
            instructions.mov(ah, al);
            instructions.@in(al, (byte)(0x40 + channel));
            instructions.hlt();

            CreateCodeSegment(instructions);

            while (!mbbsEmuCpuRegisters.Halt)
                mbbsEmuCpuCore.Tick();

            mbbsEmuCpuRegisters.AL.Should().Be((byte)(expected >> 8));
            mbbsEmuCpuRegisters.AH.Should().Be((byte)(expected >> 8));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        public void ReadHighAndLowByte(byte channel)
        {
            Reset();

            mbbsEmuCpuRegisters.AL = (byte)((3 << 4) | (channel << 6));
            fakeClock.CurrentTick = 0.25;
            var expected = (ushort)(int)(ProgrammableIntervalTimer.FREQUENCY * (1.0 - fakeClock.CurrentTick));

            var instructions = new Assembler(16);
            instructions.@out(0x43, al);
            instructions.@in(al, (byte)(0x40 + channel));
            instructions.mov(ah, al);
            instructions.@in(al, (byte)(0x40 + channel));
            instructions.xchg(ah, al);
            instructions.hlt();

            CreateCodeSegment(instructions);

            while (!mbbsEmuCpuRegisters.Halt)
                mbbsEmuCpuCore.Tick();

            mbbsEmuCpuRegisters.AL.Should().Be((byte)expected);
            mbbsEmuCpuRegisters.AH.Should().Be((byte)(expected >> 8));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        public void ReadLatchedValue(byte channel)
        {
            Reset();

            mbbsEmuCpuRegisters.AL = (byte)((0 << 4) | (channel << 6)); // latched
            fakeClock.CurrentTick = 0.25;
            var expected = (ushort)(int)(ProgrammableIntervalTimer.FREQUENCY * (1.0 - fakeClock.CurrentTick));

            var instructions = new Assembler(16);
            instructions.@out(0x43, al);
            instructions.@in(al, (byte)(0x40 + channel));
            instructions.mov(ah, al);
            instructions.@in(al, (byte)(0x40 + channel));
            instructions.xchg(ah, al);
            instructions.hlt();

            CreateCodeSegment(instructions);

            // program the PIT - sets the latched value
            mbbsEmuCpuCore.Tick();

            // reset clock to another value to verify latched value is read
            // and not this new value
            fakeClock.CurrentTick = 0;

            while (!mbbsEmuCpuRegisters.Halt)
                mbbsEmuCpuCore.Tick();

            mbbsEmuCpuRegisters.AL.Should().Be((byte)expected);
            mbbsEmuCpuRegisters.AH.Should().Be((byte)(expected >> 8));
        }
    }
}
