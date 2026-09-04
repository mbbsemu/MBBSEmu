using Iced.Intel;
using Xunit;

namespace MBBSEmu.Tests.CPU
{
    public class JP_JNP_Tests : CpuTestBase
    {
        [Theory]
        [InlineData(true, 0)]
        [InlineData(false, 2)]
        public void JP_Test(bool parityFlagValue, ushort ipValue)
        {
            Reset();

            var instructions = new Assembler(16);
            instructions.jp(0);
            CreateCodeSegment(instructions);

            mbbsEmuCpuRegisters.ParityFlag = parityFlagValue;
            mbbsEmuCpuCore.Tick();

            Assert.Equal(ipValue, mbbsEmuCpuRegisters.IP);
            Assert.Equal(parityFlagValue, mbbsEmuCpuRegisters.ParityFlag);
        }

        [Theory]
        [InlineData(true, 2)]
        [InlineData(false, 0)]
        public void JNP_Test(bool parityFlagValue, ushort ipValue)
        {
            Reset();

            var instructions = new Assembler(16);
            instructions.jnp(0);
            CreateCodeSegment(instructions);

            mbbsEmuCpuRegisters.ParityFlag = parityFlagValue;
            mbbsEmuCpuCore.Tick();

            Assert.Equal(ipValue, mbbsEmuCpuRegisters.IP);
            Assert.Equal(parityFlagValue, mbbsEmuCpuRegisters.ParityFlag);
        }

        [Theory]
        [InlineData(0x00, true)]
        [InlineData(0x01, false)]
        [InlineData(0x03, true)]
        [InlineData(0xFF, true)]
        public void LogicalOperation_EvaluatesParity(byte value, bool expected)
        {
            Reset();
            mbbsEmuCpuRegisters.AL = value;
            CreateCodeSegment(new byte[] { 0x34, 0x00 }); // XOR AL, 0

            mbbsEmuCpuCore.Tick();

            Assert.Equal(expected, mbbsEmuCpuRegisters.ParityFlag);
        }

        [Theory]
        [InlineData(0x04, true)]
        [InlineData(0x00, false)]
        public void SAHF_LoadsParityFlag(byte ah, bool expected)
        {
            Reset();
            mbbsEmuCpuRegisters.AH = ah;
            CreateCodeSegment(new byte[] { 0x9E }); // SAHF

            mbbsEmuCpuCore.Tick();

            Assert.Equal(expected, mbbsEmuCpuRegisters.ParityFlag);
        }
    }
}
