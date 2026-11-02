using Iced.Intel;
using Xunit;

namespace MBBSEmu.Tests.CPU
{
    public class FNINIT_Tests : CpuTestBase
    {
        [Fact]
        public void FNINIT_Test()
        {
            Reset();
            mbbsEmuCpuRegisters.Fpu.StatusWord = 0x3F; //Set all 6 Error Flags
            mbbsEmuCpuRegisters.Fpu.ControlWord = 0;
            mbbsEmuCpuRegisters.Fpu.SetStackTop(3);

            var instructions = new Assembler(16);
            instructions.fninit();
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(0x037F, mbbsEmuCpuRegisters.Fpu.ControlWord);
            Assert.Equal(0, mbbsEmuCpuRegisters.Fpu.StatusWord);
            Assert.Equal(0, mbbsEmuCpuRegisters.Fpu.GetStackTop());
        }
    }
}
