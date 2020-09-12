using Iced.Intel;
using System;
using Xunit;
using static Iced.Intel.AssemblerRegisters;

namespace MBBSEmu.Tests.CPU
{
    public class FADD_Tests : CpuTestBase
    {
        [Theory]
        [InlineData(0.5, 1.5)]
        [InlineData(float.MaxValue, 1)]
        [InlineData(float.MinValue, float.MaxValue)]
        [InlineData(0, 0)]
        public void FADD_Test(float initialST0Value, float valueToAdd)
        {
            Reset();

            CreateDataSegment(new ReadOnlySpan<byte>(), 2);
            mbbsEmuMemoryCore.SetArray(2, 0, BitConverter.GetBytes(valueToAdd));
            mbbsEmuCpuRegisters.DS = 2;
            mbbsEmuCpuRegisters.Fpu.SetStackTop(0);
            mbbsEmuCpuCore.FpuStack[0] = initialST0Value;

            var instructions = new Assembler(16);
            instructions.fadd(__dword_ptr[0]);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            var expectedValue = unchecked(initialST0Value + valueToAdd);

            Assert.Equal(expectedValue, mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackTop()]);
        }
    }
}
