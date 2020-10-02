using Iced.Intel;
using System;
using Xunit;
using static Iced.Intel.AssemblerRegisters;

namespace MBBSEmu.Tests.CPU
{
    public class FSUBR_Tests : CpuTestBase
    {
        [Theory]
        [InlineData(1f, .5f)]
        [InlineData(10f, 5f)]
        [InlineData(10.1f, .1f)]
        [InlineData(1.5f, 2.0f)]
        [InlineData(0f, 0f)]
        public void FSUBR_Test_M32(float value1, float value2)
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
            instructions.fsubr(__dword_ptr[0]);
            CreateCodeSegment(instructions);

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            var result = mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackTop()];
            var expectedResult = value2 - value1;

            Assert.Equal(expectedResult, (float)result);
        }

        [Theory]
        [InlineData(1d, .5d)]
        [InlineData(10d, 5d)]
        [InlineData(10.1d, .1d)]
        [InlineData(1.5d, 2.0d)]
        [InlineData(0d, 0d)]
        public void FSUBR_Test_M64(double value1, double value2)
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
            instructions.fsubr(__qword_ptr[0]);
            CreateCodeSegment(instructions);

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            var result = mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackTop()];
            var expectedResult = value2 - value1;

            Assert.Equal(expectedResult, result);
        }
    }
}
