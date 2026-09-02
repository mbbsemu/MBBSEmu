using Iced.Intel;
using Xunit;

namespace MBBSEmu.Tests.CPU
{
    public class FDECSTP_Tests : CpuTestBase
    {
        [Fact]
        public void FDECSTP_Test()
        {
            Reset();

            mbbsEmuCpuRegisters.Fpu.SetStackTop(1);
            mbbsEmuCpuCore.FpuStack[1] = 111d; //ST0
            mbbsEmuCpuCore.FpuStack[0] = 222d; //ST1
            mbbsEmuCpuCore.FpuStack[2] = 333d;

            var instructions = new Assembler(16);
            instructions.fdecstp();
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            //a new ST(0) slot is created, old ST(0) is now ST(1)
            Assert.Equal(333d, mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackTop()]);
            Assert.Equal(111d, mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackPointer(Register.ST1)]);
        }
    }
}
