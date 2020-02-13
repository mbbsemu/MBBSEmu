using System;
using Xunit;

namespace MBBSEmu.Tests.CPU
{
    public class FLDZ_Tests : CpuTestBase
    {
        [Fact]
        public void FLDZ_Test()
        {
            //Reset the CPU
            Reset();

            //Setup CPU & CODE Segment
            CreateCodeSegment(new byte[] { 0xD9, 0xEE });

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            mbbsEmuCpuRegisters.Fpu.PopStackTop();
            var result = BitConverter.ToSingle(mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackTop()]);

            Assert.Equal(0.0f, result);
            Assert.Equal(7, mbbsEmuCpuRegisters.Fpu.GetStackTop());
        }
    }
}
