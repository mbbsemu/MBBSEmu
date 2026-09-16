using Iced.Intel;
using Xunit;
using static Iced.Intel.AssemblerRegisters;

namespace MBBSEmu.Tests.CPU
{
    public class NEG_Tests : CpuTestBase
    {
        //Unit Tests using xUnit to perform testing of the NEG opcode in CPUCore
        [Theory]
        [InlineData(0x00, 0x00, false, false, false)]
        [InlineData(0x01, 0xFF, true, true, false)]
        [InlineData(0x7F, 0x81, true, true, false)]
        [InlineData(0x80, 0x80, true, true, true)]
        [InlineData(0xFF, 0x01, false, true, false)]
        [InlineData(0x81, 0x7F, false, true, false)]
        public void NEG_8_Register_Test(byte input, byte expectedValue, bool expectedSF, bool expectedCF, bool expectedOF)
        {
            Reset();
            mbbsEmuCpuRegisters.AL = input;

            var instructions = new Assembler(16);
            instructions.neg(al);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(expectedValue, mbbsEmuCpuRegisters.AL);
            Assert.Equal(expectedSF, mbbsEmuCpuRegisters.SignFlag);
            Assert.Equal(expectedCF, mbbsEmuCpuRegisters.CarryFlag);
            Assert.Equal(expectedOF, mbbsEmuCpuRegisters.OverflowFlag);
        }

        //16bit Register Unit Tests for NEG
        [Theory]
        [InlineData(0x0000, 0x0000, false, false, false)]
        [InlineData(0x0001, 0xFFFF, true, true, false)]
        [InlineData(0x7FFF, 0x8001, true, true, false)]
        [InlineData(0x8000, 0x8000, true, true, true)]
        [InlineData(0xFFFF, 0x0001, false, true, false)]
        [InlineData(0x8001, 0x7FFF, false, true, false)]
        [InlineData(0xFF87, 0x0079, false, true, false)] //neg cx in Borland startup code (e.g. BBSFNDO.EXE)
        public void NEG_16_Register_Test(ushort input, ushort expectedValue, bool expectedSF, bool expectedCF, bool expectedOF)
        {
            Reset();
            mbbsEmuCpuRegisters.AX = input;

            var instructions = new Assembler(16);
            instructions.neg(ax);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(expectedValue, mbbsEmuCpuRegisters.AX);
            Assert.Equal(expectedSF, mbbsEmuCpuRegisters.SignFlag);
            Assert.Equal(expectedCF, mbbsEmuCpuRegisters.CarryFlag);
            Assert.Equal(expectedOF, mbbsEmuCpuRegisters.OverflowFlag);
        }

        [Theory]
        [InlineData(0x00000000u, 0x00000000u, false, false, false)]
        [InlineData(0x00000001u, 0xFFFFFFFFu, true, true, false)]
        [InlineData(0x7FFFFFFFu, 0x80000001u, true, true, false)]
        [InlineData(0x80000000u, 0x80000000u, true, true, true)]
        [InlineData(0xFFFFFFFFu, 0x00000001u, false, true, false)]
        public void NEG_32_Register_Test(uint input, uint expectedValue, bool expectedSF, bool expectedCF, bool expectedOF)
        {
            Reset();
            mbbsEmuCpuRegisters.EAX = input;

            var instructions = new Assembler(16);
            instructions.neg(eax);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(expectedValue, mbbsEmuCpuRegisters.EAX);
            Assert.Equal(expectedSF, mbbsEmuCpuRegisters.SignFlag);
            Assert.Equal(expectedCF, mbbsEmuCpuRegisters.CarryFlag);
            Assert.Equal(expectedOF, mbbsEmuCpuRegisters.OverflowFlag);
        }

        [Theory]
        [InlineData(0x00000001u, true)] // 0x0 - 0x1 borrows from the low nibble
        [InlineData(0x0000000Fu, true)]
        [InlineData(0x00000000u, false)]
        [InlineData(0x00000010u, false)]
        public void NEG_AuxiliaryCarryFlag(uint value, bool expectedAuxiliaryCarryFlag)
        {
            Reset();

            mbbsEmuCpuRegisters.AL = (byte)value;
            mbbsEmuCpuRegisters.BX = (ushort)value;
            mbbsEmuCpuRegisters.ECX = value;

            var instructions = new Assembler(16);
            instructions.neg(al);
            instructions.neg(bx);
            instructions.neg(ecx);
            CreateCodeSegment(instructions);

            for (var i = 0; i < 3; i++)
            {
                mbbsEmuCpuRegisters.AuxiliaryCarryFlag = !expectedAuxiliaryCarryFlag;
                mbbsEmuCpuCore.Tick();
                Assert.Equal(expectedAuxiliaryCarryFlag, mbbsEmuCpuRegisters.AuxiliaryCarryFlag);
            }
        }
    }
}
