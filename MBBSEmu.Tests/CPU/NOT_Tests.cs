using Iced.Intel;
using Xunit;
using static Iced.Intel.AssemblerRegisters;

namespace MBBSEmu.Tests.CPU
{
    public class NOT_Tests : CpuTestBase
    {
        [Theory]
        [InlineData((byte)0x00, (byte)0xFF)]
        [InlineData((byte)0xFF, (byte)0x00)]
        [InlineData((byte)0x0F, (byte)0xF0)]
        public void NOT_AL(byte alValue, byte expectedValue)
        {
            Reset();
            mbbsEmuCpuRegisters.AL = alValue;

            var instructions = new Assembler(16);
            instructions.not(al);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(expectedValue, mbbsEmuCpuRegisters.AL);
        }

        [Theory]
        [InlineData((ushort)0x0000, (ushort)0xFFFF)]
        [InlineData((ushort)0xFFFF, (ushort)0x0000)]
        [InlineData((ushort)0x00FF, (ushort)0xFF00)]
        public void NOT_AX(ushort axValue, ushort expectedValue)
        {
            Reset();
            mbbsEmuCpuRegisters.AX = axValue;

            var instructions = new Assembler(16);
            instructions.not(ax);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(expectedValue, mbbsEmuCpuRegisters.AX);
        }

        [Theory]
        [InlineData(0x00000000u, 0xFFFFFFFFu)] // Must invert all 32 bits, not just the low 16
        [InlineData(0xFFFFFFFFu, 0x00000000u)]
        [InlineData(0x0000FFFFu, 0xFFFF0000u)]
        [InlineData(0xFF00FF00u, 0x00FF00FFu)]
        public void NOT_EAX_32Bit(uint eaxValue, uint expectedValue)
        {
            Reset();
            mbbsEmuCpuRegisters.EAX = eaxValue;

            var instructions = new Assembler(16);
            instructions.not(eax);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(expectedValue, mbbsEmuCpuRegisters.EAX);
        }
    }
}
