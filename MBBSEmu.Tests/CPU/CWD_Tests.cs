using MBBSEmu.CPU;
using Xunit;

namespace MBBSEmu.Tests.CPU
{
    public class CWD_Tests :CpuTestBase
    {
        [Theory]
        [InlineData(0x1, 0x0)]
        [InlineData(0x8000, 0xFFFF)]
        public void IDIV_AX_BX_ClearFlags(ushort axValue, ushort dxValue)
        {
            Reset();
            mbbsEmuCpuRegisters.AX = axValue;
            CreateCodeSegment(new byte[] { 0x99 });

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Results
            Assert.Equal(axValue, mbbsEmuCpuRegisters.AX);
            Assert.Equal(dxValue, mbbsEmuCpuRegisters.DX);

            //Verify Flags
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.CF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.ZF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.OF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet(EnumFlags.SF));
        }
    }
}
