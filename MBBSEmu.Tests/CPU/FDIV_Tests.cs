using Iced.Intel;
using System;
using Xunit;
using static Iced.Intel.AssemblerRegisters;

namespace MBBSEmu.Tests.CPU
{
    public class FDIV_Tests : CpuTestBase
    {
        [Theory]
        [InlineData(2, 2)]
        [InlineData(1, .5)]
        [InlineData(0, 0)]
        public void FDIV_Test(float initialST0Value, float valueToDivide)
        {
            Reset();

            CreateDataSegment(new ReadOnlySpan<byte>(), 2);
            mbbsEmuMemoryCore.SetArray(2, 0, BitConverter.GetBytes(valueToDivide));
            mbbsEmuCpuRegisters.DS = 2;
            mbbsEmuCpuRegisters.Fpu.SetStackTop(0);
            mbbsEmuCpuCore.FpuStack[0] = initialST0Value;

            var instructions = new Assembler(16);
            instructions.fdiv(__dword_ptr[0]);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            var expectedValue = unchecked(initialST0Value / valueToDivide);

            Assert.Equal(expectedValue, mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackTop()]);
        }
    }
}
