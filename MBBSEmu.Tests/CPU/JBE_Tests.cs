using MBBSEmu.CPU;
using MBBSEmu.Extensions;
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
                mbbsEmuCpuRegisters.F = mbbsEmuCpuRegisters.F.SetFlag((ushort)EnumFlags.CF);

            if(zeroFlagValue)
                mbbsEmuCpuRegisters.F = mbbsEmuCpuRegisters.F.SetFlag((ushort)EnumFlags.ZF);

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Values
            Assert.Equal(3, mbbsEmuCpuRegisters.IP);

            //Verify Flags
            if (carryFlagValue)
            {
                Assert.True(mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.CF));
            }
            else
            {
                Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.CF));
            }

            if (zeroFlagValue)
            {
                Assert.True(mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.ZF));
            }
            else
            {
                Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.ZF));
            }

            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.OF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.SF));
        }
    }
}
