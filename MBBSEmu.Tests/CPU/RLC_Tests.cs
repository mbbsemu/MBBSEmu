using Iced.Intel;
using Xunit;
using static Iced.Intel.AssemblerRegisters;

namespace MBBSEmu.Tests.CPU
{
    public class RCL_Tests : CpuTestBase
    {
        [Theory]
        [InlineData(0x8000, 1, false, 0x0000, true)] // Rotate left with carry in, resulting in CF set
        [InlineData(0x8000, 1, true, 0x0001, true)] // Rotate left with carry in, resulting in CF set, and LSB set from previous CF
        [InlineData(0x0001, 1, false, 0x0002, false)] // Simple rotate left
        [InlineData(0x0000, 1, true, 0x0001, false)] // Rotate with carry flag set, no bit set in value
        [InlineData(0xFFFF, 4, false, 0xFFF7, true)] // Rotate left multiple times
        public void Op_Rcl_16_Test(ushort axValue, byte bitsToRotate, bool initialCarryFlag, ushort expectedResult,
            bool expectedCarryFlag)
        {
            Reset();
            mbbsEmuCpuRegisters.AX = axValue;
            mbbsEmuCpuRegisters.CarryFlag = initialCarryFlag;

            var instructions = new Assembler(16);
            instructions.rcl(ax, bitsToRotate);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(expectedResult, mbbsEmuCpuRegisters.AX);
            Assert.Equal(expectedCarryFlag, mbbsEmuCpuRegisters.CarryFlag);
        }

        [Theory]
        [InlineData(0x80, 1, false, 0x00, true)] // Rotate left with carry in, resulting in CF set
        [InlineData(0x80, 1, true, 0x01, true)] // Rotate left with carry in, resulting in CF set, and LSB set from previous CF
        [InlineData(0x01, 1, false, 0x02, false)] // Simple rotate left
        [InlineData(0x00, 1, true, 0x01, false)] // Rotate with carry flag set, no bit set in value
        [InlineData(0xFF, 4, false, 0xF7, true)] // Rotate left multiple times
        public void Op_Rcl_8_Test(byte alValue, byte bitsToRotate, bool initialCarryFlag, ushort expectedResult,
            bool expectedCarryFlag)
        {
            Reset();
            mbbsEmuCpuRegisters.AL = alValue;
            mbbsEmuCpuRegisters.CarryFlag = initialCarryFlag;

            var instructions = new Assembler(16);
            instructions.rcl(al, bitsToRotate);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(expectedResult, mbbsEmuCpuRegisters.AL);
            Assert.Equal(expectedCarryFlag, mbbsEmuCpuRegisters.CarryFlag);

        }
    }
}
