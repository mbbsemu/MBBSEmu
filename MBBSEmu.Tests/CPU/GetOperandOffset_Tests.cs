using Iced.Intel;
using System;
using Xunit;
using static Iced.Intel.AssemblerRegisters;

namespace MBBSEmu.Tests.CPU
{
    public class GetOperandOffset_Tests : CpuTestBase
    {

        [Theory]
        [InlineData(0, 1, 1, 0, 1)]
        [InlineData(1, 1, 2, 0, 0)]
        [InlineData(0xFFFF, 1, 0, 1, 0)]
        [InlineData(0xFFFE, 1, 0xFFFF, 1, 1)]
        public void GetOperandOffset_ADD_BX_DI(ushort memoryInitialValue, ushort valueToAdd, ushort expectedValue, ushort initialBX, ushort initialDI)
        {
            Reset();
            CreateDataSegment(new ReadOnlySpan<byte>(), 2);
            mbbsEmuMemoryCore.SetWord(2, (ushort)(initialDI + initialBX), memoryInitialValue);
            mbbsEmuCpuCore.Registers.DS = 2;
            mbbsEmuCpuCore.Registers.BX = initialBX;
            mbbsEmuCpuCore.Registers.DI = initialDI;

            var instructions = new Assembler(16);
            instructions.add(__word_ptr[bx + di], valueToAdd);

            CreateCodeSegment(instructions.Instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(mbbsEmuMemoryCore.GetWord(2, (ushort)(initialDI + initialBX)), expectedValue);
        }
    }
}
