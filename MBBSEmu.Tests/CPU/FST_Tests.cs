using Iced.Intel;
using System;
using Xunit;
using static Iced.Intel.AssemblerRegisters;

namespace MBBSEmu.Tests.CPU
{
    public class FST_Tests : CpuTestBase
    {
        [Theory]
        [InlineData(double.MaxValue)]
        [InlineData(double.MinValue)]
        [InlineData(0.0d)]
        [InlineData(double.NaN)]
        public void FST_Test_ST1(double ST0Value)
        {
            Reset();
            mbbsEmuCpuRegisters.Fpu.SetStackTop(0);
            mbbsEmuCpuCore.FpuStack[0] = ST0Value;

            var instructions = new Assembler(16);
            instructions.fst(st0);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(ST0Value, mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackTop()]);
            Assert.Equal(0, mbbsEmuCpuRegisters.Fpu.GetStackTop());
        }

        [Theory]
        [InlineData(double.MaxValue)]
        [InlineData(double.MinValue)]
        [InlineData(0.0d)]
        [InlineData(double.NaN)]
        public void FST_Test_M32(double ST0Value)
        {
            Reset();
            CreateDataSegment(new ReadOnlySpan<byte>(), 2);
            mbbsEmuCpuRegisters.DS = 2;
            mbbsEmuCpuRegisters.Fpu.SetStackTop(0);
            mbbsEmuCpuCore.FpuStack[0] = ST0Value;

            var instructions = new Assembler(16);
            instructions.fst(__dword_ptr[0]);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal((float)ST0Value, BitConverter.ToSingle(mbbsEmuMemoryCore.GetArray(2, 0, 4)));
            Assert.Equal(0, mbbsEmuCpuRegisters.Fpu.GetStackTop());
        }

        [Theory]
        [InlineData(double.MaxValue)]
        [InlineData(double.MinValue)]
        [InlineData(0.0d)]
        [InlineData(double.NaN)]
        public void FST_Test_M64(double ST0Value)
        {
            Reset();
            CreateDataSegment(new ReadOnlySpan<byte>(), 2);
            mbbsEmuCpuRegisters.DS = 2;
            mbbsEmuCpuRegisters.Fpu.SetStackTop(0);
            mbbsEmuCpuCore.FpuStack[0] = ST0Value;

            var instructions = new Assembler(16);
            instructions.fst(__qword_ptr[0]);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(ST0Value, BitConverter.ToDouble(mbbsEmuMemoryCore.GetArray(2, 0, 8)));
            Assert.Equal(0, mbbsEmuCpuRegisters.Fpu.GetStackTop());
        }
    }
}
