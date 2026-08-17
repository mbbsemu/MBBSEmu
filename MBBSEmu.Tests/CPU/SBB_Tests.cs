using MBBSEmu.CPU;
using MBBSEmu.Extensions;
using Xunit;

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
    }
}
