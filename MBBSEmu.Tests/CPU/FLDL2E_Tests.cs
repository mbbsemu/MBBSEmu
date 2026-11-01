using Iced.Intel;
using MBBSEmu.CPU;
using Xunit;

namespace MBBSEmu.Tests.CPU
{
    public class FLDL2E_Tests : CpuTestBase
    {
        [Fact]
        public void FLDL2E_Test()
        {
            Reset();

            var instructions = new Assembler(16);
            instructions.fldl2e();
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            //log2(e) to 64 bits, rounded to nearest (the coprocessor's FLDL2E under the default control word)
            Assert.Equal(new Float80(0x3FFF, 0xB8AA3B295C17F0BC), mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackTop()]);
        }
    }
}
