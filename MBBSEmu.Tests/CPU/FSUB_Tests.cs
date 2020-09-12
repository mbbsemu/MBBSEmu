using System;
using Xunit;

namespace MBBSEmu.Tests.CPU
{
    public class FSUB_Tests : CpuTestBase
    {
        [Theory]
        [InlineData(1f,.5f, .5f)]
        [InlineData(10f, 5f, 5f)]
        [InlineData(10.1f, .1f, 10f)]
        public void FSUB_Test_M32(float value1, float value2, float expectedResult)
        {
            //Reset the CPU
            Reset();

            //Load Value1 into the x87 Stack
            mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackTop()] = value1;
            mbbsEmuCpuRegisters.Fpu.PushStackTop();

            //Load Value2 into Memory & Setup DS
            CreateDataSegment(BitConverter.GetBytes(value2));
            mbbsEmuCpuRegisters.DS = 2;

            //Setup CPU & CODE Segment
            CreateCodeSegment(new byte[] { 0x3E, 0xD8, 0x26, 0x00, 0x00 });
            
            //Process Instruction
            mbbsEmuCpuCore.Tick();

            mbbsEmuCpuRegisters.Fpu.PopStackTop();
            var result = mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackTop()];

            Assert.Equal(expectedResult, result);
        }
    }
}