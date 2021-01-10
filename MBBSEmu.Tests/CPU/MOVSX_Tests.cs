using Iced.Intel;
using MBBSEmu.CPU;
using MBBSEmu.Extensions;
using Xunit;
using static Iced.Intel.AssemblerRegisters;

namespace MBBSEmu.Tests.CPU
{
    public class MOVSX_Tests : CpuTestBase
    {
        [Theory]
        [InlineData(0xFF, 0x00000000)]
        [InlineData(0xFF, 0xFFFFFFFF)]
        [InlineData(0x00, 0x00000000)]
        public void MOVSX_R32_M16(ushort esValue, uint expectedResult)
        {
            Reset();

            mbbsEmuMemoryCore.AddSegment(2);
            mbbsEmuCpuRegisters.ES = 2;
            mbbsEmuMemoryCore.SetWord(2, 0, esValue);

            var instructions = new Assembler(16);
            instructions.movsx(eax, __word_ptr[0]);
            CreateCodeSegment(instructions);

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Results
            Assert.Equal(expectedResult, mbbsEmuCpuRegisters.EAX);
        }
    }
}
