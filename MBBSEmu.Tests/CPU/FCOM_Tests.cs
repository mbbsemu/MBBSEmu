using System;
using Iced.Intel;
using MBBSEmu.CPU;
using Xunit;
using static Iced.Intel.AssemblerRegisters;

namespace MBBSEmu.Tests.CPU
{
    public class FCOM_Tests : CpuTestBase
    {
        private readonly ushort FPU_CODE_MASK = (ushort)(EnumFpuStatusFlags.Code0 | EnumFpuStatusFlags.Code1 |
                                                         EnumFpuStatusFlags.Code2 | EnumFpuStatusFlags.Code3);

        [Theory]
        [InlineData(double.MaxValue, 0d, 0)]
        [InlineData(double.MinValue, 0d, (ushort)EnumFpuStatusFlags.Code0)]
        [InlineData(1d, 1d, (ushort)EnumFpuStatusFlags.Code3)]
        [InlineData(double.NaN, 0d, (ushort)(EnumFpuStatusFlags.Code0 | EnumFpuStatusFlags.Code2 | EnumFpuStatusFlags.Code3))]
        [InlineData(0d, double.NaN, (ushort)(EnumFpuStatusFlags.Code0 | EnumFpuStatusFlags.Code2 | EnumFpuStatusFlags.Code3))]
        public void FCOM_ST1_Test(double ST0Value, double ST1Value, ushort expectedFlags)
        {
            Reset();
            mbbsEmuCpuRegisters.Fpu.StatusWord = 0; //Manually Clear all Status Values
            mbbsEmuCpuRegisters.Fpu.SetStackTop(1);
            mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackPointer(Register.ST0)] = ST0Value;
            mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackPointer(Register.ST1)] = ST1Value;

            var instructions = new Assembler(16);
            instructions.fcom(st0, st1);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            var actualFPUCodes = mbbsEmuCpuRegisters.Fpu.StatusWord & FPU_CODE_MASK;

            Assert.Equal(expectedFlags, actualFPUCodes);
            Assert.Equal(1, mbbsEmuCpuRegisters.Fpu.GetStackTop());
        }

        [Theory]
        [InlineData(float.MaxValue, 0d, 0)]
        [InlineData(float.MinValue, 0d, (ushort)EnumFpuStatusFlags.Code0)]
        [InlineData(1d, 1d, (ushort)EnumFpuStatusFlags.Code3)]
        [InlineData(float.NaN, 0d, (ushort)(EnumFpuStatusFlags.Code0 | EnumFpuStatusFlags.Code2 | EnumFpuStatusFlags.Code3))]
        [InlineData(0d, float.NaN, (ushort)(EnumFpuStatusFlags.Code0 | EnumFpuStatusFlags.Code2 | EnumFpuStatusFlags.Code3))]
        public void FCOM_M32_Test(double ST0Value, float m32Value, ushort expectedFlags)
        {
            Reset();
            mbbsEmuMemoryCore.AddSegment(2);
            mbbsEmuMemoryCore.SetArray(2, 0, BitConverter.GetBytes(m32Value));
            mbbsEmuCpuRegisters.DS = 2;
            mbbsEmuCpuRegisters.Fpu.StatusWord = 0; //Manually Clear all Status Values
            mbbsEmuCpuRegisters.Fpu.SetStackTop(0);
            mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackPointer(Register.ST0)] = ST0Value;

            var instructions = new Assembler(16);
            instructions.fcom(__dword_ptr[0]);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            var actualFPUCodes = mbbsEmuCpuRegisters.Fpu.StatusWord & FPU_CODE_MASK;

            Assert.Equal(expectedFlags, actualFPUCodes);
            Assert.Equal(0, mbbsEmuCpuRegisters.Fpu.GetStackTop());
        }

        [Theory]
        [InlineData(double.MaxValue, 0d, 0)]
        [InlineData(double.MinValue, 0d, (ushort)EnumFpuStatusFlags.Code0)]
        [InlineData(1d, 1d, (ushort)EnumFpuStatusFlags.Code3)]
        [InlineData(double.NaN, 0d, (ushort)(EnumFpuStatusFlags.Code0 | EnumFpuStatusFlags.Code2 | EnumFpuStatusFlags.Code3))]
        [InlineData(0d, double.NaN, (ushort)(EnumFpuStatusFlags.Code0 | EnumFpuStatusFlags.Code2 | EnumFpuStatusFlags.Code3))]
        public void FCOM_M64_Test(double ST0Value, double m32Value, ushort expectedFlags)
        {
            Reset();
            mbbsEmuMemoryCore.AddSegment(2);
            mbbsEmuMemoryCore.SetArray(2, 0, BitConverter.GetBytes(m32Value));
            mbbsEmuCpuRegisters.DS = 2;
            mbbsEmuCpuRegisters.Fpu.StatusWord = 0; //Manually Clear all Status Values
            mbbsEmuCpuRegisters.Fpu.SetStackTop(0);
            mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackPointer(Register.ST0)] = ST0Value;

            var instructions = new Assembler(16);
            instructions.fcom(__qword_ptr[0]);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            var actualFPUCodes = mbbsEmuCpuRegisters.Fpu.StatusWord & FPU_CODE_MASK;

            Assert.Equal(expectedFlags, actualFPUCodes);
            Assert.Equal(0, mbbsEmuCpuRegisters.Fpu.GetStackTop());
        }
    }
}