using Iced.Intel;
using MBBSEmu.CPU;
using Xunit;

namespace MBBSEmu.Tests.CPU
{
    public class FLDLN2_Tests : CpuTestBase
    {
        [Fact]
        public void FLDLN2_Test()
        {
            Reset();

            var instructions = new Assembler(16);
            instructions.fldln2();
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            //ln(2) to 64 bits, rounded to nearest (the coprocessor's FLDLN2 under the default control word)
            Assert.Equal(new Float80(0x3FFE, 0xB17217F7D1CF79AC), mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackTop()]);
        }
    }
}
