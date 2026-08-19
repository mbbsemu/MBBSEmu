using Iced.Intel;
using MBBSEmu.CPU;
using MBBSEmu.Extensions;
using Xunit;
using static Iced.Intel.AssemblerRegisters;

namespace MBBSEmu.Tests.CPU
{
    public class SBB_Tests : CpuTestBase
    {
        [Theory]
        [InlineData(0x05, 0x03, false, 0x02, false)] // Simple subtraction, no borrow-in
        [InlineData(0x05, 0x03, true, 0x01, false)] // Subtraction with borrow-in, no resulting borrow
        [InlineData(0x00, 0x00, true, 0xFF, true)] // Borrow-in on zero operands must borrow
        public void SBB_AL_IMM8(byte alValue, byte value, bool initialCarryFlag, byte expectedValue,
            bool expectedCarryFlag)
        {
            Reset();
            mbbsEmuCpuRegisters.AL = alValue;
            mbbsEmuCpuRegisters.CarryFlag = initialCarryFlag;

            //SBB AL, imm8
            CreateCodeSegment(new byte[] { 0x1C, value });

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Results
            Assert.Equal(expectedValue, mbbsEmuCpuRegisters.AL);
            Assert.Equal(expectedCarryFlag, mbbsEmuCpuRegisters.CarryFlag);
        }

        [Fact]
        public void SBB_AL_IMM8_SourceMaxValueWithBorrowIn_DoesNotWrapCarryFlag()
        {
            Reset();
            mbbsEmuCpuRegisters.AL = 0x00;
            mbbsEmuCpuRegisters.CarryFlag = true;

            //SBB AL, 0xFF
            CreateCodeSegment(new byte[] { 0x1C, 0xFF });

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Results: 0x00 - 0xFF - 1 == -256, which is congruent to 0x00 (mod 256), and a borrow occurred
            Assert.Equal(0x00, mbbsEmuCpuRegisters.AL);
            Assert.True(mbbsEmuCpuRegisters.CarryFlag);
            Assert.True(mbbsEmuCpuRegisters.ZeroFlag);
        }

        [Fact]
        public void SBB_AX_IMM16_SourceMaxValueWithBorrowIn_DoesNotWrapCarryFlag()
        {
            Reset();
            mbbsEmuCpuRegisters.AX = 0x0000;
            mbbsEmuCpuRegisters.CarryFlag = true;

            //SBB AX, 0xFFFF
            CreateCodeSegment(new byte[] { 0x1D, 0xFF, 0xFF });

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Results: 0x0000 - 0xFFFF - 1 == -65536, which is congruent to 0x0000 (mod 65536), and a borrow occurred
            Assert.Equal(0x0000, mbbsEmuCpuRegisters.AX);
            Assert.True(mbbsEmuCpuRegisters.CarryFlag);
            Assert.True(mbbsEmuCpuRegisters.ZeroFlag);
        }

        [Theory]
        [InlineData(0x00000005u, 0x00000003u, false, 0x00000002u, false)] // Simple subtraction, no borrow-in
        [InlineData(0x00000005u, 0x00000003u, true, 0x00000001u, false)] // Borrow-in, no resulting borrow
        [InlineData(0x00000000u, 0x00000000u, true, 0xFFFFFFFFu, true)] // Borrow-in on zero operands must borrow
        [InlineData(0x00010000u, 0x00000001u, false, 0x0000FFFFu, false)] // Borrow across the low word
        [InlineData(0x00000000u, 0x00000001u, false, 0xFFFFFFFFu, true)] // Plain underflow
        public void SBB_EAX_EBX_32Bit(uint eaxValue, uint ebxValue, bool initialCarryFlag,
            uint expectedValue, bool expectedCarryFlag)
        {
            Reset();
            mbbsEmuCpuRegisters.EAX = eaxValue;
            mbbsEmuCpuRegisters.EBX = ebxValue;
            mbbsEmuCpuRegisters.CarryFlag = initialCarryFlag;

            var instructions = new Assembler(16);
            instructions.sbb(eax, ebx);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(expectedValue, mbbsEmuCpuRegisters.EAX);
            Assert.Equal(expectedCarryFlag, mbbsEmuCpuRegisters.CarryFlag);
        }

        [Fact]
        public void SBB_EAX_EBX_SourceMaxValueWithBorrowIn_DoesNotWrapCarryFlag()
        {
            Reset();
            mbbsEmuCpuRegisters.EAX = 0x00000000;
            mbbsEmuCpuRegisters.EBX = 0xFFFFFFFF;
            mbbsEmuCpuRegisters.CarryFlag = true;

            var instructions = new Assembler(16);
            instructions.sbb(eax, ebx);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            //Verify Results: 0x00000000 - 0xFFFFFFFF - 1 == -4294967296, congruent to 0x00000000
            //(mod 2^32), and a borrow occurred. Computing this in 32-bit arithmetic would wrap the
            //subtrahend to zero and report no borrow.
            Assert.Equal(0x00000000u, mbbsEmuCpuRegisters.EAX);
            Assert.True(mbbsEmuCpuRegisters.CarryFlag);
            Assert.True(mbbsEmuCpuRegisters.ZeroFlag);
        }
    }
}
