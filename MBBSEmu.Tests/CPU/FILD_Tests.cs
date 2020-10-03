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
        [InlineData(ushort.MaxValue)]
        [InlineData(ushort.MinValue)]
        public void FILD_Test_M16(ushort valueToLoad)
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
        [InlineData(ushort.MaxValue, ushort.MinValue)]
        public void FILD_Multiple_Test_M16(ushort st0, ushort st1)
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
        [InlineData(uint.MaxValue)]
        [InlineData(uint.MinValue)]
        public void FILD_Test_M32(uint valueToLoad)
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
        [InlineData(uint.MaxValue, uint.MinValue)]
        public void FILD_Multiple_Test_M32(uint st0, uint st1)
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
        [InlineData(ulong.MaxValue)]
        [InlineData(ulong.MinValue)]
        public void FILD_Test_M64(ulong valueToLoad)
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
        [InlineData(ulong.MaxValue, ulong.MinValue)]
        public void FILD_Multiple_Test_M64(ulong st0, ulong st1)
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
