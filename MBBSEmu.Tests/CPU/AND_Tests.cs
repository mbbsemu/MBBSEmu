using Iced.Intel;
using MBBSEmu.CPU;
using MBBSEmu.Extensions;
using Xunit;
using static Iced.Intel.AssemblerRegisters;

namespace MBBSEmu.Tests.CPU
{
    public class AND_Tests : CpuTestBase
    {
        [Theory]
        [InlineData(0xFF, 0x00, 0x00, true, false)]
        [InlineData(0xFF, 0xFF, 0xFF, false, true)]
        [InlineData(0x00, 0x00, 0x00, true, false)]
        public void AND_AL_IMM8(byte alValue, byte valueToAnd, byte expectedResult, bool zeroFlagSet, bool signFlagSet)
        {
            Reset();

            mbbsEmuCpuRegisters.AL = alValue;

            var instructions = new Assembler(16);
            instructions.and(al, valueToAnd);
            CreateCodeSegment(instructions);

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Results
            Assert.Equal(expectedResult, mbbsEmuCpuRegisters.AL);

            //Verify Flags
            Assert.Equal(zeroFlagSet, mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.ZF));
            Assert.Equal(signFlagSet, mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.SF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.OF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.CF));
        }

        [Theory]
        [InlineData(0xFFFF, 0x0000, 0x0000, true, false)]
        [InlineData(0xFFFF, 0xFFFF, 0xFFFF, false, true)]
        [InlineData(0x0000, 0x0000, 0x0000, true, false)]
        public void AND_AX_IMM16(ushort axValue, ushort valueToAnd, ushort expectedResult, bool zeroFlagSet, bool signFlagSet)
        {
            Reset();

            mbbsEmuCpuRegisters.AX = axValue;

            var instructions = new Assembler(16);
            instructions.and(ax, valueToAnd);
            CreateCodeSegment(instructions);

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Results
            Assert.Equal(expectedResult, mbbsEmuCpuRegisters.AX);

            //Verify Flags
            Assert.Equal(zeroFlagSet, mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.ZF));
            Assert.Equal(signFlagSet, mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.SF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.OF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.CF));
        }

        [Theory]
        [InlineData(0xFFFFFFFF, 0x00000000, 0x00000000, true, false)]
        [InlineData(0xFFFFFFFF, 0xFFFFFFFF, 0xFFFFFFFF, false, true)]
        [InlineData(0x00000000, 0x00000000, 0x00000000, true, false)]
        public void AND_EAX_IMM32(uint eaxValue, uint valueToAnd, uint expectedResult, bool zeroFlagSet, bool signFlagSet)
        {
            Reset();

            mbbsEmuCpuRegisters.EAX = eaxValue;

            var instructions = new Assembler(16);
            instructions.and(eax, valueToAnd);
            CreateCodeSegment(instructions);

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Results
            Assert.Equal(expectedResult, mbbsEmuCpuRegisters.EAX);

            //Verify Flags
            Assert.Equal(zeroFlagSet, mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.ZF));
            Assert.Equal(signFlagSet, mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.SF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.OF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.CF));
        }

        [Theory]
        [InlineData(0xFF, 0x00, 0x00, true, false)]
        [InlineData(0xFF, 0xFF, 0xFF, false, true)]
        [InlineData(0x00, 0x00, 0x00, true, false)]
        public void AND_R8_IMM8(byte blValue, byte valueToAnd, byte expectedResult, bool zeroFlagSet, bool signFlagSet)
        {
            Reset();

            mbbsEmuCpuRegisters.BL = blValue;

            var instructions = new Assembler(16);
            instructions.and(bl, valueToAnd);
            CreateCodeSegment(instructions);

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Results
            Assert.Equal(expectedResult, mbbsEmuCpuRegisters.BL);

            //Verify Flags
            Assert.Equal(zeroFlagSet, mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.ZF));
            Assert.Equal(signFlagSet, mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.SF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.OF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.CF));
        }

        [Theory]
        [InlineData(0xFFFF, 0x0000, 0x0000, true, false)]
        [InlineData(0xFFFF, 0xFFFF, 0xFFFF, false, true)]
        [InlineData(0x0000, 0x0000, 0x0000, true, false)]
        public void AND_R16_IMM16(ushort bxValue, ushort valueToAnd, ushort expectedResult, bool zeroFlagSet, bool signFlagSet)
        {
            Reset();

            mbbsEmuCpuRegisters.BX = bxValue;

            var instructions = new Assembler(16);
            instructions.and(bx, valueToAnd);
            CreateCodeSegment(instructions);

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Results
            Assert.Equal(expectedResult, mbbsEmuCpuRegisters.BX);

            //Verify Flags
            Assert.Equal(zeroFlagSet, mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.ZF));
            Assert.Equal(signFlagSet, mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.SF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.OF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.CF));
        }

        [Theory]
        [InlineData(0xFFFFFFFF, 0x00000000, 0x00000000, true, false)]
        [InlineData(0xFFFFFFFF, 0xFFFFFFFF, 0xFFFFFFFF, false, true)]
        [InlineData(0x00000000, 0x00000000, 0x00000000, true, false)]
        public void AND_R32_IMM32(uint ebxValue, uint valueToAnd, uint expectedResult, bool zeroFlagSet, bool signFlagSet)
        {
            Reset();

            mbbsEmuCpuRegisters.EBX = ebxValue;

            var instructions = new Assembler(16);
            instructions.and(ebx, valueToAnd);
            CreateCodeSegment(instructions);

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Results
            Assert.Equal(expectedResult, mbbsEmuCpuRegisters.EBX);

            //Verify Flags
            Assert.Equal(zeroFlagSet, mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.ZF));
            Assert.Equal(signFlagSet, mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.SF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.OF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.CF));
        }

        [Theory]
        [InlineData(0xFF, 0x00, 0x00, true, false)]
        [InlineData(0xFF, 0xFF, 0xFF, false, true)]
        [InlineData(0x00, 0x00, 0x00, true, false)]
        public void AND_M8_IMM8(byte memoryValue, byte valueToAnd, byte expectedResult, bool zeroFlagSet, bool signFlagSet)
        {
            Reset();

            mbbsEmuProtectedModeMemoryCore.AddSegment(2);
            mbbsEmuCpuRegisters.DS = 2;
            mbbsEmuMemoryCore.SetByte(2, 0, memoryValue);

            var instructions = new Assembler(16);
            instructions.and(__byte_ptr[0], valueToAnd);
            CreateCodeSegment(instructions);

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Results
            Assert.Equal(expectedResult, mbbsEmuMemoryCore.GetByte(2, 0));

            //Verify Flags
            Assert.Equal(zeroFlagSet, mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.ZF));
            Assert.Equal(signFlagSet, mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.SF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.OF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.CF));
        }

        [Theory]
        [InlineData(0xFFFF, 0x0000, 0x0000, true, false)]
        [InlineData(0xFFFF, 0xFFFF, 0xFFFF, false, true)]
        [InlineData(0x0000, 0x0000, 0x0000, true, false)]
        public void AND_M16_IMM16(ushort memoryValue, ushort valueToAnd, ushort expectedResult, bool zeroFlagSet, bool signFlagSet)
        {
            Reset();

            mbbsEmuProtectedModeMemoryCore.AddSegment(2);
            mbbsEmuCpuRegisters.DS = 2;
            mbbsEmuMemoryCore.SetWord(2, 0, memoryValue);

            var instructions = new Assembler(16);
            instructions.and(__word_ptr[0], valueToAnd);
            CreateCodeSegment(instructions);

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Results
            Assert.Equal(expectedResult, mbbsEmuMemoryCore.GetWord(2, 0));

            //Verify Flags
            Assert.Equal(zeroFlagSet, mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.ZF));
            Assert.Equal(signFlagSet, mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.SF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.OF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.CF));
        }

        [Theory]
        [InlineData(0xFFFFFFFF, 0x00000000, 0x00000000, true, false)]
        [InlineData(0xFFFFFFFF, 0xFFFFFFFF, 0xFFFFFFFF, false, true)]
        [InlineData(0x00000000, 0x00000000, 0x00000000, true, false)]
        public void AND_M32_IMM32(uint memoryValue, uint valueToAnd, uint expectedResult, bool zeroFlagSet, bool signFlagSet)
        {
            Reset();

            mbbsEmuProtectedModeMemoryCore.AddSegment(2);
            mbbsEmuCpuRegisters.DS = 2;
            mbbsEmuMemoryCore.SetDWord(2, 0, memoryValue);

            var instructions = new Assembler(16);
            instructions.and(__dword_ptr[0], valueToAnd);
            CreateCodeSegment(instructions);

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Results
            Assert.Equal(expectedResult, mbbsEmuMemoryCore.GetDWord(2, 0));

            //Verify Flags
            Assert.Equal(zeroFlagSet, mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.ZF));
            Assert.Equal(signFlagSet, mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.SF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.OF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.CF));
        }

        [Theory]
        [InlineData(0xFFFF, 0x00, 0x0000, true, false)]
        [InlineData(0xFFFF, 0xFF, 0xFF, false, false)]
        [InlineData(0x0000, 0x00, 0x0000, true, false)]
        public void AND_M16_IMM8(ushort memoryValue, byte valueToAnd, ushort expectedResult, bool zeroFlagSet, bool signFlagSet)
        {
            Reset();

            mbbsEmuProtectedModeMemoryCore.AddSegment(2);
            mbbsEmuCpuRegisters.DS = 2;
            mbbsEmuMemoryCore.SetWord(2, 0, memoryValue);

            var instructions = new Assembler(16);
            instructions.and(__word_ptr[0], valueToAnd);
            CreateCodeSegment(instructions);

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Results
            Assert.Equal(expectedResult, mbbsEmuMemoryCore.GetWord(2, 0));

            //Verify Flags
            Assert.Equal(zeroFlagSet, mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.ZF));
            Assert.Equal(signFlagSet, mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.SF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.OF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.CF));
        }

        [Theory]
        [InlineData(0xFFFFFFFF, 0x00, 0x00000000, true, false)]
        [InlineData(0xFFFFFFFF, 0xFF, 0xFF, false, false)]
        [InlineData(0x00000000, 0x00, 0x00000000, true, false)]
        public void AND_M32_IMM8(uint memoryValue, byte valueToAnd, uint expectedResult, bool zeroFlagSet, bool signFlagSet)
        {
            Reset();

            mbbsEmuProtectedModeMemoryCore.AddSegment(2);
            mbbsEmuCpuRegisters.DS = 2;
            mbbsEmuMemoryCore.SetDWord(2, 0, memoryValue);

            var instructions = new Assembler(16);
            instructions.and(__dword_ptr[0], valueToAnd);
            CreateCodeSegment(instructions);

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Results
            Assert.Equal(expectedResult, mbbsEmuMemoryCore.GetDWord(2, 0));

            //Verify Flags
            Assert.Equal(zeroFlagSet, mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.ZF));
            Assert.Equal(signFlagSet, mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.SF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.OF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.CF));
        }

        [Theory]
        [InlineData(0xFF, 0x00, 0x00, true, false)]
        [InlineData(0xFF, 0xFF, 0xFF, false, true)]
        [InlineData(0x00, 0x00, 0x00, true, false)]
        public void AND_R8_R8(byte blValue, byte dlValue, byte expectedResult, bool zeroFlagSet, bool signFlagSet)
        {
            Reset();

            mbbsEmuCpuRegisters.BL = blValue;
            mbbsEmuCpuRegisters.DL = dlValue;

            var instructions = new Assembler(16);
            instructions.and(bl, dl);
            CreateCodeSegment(instructions);

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Results
            Assert.Equal(expectedResult, mbbsEmuCpuRegisters.BL);

            //Verify Flags
            Assert.Equal(zeroFlagSet, mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.ZF));
            Assert.Equal(signFlagSet, mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.SF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.OF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.CF));
        }

        [Theory]
        [InlineData(0xFFFF, 0x0000, 0x0000, true, false)]
        [InlineData(0xFFFF, 0xFFFF, 0xFFFF, false, true)]
        [InlineData(0x0000, 0x0000, 0x0000, true, false)]
        public void AND_R16_R16(ushort bxValue, ushort dxValue, ushort expectedResult, bool zeroFlagSet, bool signFlagSet)
        {
            Reset();

            mbbsEmuCpuRegisters.BX = bxValue;
            mbbsEmuCpuRegisters.DX = dxValue;

            var instructions = new Assembler(16);
            instructions.and(bx, dx);
            CreateCodeSegment(instructions);

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Results
            Assert.Equal(expectedResult, mbbsEmuCpuRegisters.BX);

            //Verify Flags
            Assert.Equal(zeroFlagSet, mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.ZF));
            Assert.Equal(signFlagSet, mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.SF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.OF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.CF));
        }

        [Theory]
        [InlineData(0xFFFFFFFF, 0x00000000, 0x00000000, true, false)]
        [InlineData(0xFFFFFFFF, 0xFFFFFFFF, 0xFFFFFFFF, false, true)]
        [InlineData(0x00000000, 0x00000000, 0x00000000, true, false)]
        public void AND_R32_R32(uint ebxValue, uint edxValue, uint expectedResult, bool zeroFlagSet, bool signFlagSet)
        {
            Reset();

            mbbsEmuCpuRegisters.EBX = ebxValue;
            mbbsEmuCpuRegisters.EDX = edxValue;

            var instructions = new Assembler(16);
            instructions.and(ebx, edx);
            CreateCodeSegment(instructions);

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Results
            Assert.Equal(expectedResult, mbbsEmuCpuRegisters.EBX);

            //Verify Flags
            Assert.Equal(zeroFlagSet, mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.ZF));
            Assert.Equal(signFlagSet, mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.SF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.OF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.CF));
        }

        [Theory]
        [InlineData(0xFF, 0x00, 0x00, true, false)]
        [InlineData(0xFF, 0xFF, 0xFF, false, true)]
        [InlineData(0x00, 0x00, 0x00, true, false)]
        public void AND_R8_M8(byte registerValue, byte memoryValue, byte expectedResult, bool zeroFlagSet, bool signFlagSet)
        {
            Reset();

            mbbsEmuProtectedModeMemoryCore.AddSegment(2);
            mbbsEmuCpuRegisters.DS = 2;
            mbbsEmuMemoryCore.SetByte(2, 0, memoryValue);
            mbbsEmuCpuRegisters.AL = registerValue;

            var instructions = new Assembler(16);
            instructions.and(al, __byte_ptr[0]);
            CreateCodeSegment(instructions);

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Results
            Assert.Equal(expectedResult, mbbsEmuCpuRegisters.AL);

            //Verify Flags
            Assert.Equal(zeroFlagSet, mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.ZF));
            Assert.Equal(signFlagSet, mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.SF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.OF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.CF));
        }

        [Theory]
        [InlineData(0xFFFF, 0x0000, 0x0000, true, false)]
        [InlineData(0xFFFF, 0xFFFF, 0xFFFF, false, true)]
        [InlineData(0x0000, 0x0000, 0x0000, true, false)]
        public void AND_R16_M16(ushort registerValue, ushort memoryValue, ushort expectedResult, bool zeroFlagSet, bool signFlagSet)
        {
            Reset();

            mbbsEmuProtectedModeMemoryCore.AddSegment(2);
            mbbsEmuCpuRegisters.DS = 2;
            mbbsEmuMemoryCore.SetWord(2, 0, memoryValue);
            mbbsEmuCpuRegisters.AX = registerValue;

            var instructions = new Assembler(16);
            instructions.and(ax, __word_ptr[0]);
            CreateCodeSegment(instructions);

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Results
            Assert.Equal(expectedResult, mbbsEmuCpuRegisters.AX);

            //Verify Flags
            Assert.Equal(zeroFlagSet, mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.ZF));
            Assert.Equal(signFlagSet, mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.SF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.OF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.CF));
        }

        [Theory]
        [InlineData(0xFFFFFFFF, 0x00000000, 0x00000000, true, false)]
        [InlineData(0xFFFFFFFF, 0xFFFFFFFF, 0xFFFFFFFF, false, true)]
        [InlineData(0x00000000, 0x00000000, 0x00000000, true, false)]
        public void AND_R32_M32(uint registerValue, uint memoryValue, uint expectedResult, bool zeroFlagSet, bool signFlagSet)
        {
            Reset();

            mbbsEmuProtectedModeMemoryCore.AddSegment(2);
            mbbsEmuCpuRegisters.DS = 2;
            mbbsEmuMemoryCore.SetDWord(2, 0, memoryValue);
            mbbsEmuCpuRegisters.EAX = registerValue;

            var instructions = new Assembler(16);
            instructions.and(eax, __dword_ptr[0]);
            CreateCodeSegment(instructions);

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Results
            Assert.Equal(expectedResult, mbbsEmuCpuRegisters.EAX);

            //Verify Flags
            Assert.Equal(zeroFlagSet, mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.ZF));
            Assert.Equal(signFlagSet, mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.SF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.OF));
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.CF));
        }
    }
}
