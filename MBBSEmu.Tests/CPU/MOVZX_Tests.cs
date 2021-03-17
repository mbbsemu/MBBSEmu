using Iced.Intel;
using Xunit;
using static Iced.Intel.AssemblerRegisters;

namespace MBBSEmu.Tests.CPU
{
    public class MOVZX_Tests : CpuTestBase
    {
        [Theory]
        [InlineData(0xC3EE, 0x0000C3EE)]
        [InlineData(0xFFFF, 0x0000FFFF)]
        [InlineData(0x0FFF, 0x00000FFF)]
        [InlineData(0xFFF4, 0x0000FFF4)]
        public void MOVZX_R32_M16(ushort dsValue, uint expectedResult)
        {
            Reset();

            mbbsEmuProtectedModeMemoryCore.AddSegment(2);
            mbbsEmuCpuRegisters.DS = 2;
            mbbsEmuMemoryCore.SetWord(2, 0, dsValue);

            var instructions = new Assembler(16);
            instructions.movzx(eax, __word_ptr[0]);
            CreateCodeSegment(instructions);

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Results
            Assert.Equal(expectedResult, mbbsEmuCpuRegisters.EAX);
        }

        [Theory]
        [InlineData(0xC3EE, 0x0000C3EE)]
        [InlineData(0xFFFF, 0x0000FFFF)]
        [InlineData(0x0FFF, 0x00000FFF)]
        [InlineData(0xFFF4, 0x0000FFF4)]
        public void MOVZX_R32_R16(ushort bxValue, uint expectedResult)
        {
            Reset();

            mbbsEmuCpuRegisters.BX = bxValue;

            var instructions = new Assembler(16);
            instructions.movzx(ebx, bx);
            CreateCodeSegment(instructions);

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Results
            Assert.Equal(expectedResult, mbbsEmuCpuRegisters.EBX);
        }

        [Theory]
        [InlineData(0xC3, 0x000000C3)]
        [InlineData(0xFF, 0x000000FF)]
        [InlineData(0x0F, 0x0000000F)]
        [InlineData(0xF4, 0x000000F4)]
        public void MOVZX_R32_M8(byte dsValue, uint expectedResult)
        {
            Reset();

            mbbsEmuProtectedModeMemoryCore.AddSegment(2);
            mbbsEmuCpuRegisters.DS = 2;
            mbbsEmuMemoryCore.SetByte(2, 0, dsValue);

            var instructions = new Assembler(16);
            instructions.movzx(eax, __byte_ptr[0]);
            CreateCodeSegment(instructions);

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Results
            Assert.Equal(expectedResult, mbbsEmuCpuRegisters.EAX);
        }

        [Theory]
        [InlineData(0xC3, 0x000000C3)]
        [InlineData(0xFF, 0x000000FF)]
        [InlineData(0x0F, 0x0000000F)]
        [InlineData(0xF4, 0x000000F4)]
        public void MOVZX_R32_R8(byte blValue, uint expectedResult)
        {
            Reset();

            mbbsEmuCpuRegisters.BL = blValue;

            var instructions = new Assembler(16);
            instructions.movzx(ebx, bl);
            CreateCodeSegment(instructions);

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Results
            Assert.Equal(expectedResult, mbbsEmuCpuRegisters.EBX);
        }

        [Theory]
        [InlineData(0xC3, 0x00C3)]
        [InlineData(0xFF, 0x00FF)]
        [InlineData(0x0F, 0x000F)]
        [InlineData(0xF4, 0x00F4)]
        public void MOVZX_R16_M8(byte dsValue, ushort expectedResult)
        {
            Reset();

            mbbsEmuProtectedModeMemoryCore.AddSegment(2);
            mbbsEmuCpuRegisters.DS = 2;
            mbbsEmuMemoryCore.SetByte(2, 0, dsValue);

            var instructions = new Assembler(16);
            instructions.movzx(bx, __byte_ptr[0]);
            CreateCodeSegment(instructions);

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Results
            Assert.Equal(expectedResult, mbbsEmuCpuRegisters.BX);
        }

        [Theory]
        [InlineData(0x40, 0x0040)]
        [InlineData(0xF8, 0x00F8)]
        [InlineData(0x0F, 0x000F)]
        [InlineData(0xF4, 0x00F4)]
        public void MOVZX_R16_R8(byte blValue, ushort expectedResult)
        {
            Reset();

            mbbsEmuCpuRegisters.BL = blValue;

            var instructions = new Assembler(16);
            instructions.movzx(bx, bl);
            CreateCodeSegment(instructions);

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Results
            Assert.Equal(expectedResult, mbbsEmuCpuRegisters.BX);
        }
    }
}
