using FluentAssertions;
using Iced.Intel;
using MBBSEmu.CPU;
using MBBSEmu.Extensions;
using Xunit;
using static Iced.Intel.AssemblerRegisters;

namespace MBBSEmu.Tests.CPU
{
    public class XCHG_Tests : CpuTestBase
    {
        [Fact]
        public void XCHG_8Bit_Regs()
        {
            Reset();

            mbbsEmuCpuRegisters.AL = 5;
            mbbsEmuCpuRegisters.AH = 8;

            var instructions = new Assembler(16);
            instructions.xchg(al, ah);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            mbbsEmuCpuRegisters.AL.Should().Be(8);
            mbbsEmuCpuRegisters.AH.Should().Be(5);
        }

        [Fact]
        public void XCHG_16Bit_Regs()
        {
            Reset();

            mbbsEmuCpuRegisters.AX = 0x5050;
            mbbsEmuCpuRegisters.BX = 0x6060;

            var instructions = new Assembler(16);
            instructions.xchg(ax, bx);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            mbbsEmuCpuRegisters.AX.Should().Be(0x6060);
            mbbsEmuCpuRegisters.BX.Should().Be(0x5050);
        }

        [Fact]
        public void XCHG_8Bit_Reg_To_Memory()
        {
            Reset();

            // data segment
            mbbsEmuProtectedModeMemoryCore.AddSegment(2);

            mbbsEmuCpuRegisters.ES = mbbsEmuCpuRegisters.DS = 2;
            mbbsEmuMemoryCore.SetByte(mbbsEmuCpuRegisters.DS, 0x300, 8);

            mbbsEmuCpuRegisters.AL = 5;

            var instructions = new Assembler(16);
            instructions.xchg(__byte_ptr[0x300], al);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            mbbsEmuCpuRegisters.AL.Should().Be(8);
            mbbsEmuMemoryCore.GetByte(mbbsEmuCpuRegisters.DS, 0x300).Should().Be(5);
        }

        [Fact]
        public void XCHG_16Bit_Reg_To_Memory()
        {
            Reset();

            // data segment
            mbbsEmuProtectedModeMemoryCore.AddSegment(2);

            mbbsEmuCpuRegisters.ES = mbbsEmuCpuRegisters.DS = 2;
            mbbsEmuMemoryCore.SetWord(mbbsEmuCpuRegisters.DS, 0x300, 0x8080);

            mbbsEmuCpuRegisters.AX = 0x5050;

            var instructions = new Assembler(16);
            instructions.xchg(__word_ptr[0x300], ax);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            mbbsEmuCpuRegisters.AX.Should().Be(0x8080);
            mbbsEmuMemoryCore.GetWord(mbbsEmuCpuRegisters.DS, 0x300).Should().Be(0x5050);
        }
    }
}
