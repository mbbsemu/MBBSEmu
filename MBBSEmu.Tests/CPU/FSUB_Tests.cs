using Iced.Intel;
using MBBSEmu.CPU;
using System;
using Xunit;
using static Iced.Intel.AssemblerRegisters;

namespace MBBSEmu.Tests.CPU
{
    public class FSUB_Tests : CpuTestBase
    {
        [Theory]
        [InlineData(1f, .5f, .5f)]
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

            Assert.Equal(expectedResult, (float)result);
        }

        [Theory]
        [InlineData(1d, .5d, (ushort)0x3FFE, 0x8000000000000000UL)]
        [InlineData(10d, 5d, (ushort)0x4001, 0xA000000000000000UL)]
        [InlineData(10.1d, .1d, (ushort)0x4002, 0x9FFFFFFFFFFFFE60UL)] //Extended precision: 10.1 - 0.1 is not 10 (coprocessor)
        public void FSUB_Test_M64(double value1, double value2, ushort expectedSignExponent, ulong expectedSignificand)
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
            instructions.fsub(__qword_ptr[0]);
            CreateCodeSegment(instructions);

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            var result = mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackTop()];

            Assert.Equal(new Float80(expectedSignExponent, expectedSignificand), result);
        }
    }
}