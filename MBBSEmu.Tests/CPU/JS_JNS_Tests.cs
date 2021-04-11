using Iced.Intel;
using MBBSEmu.CPU;
using MBBSEmu.Extensions;
using Xunit;

namespace MBBSEmu.Tests.CPU
{
    public class JS_JNS_Tests : CpuTestBase
    {
        [Theory]
        [InlineData(true, 0)]
        [InlineData(false, 2)]
        public void JS_Test(bool signFlagValue, ushort ipValue)
        {
            Reset();

            var instructions = new Assembler(16);
            instructions.js(0);
            CreateCodeSegment(instructions);

            if (signFlagValue)
                mbbsEmuCpuRegisters.F = mbbsEmuCpuRegisters.F.SetFlag((ushort)EnumFlags.SF);

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Values
            Assert.Equal(ipValue, mbbsEmuCpuRegisters.IP);

            //Verify Flags
            if (signFlagValue)
            {
                Assert.True(mbbsEmuCpuRegisters.SignFlag);
            }
            else
            {
                Assert.False(mbbsEmuCpuRegisters.SignFlag);
            }

            Assert.False(mbbsEmuCpuRegisters.OverflowFlag);
            Assert.False(mbbsEmuCpuRegisters.ZeroFlag);
            Assert.False(mbbsEmuCpuRegisters.CarryFlag);
        }

        [Theory]
        [InlineData(true, 2)]
        [InlineData(false, 0)]
        public void JNS_Test(bool signFlagValue, ushort ipValue)
        {
            Reset();

            var instructions = new Assembler(16);
            instructions.jns(0);
            CreateCodeSegment(instructions);

            if (signFlagValue)
                mbbsEmuCpuRegisters.F = mbbsEmuCpuRegisters.F.SetFlag((ushort)EnumFlags.SF);

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Values
            Assert.Equal(ipValue, mbbsEmuCpuRegisters.IP);

            //Verify Flags
            if (signFlagValue)
            {
                Assert.True(mbbsEmuCpuRegisters.SignFlag);
            }
            else
            {
                Assert.False(mbbsEmuCpuRegisters.SignFlag);
            }

            Assert.False(mbbsEmuCpuRegisters.OverflowFlag);
            Assert.False(mbbsEmuCpuRegisters.ZeroFlag);
            Assert.False(mbbsEmuCpuRegisters.CarryFlag);
        }
    }
}
