using Iced.Intel;
using System;
using Xunit;
using static Iced.Intel.AssemblerRegisters;

namespace MBBSEmu.Tests.CPU
{
    public class FMUL_Tests : CpuTestBase
    {
        [Theory]
        [InlineData(2, .5)]
        [InlineData(1, 0)]
        [InlineData(10, 2.5)]
        [InlineData(0, 0)]
        public void FMUL_Test(float initialST0Value, float valueToMultiply)
        {
            Reset();

            CreateDataSegment(new ReadOnlySpan<byte>(), 2);
            mbbsEmuMemoryCore.SetArray(2, 0, BitConverter.GetBytes(valueToMultiply));
            mbbsEmuCpuRegisters.DS = 2;
            mbbsEmuCpuRegisters.Fpu.SetStackTop(0);
            mbbsEmuCpuCore.FpuStack[0] = initialST0Value;

            var instructions = new Assembler(16);
            instructions.fmul(__dword_ptr[0]);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            var expectedValue = initialST0Value * valueToMultiply;

            Assert.Equal(expectedValue, mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackTop()]);
        }
    }
}
