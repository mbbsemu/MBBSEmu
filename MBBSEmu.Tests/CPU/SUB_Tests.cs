using Iced.Intel;
using MBBSEmu.CPU;
using MBBSEmu.Extensions;
using Xunit;
using static Iced.Intel.AssemblerRegisters;

namespace MBBSEmu.Tests.CPU
{
    public class SUB_Tests : CpuTestBase
    {
        [Theory]
        [InlineData(1, 1, 0, false, false, true, false)]
        [InlineData(0, 1, 255, false, true, false, true)]
        [InlineData(0, 255, 1, false, false, false, true)]
        [InlineData(0x80, 1, 0x7F, true, false, false, false)]
        public void SUB_AL_IMM8(byte alValue, byte valueToSubtract, byte expectedValue, bool overflowFlagValue, bool signFlagValue, bool zeroFlagValue, bool carryFlagValue)
        {
            Reset();

            mbbsEmuCpuRegisters.AL = alValue;

            var instructions = new Assembler(16);
            instructions.sub(al, valueToSubtract);
            CreateCodeSegment(instructions);

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Results
            Assert.Equal(expectedValue, mbbsEmuCpuRegisters.AL);

            //Verify Flags
            Assert.Equal(zeroFlagValue, mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.ZF));
            Assert.Equal(signFlagValue, mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.SF));
            Assert.Equal(overflowFlagValue, mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.OF));
            Assert.Equal(carryFlagValue, mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.CF));
        }

        [Theory]
        [InlineData(1, 1, 0, false, false, true, false)]
        [InlineData(0, 1, 0xFFFF, false, true, false, true)]
        [InlineData(0, 0xFFFF, 1, false, false, false, true)]
        [InlineData(0x8000, 1, 0x7FFF, true, false, false, false)]
        public void SUB_AX_IMM16(ushort axValue, ushort valueToSubtract, ushort expectedValue, bool overflowFlagValue, bool signFlagValue, bool zeroFlagValue, bool carryFlagValue)
        {
            Reset();

            mbbsEmuCpuRegisters.AX = axValue;

            var instructions = new Assembler(16);
            instructions.sub(ax, valueToSubtract);
            CreateCodeSegment(instructions);

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Results
            Assert.Equal(expectedValue, mbbsEmuCpuRegisters.AX);

            //Verify Flags
            Assert.Equal(zeroFlagValue, mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.ZF));
            Assert.Equal(signFlagValue, mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.SF));
            Assert.Equal(overflowFlagValue, mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.OF));
            Assert.Equal(carryFlagValue, mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.CF));
        }

        [Theory]
        [InlineData(1, 1, 0, false, false, true, false)]
        [InlineData(0, 1, 0xFFFFFFFF, false, true, false, true)]
        [InlineData(0, 0xFFFFFFFF, 1, false, false, false, true)]
        [InlineData(0x80000000, 1, 0x7FFFFFFF, true, false, false, false)]
        public void SUB_EAX_IMM32(uint eaxValue, uint valueToSubtract, uint expectedValue, bool overflowFlagValue, bool signFlagValue, bool zeroFlagValue, bool carryFlagValue)
        {
            Reset();

            mbbsEmuCpuRegisters.EAX = eaxValue;

            var instructions = new Assembler(16);
            instructions.sub(eax, valueToSubtract);
            CreateCodeSegment(instructions);

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Results
            Assert.Equal(expectedValue, mbbsEmuCpuRegisters.EAX);

            //Verify Flags
            Assert.Equal(zeroFlagValue, mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.ZF));
            Assert.Equal(signFlagValue, mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.SF));
            Assert.Equal(overflowFlagValue, mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.OF));
            Assert.Equal(carryFlagValue, mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.CF));
        }

        [Theory]
        [InlineData(1, 1, 0, false, false, true, false)]
        [InlineData(0, 1, 255, false, true, false, true)]
        [InlineData(0, 255, 1, false, false, false, true)]
        [InlineData(0x80, 1, 0x7F, true, false, false, false)]
        public void SUB_R8_IMM8(byte blValue, byte valueToSubtract, byte expectedValue, bool overflowFlagValue, bool signFlagValue, bool zeroFlagValue, bool carryFlagValue)
        {
            Reset();

            mbbsEmuCpuRegisters.BL = blValue;

            var instructions = new Assembler(16);
            instructions.sub(bl, valueToSubtract);
            CreateCodeSegment(instructions);

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Results
            Assert.Equal(expectedValue, mbbsEmuCpuRegisters.BL);

            //Verify Flags
            Assert.Equal(zeroFlagValue, mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.ZF));
            Assert.Equal(signFlagValue, mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.SF));
            Assert.Equal(overflowFlagValue, mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.OF));
            Assert.Equal(carryFlagValue, mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.CF));
        }

        [Theory]
        [InlineData(1, 1, 0, false, false, true, false)]
        [InlineData(0, 1, 0xFFFF, false, true, false, true)]
        [InlineData(0, 0xFFFF, 1, false, false, false, true)]
        [InlineData(0x8000, 1, 0x7FFF, true, false, false, false)]
        public void SUB_R16_IMM16(ushort bxValue, ushort valueToSubtract, ushort expectedValue, bool overflowFlagValue, bool signFlagValue, bool zeroFlagValue, bool carryFlagValue)
        {
            Reset();

            mbbsEmuCpuRegisters.BX = bxValue;

            var instructions = new Assembler(16);
            instructions.sub(bx, valueToSubtract);
            CreateCodeSegment(instructions);

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Results
            Assert.Equal(expectedValue, mbbsEmuCpuRegisters.BX);

            //Verify Flags
            Assert.Equal(zeroFlagValue, mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.ZF));
            Assert.Equal(signFlagValue, mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.SF));
            Assert.Equal(overflowFlagValue, mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.OF));
            Assert.Equal(carryFlagValue, mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.CF));
        }

        [Theory]
        [InlineData(1, 1, 0, false, false, true, false)]
        [InlineData(0, 1, 0xFFFFFFFF, false, true, false, true)]
        [InlineData(0, 0xFFFFFFFF, 1, false, false, false, true)]
        [InlineData(0x80000000, 1, 0x7FFFFFFF, true, false, false, false)]
        public void SUB_R32_IMM32(uint ebxValue, uint valueToSubtract, uint expectedValue, bool overflowFlagValue, bool signFlagValue, bool zeroFlagValue, bool carryFlagValue)
        {
            Reset();

            mbbsEmuCpuRegisters.EBX = ebxValue;

            var instructions = new Assembler(16);
            instructions.sub(ebx, valueToSubtract);
            CreateCodeSegment(instructions);

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Results
            Assert.Equal(expectedValue, mbbsEmuCpuRegisters.EBX);

            //Verify Flags
            Assert.Equal(zeroFlagValue, mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.ZF));
            Assert.Equal(signFlagValue, mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.SF));
            Assert.Equal(overflowFlagValue, mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.OF));
            Assert.Equal(carryFlagValue, mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.CF));
        }

        [Theory]
        [InlineData(1, 1, 0, false, false, true, false)]
        [InlineData(0, 1, 255, false, true, false, true)]
        [InlineData(0, 255, 1, false, false, false, true)]
        [InlineData(0x80, 1, 0x7F, true, false, false, false)]
        public void SUB_R16_IMM8(ushort bxValue, byte valueToSubtract, byte expectedValue, bool overflowFlagValue, bool signFlagValue, bool zeroFlagValue, bool carryFlagValue)
        {
            Reset();

            mbbsEmuCpuRegisters.BX = bxValue;

            var instructions = new Assembler(16);
            instructions.sub(bl, valueToSubtract);
            CreateCodeSegment(instructions);

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Results
            Assert.Equal(expectedValue, mbbsEmuCpuRegisters.BL);

            //Verify Flags
            Assert.Equal(zeroFlagValue, mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.ZF));
            Assert.Equal(signFlagValue, mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.SF));
            Assert.Equal(overflowFlagValue, mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.OF));
            Assert.Equal(carryFlagValue, mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.CF));
        }

        [Theory]
        [InlineData(1, 1, 0, false, false, true, false)]
        [InlineData(0, 1, 0xFFFFFFFF, false, true, false, true)]
        [InlineData(0, 255, 0xFFFFFF01, false, true, false, true)]
        [InlineData(0x80000000, 1, 0x7FFFFFFF, true, false, false, false)]
        public void SUB_R32_IMM8(uint ebxValue, byte valueToSubtract, uint expectedValue, bool overflowFlagValue, bool signFlagValue, bool zeroFlagValue, bool carryFlagValue)
        {
            Reset();

            mbbsEmuCpuRegisters.EBX = ebxValue;

            var instructions = new Assembler(16);
            instructions.sub(ebx, valueToSubtract);
            CreateCodeSegment(instructions);

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Results
            Assert.Equal(expectedValue, mbbsEmuCpuRegisters.EBX);

            //Verify Flags
            Assert.Equal(zeroFlagValue, mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.ZF));
            Assert.Equal(signFlagValue, mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.SF));
            Assert.Equal(overflowFlagValue, mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.OF));
            Assert.Equal(carryFlagValue, mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.CF));
        }

        [Theory]
        [InlineData(1, 1, 0, false, false, true, false)]
        [InlineData(0, 1, 255, false, true, false, true)]
        [InlineData(0, 255, 1, false, false, false, true)]
        [InlineData(0x80, 1, 0x7F, true, false, false, false)]
        public void SUB_R8_R8(byte alValue, byte blValue, byte expectedValue, bool overflowFlagValue, bool signFlagValue, bool zeroFlagValue, bool carryFlagValue)
        {
            Reset();

            mbbsEmuCpuRegisters.AL = alValue;
            mbbsEmuCpuRegisters.BL = blValue;

            var instructions = new Assembler(16);
            instructions.sub(al, bl);
            CreateCodeSegment(instructions);

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Results
            Assert.Equal(expectedValue, mbbsEmuCpuRegisters.AL);

            //Verify Flags
            Assert.Equal(zeroFlagValue, mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.ZF));
            Assert.Equal(signFlagValue, mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.SF));
            Assert.Equal(overflowFlagValue, mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.OF));
            Assert.Equal(carryFlagValue, mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.CF));
        }

        [Theory]
        [InlineData(1, 1, 0, false, false, true, false)]
        [InlineData(0, 1, 0xFFFF, false, true, false, true)]
        [InlineData(0, 0xFFFF, 1, false, false, false, true)]
        [InlineData(0x8000, 1, 0x7FFF, true, false, false, false)]
        public void SUB_R16_R16(ushort axValue, ushort bxValue, ushort expectedValue, bool overflowFlagValue, bool signFlagValue, bool zeroFlagValue, bool carryFlagValue)
        {
            Reset();

            mbbsEmuCpuRegisters.AX = axValue;
            mbbsEmuCpuRegisters.BX = bxValue;

            var instructions = new Assembler(16);
            instructions.sub(ax, bx);
            CreateCodeSegment(instructions);

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Results
            Assert.Equal(expectedValue, mbbsEmuCpuRegisters.AX);

            //Verify Flags
            Assert.Equal(zeroFlagValue, mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.ZF));
            Assert.Equal(signFlagValue, mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.SF));
            Assert.Equal(overflowFlagValue, mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.OF));
            Assert.Equal(carryFlagValue, mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.CF));
        }

        [Theory]
        [InlineData(1, 1, 0, false, false, true, false)]
        [InlineData(0, 1, 0xFFFFFFFF, false, true, false, true)]
        [InlineData(0, 0xFFFFFFFF, 1, false, false, false, true)]
        [InlineData(0x80000000, 1, 0x7FFFFFFF, true, false, false, false)]
        public void SUB_R32_R32(uint eaxValue, uint ebxValue, uint expectedValue, bool overflowFlagValue, bool signFlagValue, bool zeroFlagValue, bool carryFlagValue)
        {
            Reset();

            mbbsEmuCpuRegisters.EAX = eaxValue;
            mbbsEmuCpuRegisters.EBX = ebxValue;

            var instructions = new Assembler(16);
            instructions.sub(eax, ebx);
            CreateCodeSegment(instructions);

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Results
            Assert.Equal(expectedValue, mbbsEmuCpuRegisters.EAX);

            //Verify Flags
            Assert.Equal(zeroFlagValue, mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.ZF));
            Assert.Equal(signFlagValue, mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.SF));
            Assert.Equal(overflowFlagValue, mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.OF));
            Assert.Equal(carryFlagValue, mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.CF));
        }

        [Theory]
        [InlineData(1, 1, 0, false, false, true, false)]
        [InlineData(0, 1, 255, false, true, false, true)]
        [InlineData(0, 255, 1, false, false, false, true)]
        [InlineData(0x80, 1, 0x7F, true, false, false, false)]
        public void SUB_M8_R8(byte memoryValue, byte alValue, byte expectedValue, bool overflowFlagValue, bool signFlagValue, bool zeroFlagValue, bool carryFlagValue)
        {
            Reset();
            mbbsEmuProtectedModeMemoryCore.AddSegment(2);
            mbbsEmuCpuRegisters.DS = 2;
            mbbsEmuMemoryCore.SetByte(2,0, memoryValue);
            mbbsEmuCpuRegisters.AL = alValue;

            var instructions = new Assembler(16);
            instructions.sub(__byte_ptr[0], al);
            CreateCodeSegment(instructions);

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Results
            Assert.Equal(expectedValue, mbbsEmuMemoryCore.GetByte(2,0));

            //Verify Flags
            Assert.Equal(zeroFlagValue, mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.ZF));
            Assert.Equal(signFlagValue, mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.SF));
            Assert.Equal(overflowFlagValue, mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.OF));
            Assert.Equal(carryFlagValue, mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.CF));
        }

        [Theory]
        [InlineData(1, 1, 0, false, false, true, false)]
        [InlineData(0, 1, 0xFFFF, false, true, false, true)]
        [InlineData(0, 0xFFFF, 1, false, false, false, true)]
        [InlineData(0x8000, 1, 0x7FFF, true, false, false, false)]
        public void SUB_M16_R16(ushort memoryValue, ushort axValue, ushort expectedValue, bool overflowFlagValue, bool signFlagValue, bool zeroFlagValue, bool carryFlagValue)
        {
            Reset();
            mbbsEmuProtectedModeMemoryCore.AddSegment(2);
            mbbsEmuCpuRegisters.DS = 2;
            mbbsEmuMemoryCore.SetWord(2, 0, memoryValue);
            mbbsEmuCpuRegisters.AX = axValue;

            var instructions = new Assembler(16);
            instructions.sub(__word_ptr[0], ax);
            CreateCodeSegment(instructions);

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Results
            Assert.Equal(expectedValue, mbbsEmuMemoryCore.GetWord(2, 0));

            //Verify Flags
            Assert.Equal(zeroFlagValue, mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.ZF));
            Assert.Equal(signFlagValue, mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.SF));
            Assert.Equal(overflowFlagValue, mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.OF));
            Assert.Equal(carryFlagValue, mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.CF));
        }

        [Theory]
        [InlineData(1, 1, 0, false, false, true, false)]
        [InlineData(0, 1, 0xFFFFFFFF, false, true, false, true)]
        [InlineData(0, 0xFFFFFFFF, 1, false, false, false, true)]
        [InlineData(0x80000000, 1, 0x7FFFFFFF, true, false, false, false)]
        public void SUB_M32_R32(uint memoryValue, uint axValue, uint expectedValue, bool overflowFlagValue, bool signFlagValue, bool zeroFlagValue, bool carryFlagValue)
        {
            Reset();
            mbbsEmuProtectedModeMemoryCore.AddSegment(2);
            mbbsEmuCpuRegisters.DS = 2;
            mbbsEmuMemoryCore.SetDWord(2, 0, memoryValue);
            mbbsEmuCpuRegisters.EAX = axValue;

            var instructions = new Assembler(16);
            instructions.sub(__dword_ptr[0], eax);
            CreateCodeSegment(instructions);

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Results
            Assert.Equal(expectedValue, mbbsEmuMemoryCore.GetDWord(2, 0));

            //Verify Flags
            Assert.Equal(zeroFlagValue, mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.ZF));
            Assert.Equal(signFlagValue, mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.SF));
            Assert.Equal(overflowFlagValue, mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.OF));
            Assert.Equal(carryFlagValue, mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.CF));
        }

        [Theory]
        [InlineData(1, 1, 0, false, false, true, false)]
        [InlineData(0, 1, 255, false, true, false, true)]
        [InlineData(0, 255, 1, false, false, false, true)]
        [InlineData(0x80, 1, 0x7F, true, false, false, false)]
        public void SUB_R8_M8(byte alValue, byte valueToSubtract, byte expectedValue, bool overflowFlagValue, bool signFlagValue, bool zeroFlagValue, bool carryFlagValue)
        {
            Reset();
            mbbsEmuProtectedModeMemoryCore.AddSegment(2);
            mbbsEmuCpuRegisters.DS = 2;
            mbbsEmuMemoryCore.SetByte(2, 0, valueToSubtract);
            mbbsEmuCpuRegisters.AL = alValue;

            var instructions = new Assembler(16);
            instructions.sub(al, __byte_ptr[0]);
            CreateCodeSegment(instructions);

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Results
            Assert.Equal(expectedValue, mbbsEmuCpuRegisters.AL);

            //Verify Flags
            Assert.Equal(zeroFlagValue, mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.ZF));
            Assert.Equal(signFlagValue, mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.SF));
            Assert.Equal(overflowFlagValue, mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.OF));
            Assert.Equal(carryFlagValue, mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.CF));
        }

        [Theory]
        [InlineData(1, 1, 0, false, false, true, false)]
        [InlineData(0, 1, 0xFFFF, false, true, false, true)]
        [InlineData(0, 0xFFFF, 1, false, false, false, true)]
        [InlineData(0x8000, 1, 0x7FFF, true, false, false, false)]
        public void SUB_R16_M16(ushort axValue, ushort valueToSubtract, ushort expectedValue, bool overflowFlagValue, bool signFlagValue, bool zeroFlagValue, bool carryFlagValue)
        {
            Reset();
            mbbsEmuProtectedModeMemoryCore.AddSegment(2);
            mbbsEmuCpuRegisters.DS = 2;
            mbbsEmuMemoryCore.SetWord(2, 0, valueToSubtract);
            mbbsEmuCpuRegisters.AX = axValue;

            var instructions = new Assembler(16);
            instructions.sub(ax, __word_ptr[0]);
            CreateCodeSegment(instructions);

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Results
            Assert.Equal(expectedValue, mbbsEmuCpuRegisters.AX);

            //Verify Flags
            Assert.Equal(zeroFlagValue, mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.ZF));
            Assert.Equal(signFlagValue, mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.SF));
            Assert.Equal(overflowFlagValue, mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.OF));
            Assert.Equal(carryFlagValue, mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.CF));
        }

        [Theory]
        [InlineData(1, 1, 0, false, false, true, false)]
        [InlineData(0, 1, 0xFFFFFFFF, false, true, false, true)]
        [InlineData(0, 0xFFFFFFFF, 1, false, false, false, true)]
        [InlineData(0x80000000, 1, 0x7FFFFFFF, true, false, false, false)]
        public void SUB_R32_M32(uint eaxValue, uint valueToSubtract, uint expectedValue, bool overflowFlagValue, bool signFlagValue, bool zeroFlagValue, bool carryFlagValue)
        {
            Reset();
            mbbsEmuProtectedModeMemoryCore.AddSegment(2);
            mbbsEmuCpuRegisters.DS = 2;
            mbbsEmuMemoryCore.SetDWord(2, 0, valueToSubtract);
            mbbsEmuCpuRegisters.EAX = eaxValue;

            var instructions = new Assembler(16);
            instructions.sub(eax, __dword_ptr[0]);
            CreateCodeSegment(instructions);

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Results
            Assert.Equal(expectedValue, mbbsEmuCpuRegisters.EAX);

            //Verify Flags
            Assert.Equal(zeroFlagValue, mbbsEmuCpuRegisters.ZeroFlag);
            Assert.Equal(signFlagValue, mbbsEmuCpuRegisters.SignFlag);
            Assert.Equal(overflowFlagValue, mbbsEmuCpuRegisters.OverflowFlag);
            Assert.Equal(carryFlagValue, mbbsEmuCpuRegisters.CarryFlag);
        }
    }
}
