using Iced.Intel;
using MBBSEmu.CPU;
using Xunit;

namespace MBBSEmu.Tests.CPU
{
    public class FLDPI_Tests : CpuTestBase
    {
        [Fact]
        public void FLDPI_Test()
        {
            Reset();

            var instructions = new Assembler(16);
            instructions.fldpi();
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            //pi to 64 bits, rounded to nearest (the coprocessor's FLDPI under the default control word)
            Assert.Equal(new Float80(0x4000, 0xC90FDAA22168C235), mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackTop()]);
        }
    }
}
