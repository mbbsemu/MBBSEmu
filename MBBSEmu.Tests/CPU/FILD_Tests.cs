using Iced.Intel;
using System;
using Xunit;
using static Iced.Intel.AssemblerRegisters;

namespace MBBSEmu.Tests.CPU
{
    public class FILD_Tests : CpuTestBase
    {
        [Theory]
        [InlineData(0)]
        [InlineData(short.MaxValue)]
        [InlineData(short.MinValue)]
        public void FILD_Test_M16(short valueToLoad)
        {
            Reset();

            CreateDataSegment(new ReadOnlySpan<byte>(), 2);
            mbbsEmuMemoryCore.SetArray(2, 0, BitConverter.GetBytes(valueToLoad));
            mbbsEmuCpuRegisters.DS = 2;

            var instructions = new Assembler(16);
            instructions.fild(__word_ptr[0]);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(valueToLoad, mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackTop()]);
        }

        [Theory]
        [InlineData(1, 2)]
        [InlineData(short.MaxValue, short.MinValue)]
        public void FILD_Multiple_Test_M16(short st0, short st1)
        {
            Reset();

            CreateDataSegment(new ReadOnlySpan<byte>(), 2);
            mbbsEmuMemoryCore.SetArray(2, 0, BitConverter.GetBytes(st1)); //ST(0)
            mbbsEmuMemoryCore.SetArray(2, 2, BitConverter.GetBytes(st0)); //ST(0)->ST(1), new ST(0)
            mbbsEmuCpuRegisters.DS = 2;

            var instructions = new Assembler(16);
            instructions.fild(__word_ptr[0]);
            instructions.fild(__word_ptr[2]);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();
            mbbsEmuCpuCore.Tick();

            Assert.Equal(st0, mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackTop()]);
            Assert.Equal(st1, mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackPointer(Register.ST1)]);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(int.MaxValue)]
        [InlineData(int.MinValue)]
        public void FILD_Test_M32(int valueToLoad)
        {
            Reset();

            CreateDataSegment(new ReadOnlySpan<byte>(), 2);
            mbbsEmuMemoryCore.SetArray(2, 0, BitConverter.GetBytes(valueToLoad));
            mbbsEmuCpuRegisters.DS = 2;

            var instructions = new Assembler(16);
            instructions.fild(__dword_ptr[0]);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(valueToLoad, mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackTop()]);
        }

        [Theory]
        [InlineData(1, 2)]
        [InlineData(int.MaxValue, int.MinValue)]
        public void FILD_Multiple_Test_M32(int st0, int st1)
        {
            Reset();

            CreateDataSegment(new ReadOnlySpan<byte>(), 2);
            mbbsEmuMemoryCore.SetArray(2, 0, BitConverter.GetBytes(st1)); //ST(0)
            mbbsEmuMemoryCore.SetArray(2, 4, BitConverter.GetBytes(st0)); //ST(0)->ST(1), new ST(0)
            mbbsEmuCpuRegisters.DS = 2;

            var instructions = new Assembler(16);
            instructions.fild(__dword_ptr[0]);
            instructions.fild(__dword_ptr[4]);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();
            mbbsEmuCpuCore.Tick();

            Assert.Equal(st0, mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackTop()]);
            Assert.Equal(st1, mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackPointer(Register.ST1)]);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(long.MaxValue)]
        [InlineData(long.MinValue)]
        public void FILD_Test_M64(long valueToLoad)
        {
            Reset();

            CreateDataSegment(new ReadOnlySpan<byte>(), 2);
            mbbsEmuMemoryCore.SetArray(2, 0, BitConverter.GetBytes(valueToLoad));
            mbbsEmuCpuRegisters.DS = 2;

            var instructions = new Assembler(16);
            instructions.fild(__qword_ptr[0]);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(valueToLoad, mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackTop()]);
        }

        [Theory]
        [InlineData(1, 2)]
        [InlineData(long.MaxValue, long.MinValue)]
        public void FILD_Multiple_Test_M64(long st0, long st1)
        {
            Reset();

            CreateDataSegment(new ReadOnlySpan<byte>(), 2);
            mbbsEmuMemoryCore.SetArray(2, 0, BitConverter.GetBytes(st1)); //ST(0)
            mbbsEmuMemoryCore.SetArray(2, 8, BitConverter.GetBytes(st0)); //ST(0)->ST(1), new ST(0)
            mbbsEmuCpuRegisters.DS = 2;

            var instructions = new Assembler(16);
            instructions.fild(__qword_ptr[0]);
            instructions.fild(__qword_ptr[8]);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();
            mbbsEmuCpuCore.Tick();

            Assert.Equal(st0, mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackTop()]);
            Assert.Equal(st1, mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackPointer(Register.ST1)]);
        }
    }
}
