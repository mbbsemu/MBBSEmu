using MBBSEmu.CPU;
using Xunit;

namespace MBBSEmu.Tests.CPU
{
    public class JBE_Tests : CpuTestBase
    {
        [Theory]
        [InlineData(true, false)]
        [InlineData(false, true)]
        public void JBE_DoesJump(bool carryFlagValue, bool zeroFlagValue)
        {
            Reset();
            CreateCodeSegment(new byte[] { 0x76, 01 });
            
            if(carryFlagValue)
                mbbsEmuCpuRegisters.F.SetFlag(EnumFlags.CF);

            if(zeroFlagValue)
                mbbsEmuCpuRegisters.F.SetFlag(EnumFlags.ZF);

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Values
            Assert.Equal(3, mbbsEmuCpuRegisters.IP);

            //Verify Flags
            if (carryFlagValue)
            {
                Assert.True(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.CF));
            }
            else
            {
                Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.CF));
            }

            if (zeroFlagValue)
            {
                Assert.True(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.ZF));
            }
            else
            {
                Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.ZF));
            }

            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.OF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.SF));
        }
    }
}
