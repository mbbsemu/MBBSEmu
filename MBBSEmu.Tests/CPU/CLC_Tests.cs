using FluentAssertions;
using Iced.Intel;
using Xunit;

namespace MBBSEmu.Tests.CPU
{
    public class CLC_Tests : CpuTestBase
    {
        [Fact]
        public void CLC()
        {
            Reset();

            mbbsEmuCpuRegisters.F = 0xFFFF;

            var instructions = new Assembler(16);
            instructions.clc();
            CreateCodeSegment(instructions);

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Results
            mbbsEmuCpuRegisters.CarryFlag.Should().BeFalse();
        }

        [Fact]
        public void STC()
        {
            Reset();

            mbbsEmuCpuRegisters.F = 0;

            var instructions = new Assembler(16);
            instructions.stc();
            CreateCodeSegment(instructions);

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Results
            mbbsEmuCpuRegisters.CarryFlag.Should().BeTrue();
        }
    }
}
