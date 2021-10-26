using Iced.Intel;
using Xunit;

namespace MBBSEmu.Tests.CPU
{
    public class CDQ_Tests :CpuTestBase
    {
        [Theory]
        [InlineData(0x7FFFFFFF, 0x0)]
        [InlineData(0x80000000, 0xFFFFFFFF)]
        public void CDQ_ClearFlags(uint eaxValue, uint edxValue)
        {
            Reset();
            mbbsEmuCpuRegisters.EAX = eaxValue;
            var instructions = new Assembler(16);
            instructions.cdq();
            CreateCodeSegment(instructions);

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Results
            Assert.Equal(eaxValue, mbbsEmuCpuRegisters.EAX);
            Assert.Equal(edxValue, mbbsEmuCpuRegisters.EDX);

            //Verify Flags
            Assert.False(mbbsEmuCpuRegisters.CarryFlag);
            Assert.False(mbbsEmuCpuRegisters.ZeroFlag);
            Assert.False(mbbsEmuCpuRegisters.OverflowFlag);
            Assert.False(mbbsEmuCpuRegisters.SignFlag);
        }
    }
}
