using Iced.Intel;
using Xunit;

namespace MBBSEmu.Tests.CPU
{
    public class CWD_Tests : CpuTestBase
    {
        [Theory]
        [InlineData(0x1, 0x0)]
        [InlineData(0x8000, 0xFFFF)]
        public void CWD_ClearFlags(ushort axValue, ushort dxValue)
        {
            Reset();
            mbbsEmuCpuRegisters.AX = axValue;
            var instructions = new Assembler(16);
            instructions.cwd();
            CreateCodeSegment(instructions);

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Results
            Assert.Equal(axValue, mbbsEmuCpuRegisters.AX);
            Assert.Equal(dxValue, mbbsEmuCpuRegisters.DX);

            //Verify Flags
            Assert.False(mbbsEmuCpuRegisters.CarryFlag);
            Assert.False(mbbsEmuCpuRegisters.ZeroFlag);
            Assert.False(mbbsEmuCpuRegisters.OverflowFlag);
            Assert.False(mbbsEmuCpuRegisters.SignFlag);
        }
    }
}
