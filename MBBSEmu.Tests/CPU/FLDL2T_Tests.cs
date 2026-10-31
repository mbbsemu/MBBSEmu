using Iced.Intel;
using MBBSEmu.CPU;
using Xunit;

namespace MBBSEmu.Tests.CPU
{
    public class FLDL2T_Tests : CpuTestBase
    {
        [Fact]
        public void FLDL2T_Test()
        {
            Reset();

            var instructions = new Assembler(16);
            instructions.fldl2t();
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            //log2(10) to 64 bits, rounded to nearest (the coprocessor's FLDL2T under the default control word)
            Assert.Equal(new Float80(0x4000, 0xD49A784BCD1B8AFE), mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackTop()]);
        }
    }
}
