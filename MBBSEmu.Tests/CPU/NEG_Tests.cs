using Iced.Intel;
using Xunit;
using static Iced.Intel.AssemblerRegisters;

namespace MBBSEmu.Tests.CPU
{
    public class NEG_Tests : CpuTestBase
    {
        //Unit Tests using xUnit to perform testing of the NEG opcode in CPUCore
        [Theory]
        [InlineData(0x00, 0x00, false, false)]
        [InlineData(0x01, 0xFF, true, true)]
        [InlineData(0x7F, 0x81, true, true)]
        [InlineData(0x80, 0x80, true, true)]
        [InlineData(0xFF, 0x01, false, true)]
        [InlineData(0x81, 0x7F, false, true)]
        public void NEG_8_Register_Test(byte input, byte expectedValue, bool expectedSF, bool expectedCF)
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
        }

        //16bit Register Unit Tests for NEG
        [Theory]
        [InlineData(0x0000, 0x0000, false, false)]
        [InlineData(0x0001, 0xFFFF, true, true)]
        [InlineData(0x7FFF, 0x8001, true, true)]
        [InlineData(0x8000, 0x8000, true, true)]
        [InlineData(0xFFFF, 0x0001, false, true)]
        [InlineData(0x8001, 0x7FFF, false, true)]
        public void NEG_16_Register_Test(ushort input, ushort expectedValue, bool expectedSF, bool expectedCF)
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
        }

    }
}
