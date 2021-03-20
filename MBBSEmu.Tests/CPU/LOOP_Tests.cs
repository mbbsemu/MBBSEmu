using FluentAssertions;
using Iced.Intel;
using MBBSEmu.CPU;
using MBBSEmu.Extensions;
using Xunit;
using static Iced.Intel.AssemblerRegisters;

namespace MBBSEmu.Tests.CPU
{
    public class LOOP_Tests : CpuTestBase
    {
        [Fact]
        public void LOOP()
        {
            Reset();

            var instructions = new Assembler(16);
            var loop = instructions.CreateLabel();

            instructions.mov(cx, 10);
            instructions.Label(ref loop);
            instructions.inc(bx);
            instructions.loop(loop);
            instructions.hlt();
            CreateCodeSegment(instructions);

            //Process Instruction
            while (!mbbsEmuCpuRegisters.Halt)
                mbbsEmuCpuCore.Tick();

            //Verify Results
            mbbsEmuCpuRegisters.BX.Should().Be(10);
            mbbsEmuCpuRegisters.CX.Should().Be(0);
        }

        [Fact]
        public void LOOP_Loops()
        {
            Reset();

            var instructions = new Assembler(16);
            var loop = instructions.CreateLabel();

            mbbsEmuCpuRegisters.CX = 2;
            instructions.Label(ref loop);
            instructions.loop(loop);
            CreateCodeSegment(instructions);

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Results
            mbbsEmuCpuRegisters.CX.Should().Be(1);
            mbbsEmuCpuRegisters.IP.Should().Be(0);
        }

        [Fact]
        public void LOOP_NoLoop()
        {
            Reset();

            var instructions = new Assembler(16);
            var loop = instructions.CreateLabel();

            mbbsEmuCpuRegisters.CX = 1;
            instructions.Label(ref loop);
            instructions.loop(loop);
            CreateCodeSegment(instructions);

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Results
            mbbsEmuCpuRegisters.CX.Should().Be(0);
            mbbsEmuCpuRegisters.IP.Should().Be(2);
        }

        [Fact]
        public void LOOPE_LoopsWithZero()
        {
            Reset();

            var instructions = new Assembler(16);
            var loop = instructions.CreateLabel();

            mbbsEmuCpuRegisters.F = (ushort)EnumFlags.ZF;
            mbbsEmuCpuRegisters.CX = 2;
            instructions.Label(ref loop);
            instructions.loope(loop);
            CreateCodeSegment(instructions);

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Results
            mbbsEmuCpuRegisters.CX.Should().Be(1);
            mbbsEmuCpuRegisters.IP.Should().Be(0);
        }

        [Fact]
        public void LOOPE_NoLoopWithoutZero()
        {
            Reset();

            var instructions = new Assembler(16);
            var loop = instructions.CreateLabel();

            mbbsEmuCpuRegisters.F = 0;
            mbbsEmuCpuRegisters.CX = 2;
            instructions.Label(ref loop);
            instructions.loope(loop);
            CreateCodeSegment(instructions);

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Results
            mbbsEmuCpuRegisters.CX.Should().Be(1);
            mbbsEmuCpuRegisters.IP.Should().Be(2);
        }

        [Fact]
        public void LOOPE_NoLoopNoCX()
        {
            Reset();

            var instructions = new Assembler(16);
            var loop = instructions.CreateLabel();

            mbbsEmuCpuRegisters.F = (ushort)EnumFlags.ZF;
            mbbsEmuCpuRegisters.CX = 1;
            instructions.Label(ref loop);
            instructions.loope(loop);
            CreateCodeSegment(instructions);

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Results
            mbbsEmuCpuRegisters.CX.Should().Be(0);
            mbbsEmuCpuRegisters.IP.Should().Be(2);
        }

        [Fact]
        public void LOOPNE_LoopsWithoutZero()
        {
            Reset();

            var instructions = new Assembler(16);
            var loop = instructions.CreateLabel();

            mbbsEmuCpuRegisters.F = 0;
            mbbsEmuCpuRegisters.CX = 2;
            instructions.Label(ref loop);
            instructions.loopne(loop);
            CreateCodeSegment(instructions);

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Results
            mbbsEmuCpuRegisters.CX.Should().Be(1);
            mbbsEmuCpuRegisters.IP.Should().Be(0);
        }

        [Fact]
        public void LOOPNE_NoLoopWithZero()
        {
            Reset();

            var instructions = new Assembler(16);
            var loop = instructions.CreateLabel();

            mbbsEmuCpuRegisters.F = (ushort)EnumFlags.ZF;
            mbbsEmuCpuRegisters.CX = 2;
            instructions.Label(ref loop);
            instructions.loopne(loop);
            CreateCodeSegment(instructions);

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Results
            mbbsEmuCpuRegisters.CX.Should().Be(1);
            mbbsEmuCpuRegisters.IP.Should().Be(2);
        }

        [Fact]
        public void LOOPNE_NoLoopNoCX()
        {
            Reset();

            var instructions = new Assembler(16);
            var loop = instructions.CreateLabel();

            mbbsEmuCpuRegisters.F = 0;
            mbbsEmuCpuRegisters.CX = 1;
            instructions.Label(ref loop);
            instructions.loopne(loop);
            CreateCodeSegment(instructions);

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Results
            mbbsEmuCpuRegisters.CX.Should().Be(0);
            mbbsEmuCpuRegisters.IP.Should().Be(2);
        }
    }
}
