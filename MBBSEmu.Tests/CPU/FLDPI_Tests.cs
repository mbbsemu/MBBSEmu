using System;
using Iced.Intel;
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

            Assert.Equal(Math.PI, mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackTop()]);
        }
    }
}
