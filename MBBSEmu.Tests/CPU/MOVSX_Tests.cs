using Iced.Intel;
using Xunit;
using static Iced.Intel.AssemblerRegisters;

namespace MBBSEmu.Tests.CPU
{
    public class MOVSX_Tests : CpuTestBase
    {
        [Theory]
        [InlineData(0xC3EE, 0xFFFFC3EE)]
        [InlineData(0xFFFF, 0xFFFFFFFF)]
        [InlineData(0x0FFF, 0x00000FFF)]
        [InlineData(0xFFF4, 0xFFFFFFF4)]
        public void MOVSX_R32_M16(ushort esValue, uint expectedResult)
        {
            Reset();

            mbbsEmuMemoryCore.AddSegment(2);
            mbbsEmuCpuRegisters.DS = 2;
            mbbsEmuMemoryCore.SetWord(2, 0, esValue);

            var instructions = new Assembler(16);
            instructions.movsx(eax, __word_ptr[0]);
            CreateCodeSegment(instructions);

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Results
            Assert.Equal(expectedResult, mbbsEmuCpuRegisters.EAX);
        }

        [Theory]
        [InlineData(0xC3EE, 0xFFFFC3EE)]
        [InlineData(0xFFFF, 0xFFFFFFFF)]
        [InlineData(0x0FFF, 0x00000FFF)]
        [InlineData(0xFFF4, 0xFFFFFFF4)]
        public void MOVSX_R32_R16(ushort bxValue, uint expectedResult)
        {
            Reset();

            mbbsEmuCpuRegisters.BX = bxValue;

            var instructions = new Assembler(16);
            instructions.movsx(ebx, bx);
            CreateCodeSegment(instructions);

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Results
            Assert.Equal(expectedResult, mbbsEmuCpuRegisters.EBX);
        }

        [Theory]
        [InlineData(0xC3, 0xFFFFFFC3)]
        [InlineData(0xFF, 0xFFFFFFFF)]
        [InlineData(0x0F, 0x0000000F)]
        [InlineData(0xF4, 0xFFFFFFF4)]
        [InlineData(0x80, 0xFFFFFF80)]
        public void MOVSX_R32_M8(byte dsValue, uint expectedResult)
        {
            Reset();

            mbbsEmuMemoryCore.AddSegment(2);
            mbbsEmuCpuRegisters.DS = 2;
            mbbsEmuMemoryCore.SetByte(2, 0, dsValue);

            var instructions = new Assembler(16);
            instructions.movsx(eax, __byte_ptr[0]);
            CreateCodeSegment(instructions);

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Results
            Assert.Equal(expectedResult, mbbsEmuCpuRegisters.EAX);
        }

        [Theory]
        [InlineData(0xC3, 0xFFFFFFC3)]
        [InlineData(0xFF, 0xFFFFFFFF)]
        [InlineData(0x0F, 0x0000000F)]
        [InlineData(0xF4, 0xFFFFFFF4)]
        public void MOVSX_R32_R8(byte blValue, uint expectedResult)
        {
            Reset();

            mbbsEmuCpuRegisters.BL = blValue;

            var instructions = new Assembler(16);
            instructions.movsx(ebx, bl);
            CreateCodeSegment(instructions);

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Results
            Assert.Equal(expectedResult, mbbsEmuCpuRegisters.EBX);
        }

        [Theory]
        [InlineData(0x80, 0x0080)]
        [InlineData(0xFF, 0x00FF)]
        [InlineData(0x8C, 0x008C)]
        [InlineData(0xCC, 0x00CC)]
        public void MOVSX_R16_M8(byte dsValue, ushort expectedResult)
        {
            Reset();

            mbbsEmuMemoryCore.AddSegment(2);
            mbbsEmuCpuRegisters.DS = 2;
            mbbsEmuMemoryCore.SetByte(2, 0, dsValue);

            var instructions = new Assembler(16);
            instructions.movsx(dx, __byte_ptr[0]);
            CreateCodeSegment(instructions);

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Results
            Assert.Equal(expectedResult, mbbsEmuCpuRegisters.DX);
        }

        [Theory]
        [InlineData(0x80, 0x0080)]
        [InlineData(0xFF, 0x00FF)]
        [InlineData(0x8C, 0x008C)]
        [InlineData(0xCC, 0x00CC)]
        public void MOVSX_R16_R8(byte blValue, ushort expectedResult)
        {
            Reset();

            mbbsEmuCpuRegisters.BL = blValue;

            var instructions = new Assembler(16);
            instructions.movsx(dx, bl);
            CreateCodeSegment(instructions);

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Results
            Assert.Equal(expectedResult, mbbsEmuCpuRegisters.DX);
        }
    }
}
