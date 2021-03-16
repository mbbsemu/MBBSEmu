using Iced.Intel;
using MBBSEmu.CPU;
using MBBSEmu.Extensions;
using Xunit;
using static Iced.Intel.AssemblerRegisters;

namespace MBBSEmu.Tests.CPU
{
    public class INC_Tests : CpuTestBase
    {
        [Theory]
        [InlineData(0,1, false, false, false)]
        [InlineData(0xFF, 0, true, false, false)]
        [InlineData(0x7F, 0x80, false, true, true)]
        [InlineData(0x80, 0x81, false, false, true)]
        public void INC_R8(byte alValue, byte expectedResult, bool zeroFlagSet, bool overflowFlagSet, bool signFlagSet)
        {
            Reset();

            mbbsEmuCpuRegisters.AL = alValue;

            var instructions = new Assembler(16);
            instructions.inc(al);
            CreateCodeSegment(instructions);

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Results
            Assert.Equal(expectedResult, mbbsEmuCpuRegisters.AL);

            //Verify Flags
            Assert.Equal(zeroFlagSet, mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.ZF));
            Assert.Equal(overflowFlagSet, mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.OF));
            Assert.Equal(signFlagSet, mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.SF));
        }

        [Theory]
        [InlineData(0, 1, false, false, false)]
        [InlineData(0xFF, 0, true, false, false)]
        [InlineData(0x7F, 0x80, false, true, true)]
        [InlineData(0x80, 0x81, false, false, true)]
        public void INC_M8_ClearFlags(byte memoryValue, byte expectedResult, bool zeroFlagSet, bool overflowFlagSet, bool signFlagSet)
        {
            Reset();
            mbbsEmuProtectedMemoryCore.AddSegment(2);
            mbbsEmuCpuRegisters.DS = 2;
            mbbsEmuMemoryCore.SetByte(2,0, memoryValue);

            var instructions = new Assembler(16);
            instructions.inc(__byte_ptr[0]);
            CreateCodeSegment(instructions);

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Results
            Assert.Equal(expectedResult, mbbsEmuMemoryCore.GetByte(2,0));

            //Verify Flags
            Assert.Equal(zeroFlagSet, mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.ZF));
            Assert.Equal(overflowFlagSet, mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.OF));
            Assert.Equal(signFlagSet, mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.SF));
        }

        [Theory]
        [InlineData(0, 1, false, false, false)]
        [InlineData(0xFFFF, 0, true, false, false)]
        [InlineData(0x7FFF, 0x8000, false, true, true)]
        [InlineData(0x8000, 0x8001, false, false, true)]
        public void INC_R16(ushort axValue, ushort expectedResult, bool zeroFlagSet, bool overflowFlagSet, bool signFlagSet)
        {
            Reset();

            mbbsEmuCpuRegisters.AX = axValue;

            var instructions = new Assembler(16);
            instructions.inc(ax);
            CreateCodeSegment(instructions);

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Results
            Assert.Equal(expectedResult, mbbsEmuCpuRegisters.AX);

            //Verify Flags
            Assert.Equal(zeroFlagSet, mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.ZF));
            Assert.Equal(overflowFlagSet, mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.OF));
            Assert.Equal(signFlagSet, mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.SF));
        }

        [Theory]
        [InlineData(0, 1, false, false, false)]
        [InlineData(0xFFFF, 0, true, false, false)]
        [InlineData(0x7FFF, 0x8000, false, true, true)]
        [InlineData(0x8000, 0x8001, false, false, true)]
        public void INC_M16(ushort memoryValue, ushort expectedResult, bool zeroFlagSet, bool overflowFlagSet, bool signFlagSet)
        {
            Reset();

            mbbsEmuProtectedMemoryCore.AddSegment(2);
            mbbsEmuCpuRegisters.DS = 2;
            mbbsEmuMemoryCore.SetWord(2, 0, memoryValue);

            var instructions = new Assembler(16);
            instructions.inc(__word_ptr[0]);
            CreateCodeSegment(instructions);

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Results
            Assert.Equal(expectedResult, mbbsEmuMemoryCore.GetWord(2, 0));

            //Verify Flags
            Assert.Equal(zeroFlagSet, mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.ZF));
            Assert.Equal(overflowFlagSet, mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.OF));
            Assert.Equal(signFlagSet, mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.SF));
        }

        [Theory]
        [InlineData(0, 1, false, false, false)]
        [InlineData(0xFFFFFFFF, 0, true, false, false)]
        [InlineData(0x7FFFFFFF, 0x80000000, false, true, true)]
        [InlineData(0x80000000, 0x80000001, false, false, true)]
        public void INC_R32(uint eaxValue, uint expectedResult, bool zeroFlagSet, bool overflowFlagSet, bool signFlagSet)
        {
            Reset();

            mbbsEmuCpuRegisters.EAX = eaxValue;

            var instructions = new Assembler(16);
            instructions.inc(eax);
            CreateCodeSegment(instructions);

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Results
            Assert.Equal(expectedResult, mbbsEmuCpuRegisters.EAX);

            //Verify Flags
            Assert.Equal(zeroFlagSet, mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.ZF));
            Assert.Equal(overflowFlagSet, mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.OF));
            Assert.Equal(signFlagSet, mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.SF));
        }

        [Theory]
        [InlineData(0, 1, false, false, false)]
        [InlineData(0xFFFFFFFF, 0, true, false, false)]
        [InlineData(0x7FFFFFFF, 0x80000000, false, true, true)]
        [InlineData(0x80000000, 0x80000001, false, false, true)]
        public void INC_M32(uint memoryValue, uint expectedResult, bool zeroFlagSet, bool overflowFlagSet, bool signFlagSet)
        {
            Reset();

            mbbsEmuProtectedMemoryCore.AddSegment(2);
            mbbsEmuCpuRegisters.DS = 2;
            mbbsEmuMemoryCore.SetDWord(2, 0, memoryValue);

            var instructions = new Assembler(16);
            instructions.inc(__dword_ptr[0]);
            CreateCodeSegment(instructions);

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Results
            Assert.Equal(expectedResult, mbbsEmuMemoryCore.GetDWord(2, 0));

            //Verify Flags
            Assert.Equal(zeroFlagSet, mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.ZF));
            Assert.Equal(overflowFlagSet, mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.OF));
            Assert.Equal(signFlagSet, mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.SF));
        }
    }
}
