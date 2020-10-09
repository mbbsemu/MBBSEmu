using System;
using Iced.Intel;
using Xunit;
using static Iced.Intel.AssemblerRegisters;

namespace MBBSEmu.Tests.CPU
{
    public class FISTP_Tests : CpuTestBase
    {
        private const ushort FPU_CONTROLWORD_EXCEPTION_MASK = 0x3F;

        [Theory]
        [InlineData((double)(short.MaxValue), short.MaxValue, 0)]
        [InlineData((double)(short.MaxValue + 1), 0, 1)]
        [InlineData((double)(short.MinValue), short.MinValue, 0)]
        [InlineData((double)(short.MinValue - 1), 0, 1)]
        [InlineData(-0.0d, 0, 0)]
        [InlineData(double.NaN, 0, 1)]
        public void FISTP_Test_M16(double ST0Value, short expectedvalue, ushort expectedControlWord)
        {
            Reset();

            mbbsEmuCpuRegisters.DS = 2;
            mbbsEmuMemoryCore.AddSegment(2);
            mbbsEmuCpuRegisters.Fpu.SetStackTop(1);
            mbbsEmuCpuRegisters.Fpu.ControlWord = 0;
            mbbsEmuCpuCore.FpuStack[1] = ST0Value;

            var instructions = new Assembler(16);
            instructions.fistp(__word_ptr[0]);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(expectedvalue, (short)mbbsEmuMemoryCore.GetWord(2, 0));
            Assert.Equal(0, mbbsEmuCpuRegisters.Fpu.GetStackTop());
            Assert.Equal(expectedControlWord, mbbsEmuCpuRegisters.Fpu.ControlWord & FPU_CONTROLWORD_EXCEPTION_MASK);
        }

        [Theory]
        [InlineData((double)(int.MaxValue), int.MaxValue, 0)]
        [InlineData((double)(int.MinValue), int.MinValue, 0)]
        [InlineData(-0.0d, 0, 0)]
        [InlineData(double.NaN, 0, 1)]
        public void FISTP_Test_M32(double ST0Value, int expectedvalue, ushort expectedControlWord)
        {
            Reset();

            mbbsEmuCpuRegisters.DS = 2;
            mbbsEmuMemoryCore.AddSegment(2);
            mbbsEmuCpuRegisters.Fpu.SetStackTop(1);
            mbbsEmuCpuRegisters.Fpu.ControlWord = 0;
            mbbsEmuCpuCore.FpuStack[1] = ST0Value;

            var instructions = new Assembler(16);
            instructions.fistp(__dword_ptr[0]);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(expectedvalue, BitConverter.ToInt32(mbbsEmuMemoryCore.GetArray(2, 0, 4)));
            Assert.Equal(0, mbbsEmuCpuRegisters.Fpu.GetStackTop());
            Assert.Equal(expectedControlWord, mbbsEmuCpuRegisters.Fpu.ControlWord & FPU_CONTROLWORD_EXCEPTION_MASK);
        }

        [Theory]
        //[InlineData((double)(long.MaxValue), long.MaxValue, 0)] //Overflows -- need to figure out why
        [InlineData((double)(long.MinValue), long.MinValue, 0)]
        [InlineData(-0.0d, 0, 0)]
        [InlineData(double.NaN, 0, 1)]
        public void FISTP_Test_M64(double ST0Value, long expectedvalue, ushort expectedControlWord)
        {
            Reset();

            mbbsEmuCpuRegisters.DS = 2;
            mbbsEmuMemoryCore.AddSegment(2);
            mbbsEmuCpuRegisters.Fpu.SetStackTop(1);
            mbbsEmuCpuRegisters.Fpu.ControlWord = 0;
            mbbsEmuCpuCore.FpuStack[1] = ST0Value;

            var instructions = new Assembler(16);
            instructions.fistp(__qword_ptr[0]);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(expectedvalue, BitConverter.ToInt64(mbbsEmuMemoryCore.GetArray(2, 0, 8)));
            Assert.Equal(0, mbbsEmuCpuRegisters.Fpu.GetStackTop());
            Assert.Equal(expectedControlWord, mbbsEmuCpuRegisters.Fpu.ControlWord & FPU_CONTROLWORD_EXCEPTION_MASK);
        }
    }
}
