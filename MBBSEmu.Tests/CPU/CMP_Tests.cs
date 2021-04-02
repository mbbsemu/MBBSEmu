using MBBSEmu.CPU;
using MBBSEmu.Extensions;
using Xunit;

namespace MBBSEmu.Tests.CPU
{
    public class CMP_Tests : CpuTestBase
    {
        [Theory]
        [InlineData(0x0, 0x0)]
        [InlineData(0x1, 0x1)]
        public void CMP_AL_IMM8_ZeroFlag(byte alValue, byte sourceValue)
        {
            Reset();
            CreateCodeSegment(new byte[] { 0x3C, sourceValue });
            mbbsEmuCpuRegisters.AL = alValue;

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Flags
            Assert.False(mbbsEmuCpuRegisters.CarryFlag);
            Assert.True(mbbsEmuCpuRegisters.ZeroFlag);
            Assert.False(mbbsEmuCpuRegisters.OverflowFlag);
            Assert.False(mbbsEmuCpuRegisters.SignFlag);
        }

        [Theory]
        [InlineData(0x0, 0xFF)]
        public void CMP_AL_IMM8_CarryFlag(byte alValue, byte sourceValue)
        {
            Reset();
            CreateCodeSegment(new byte[] { 0x3C, sourceValue });
            mbbsEmuCpuRegisters.AL = alValue;

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Flags
            Assert.True(mbbsEmuCpuRegisters.CarryFlag);
            Assert.False(mbbsEmuCpuRegisters.ZeroFlag);
            Assert.False(mbbsEmuCpuRegisters.OverflowFlag);
            Assert.False(mbbsEmuCpuRegisters.SignFlag);
        }

        [Theory]
        [InlineData(0x2, 0x1)]
        public void CMP_AL_IMM8_ClearFlags(byte alValue, byte sourceValue)
        {
            Reset();
            CreateCodeSegment(new byte[] { 0x3C, sourceValue });
            mbbsEmuCpuRegisters.AL = alValue;

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Flags
            Assert.False(mbbsEmuCpuRegisters.CarryFlag);
            Assert.False(mbbsEmuCpuRegisters.ZeroFlag);
            Assert.False(mbbsEmuCpuRegisters.OverflowFlag);
            Assert.False(mbbsEmuCpuRegisters.SignFlag);
        }

        [Theory]
        [InlineData(0x1, 0x3)]
        public void CMP_AL_IMM8_CarryFlagSignFlag(byte alValue, byte sourceValue)
        {
            Reset();
            CreateCodeSegment(new byte[] { 0x3C, sourceValue });
            mbbsEmuCpuRegisters.AL = alValue;

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Flags
            Assert.True(mbbsEmuCpuRegisters.CarryFlag);
            Assert.False(mbbsEmuCpuRegisters.ZeroFlag);
            Assert.False(mbbsEmuCpuRegisters.OverflowFlag);
            Assert.True(mbbsEmuCpuRegisters.SignFlag);
        }
    }
}
