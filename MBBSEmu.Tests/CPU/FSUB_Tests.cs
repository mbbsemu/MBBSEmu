using System;
using Iced.Intel;
using Xunit;
using static Iced.Intel.AssemblerRegisters;

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
            mbbsEmuCpuRegisters.Fpu.SetStackTop(0);
            mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackTop()] = value1;

            //Load Value2 into Memory & Setup DS
            CreateDataSegment(BitConverter.GetBytes(value2));
            mbbsEmuCpuRegisters.DS = 2;

            //Setup CPU & CODE Segment
            var instructions = new Assembler(16);
            instructions.fsub(__dword_ptr[0]);
            CreateCodeSegment(instructions);

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            var result = mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackTop()];

            Assert.Equal(expectedResult, result);
        }
    }
}