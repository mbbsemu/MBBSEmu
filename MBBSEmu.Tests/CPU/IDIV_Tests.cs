using MBBSEmu.CPU;
using Xunit;

namespace MBBSEmu.Tests.CPU
{
    public class IDIV_Tests : CpuTestBase
    {
        [Theory]
        [InlineData(0x4, 0xA, 0x0, 0x4)]
        [InlineData(0xA, 0xA, 0x1, 0x0)]
        public void IDIV_AX_BX_ClearFlags(ushort axValue, ushort bxValue, ushort result, ushort remainder)
        {
            Reset();
            mbbsEmuCpuRegisters.AX = axValue;
            mbbsEmuCpuRegisters.BX = bxValue;
            CreateCodeSegment(new byte[] { 0xF7, 0xFB });

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Results
            Assert.NotEqual(axValue, mbbsEmuCpuRegisters.AX);
            Assert.Equal(result, mbbsEmuCpuRegisters.AX);
            Assert.Equal(remainder, mbbsEmuCpuRegisters.DX);

            //Verify Flags
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.CF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.ZF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.OF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.SF));
        }
    }
}
