using Iced.Intel;
using Xunit;
using static Iced.Intel.AssemblerRegisters;

namespace MBBSEmu.Tests.CPU
{
    public class DEC_Tests : CpuTestBase
    {
        [Theory]
        [InlineData(1, 0, true, false, false)]
        [InlineData(0, 0xFF, false, false, true)]
        [InlineData(0x80, 0x7F, false, true, false)]
        [InlineData(0x81, 0x80, false, false, true)]
        public void DEC_R8(byte alValue, byte expectedResult, bool zeroFlagSet, bool overflowFlagSet, bool signFlagSet)
        {
            Reset();

            mbbsEmuCpuRegisters.AL = alValue;

            var instructions = new Assembler(16);
            instructions.dec(al);
            CreateCodeSegment(instructions);

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Results
            Assert.Equal(expectedResult, mbbsEmuCpuRegisters.AL);

            //Verify Flags
            Assert.Equal(zeroFlagSet, mbbsEmuCpuRegisters.ZeroFlag);
            Assert.Equal(overflowFlagSet, mbbsEmuCpuRegisters.OverflowFlag);
            Assert.Equal(signFlagSet, mbbsEmuCpuRegisters.SignFlag);
        }

        [Theory]
        [InlineData(1, 0, true, false, false)]
        [InlineData(0, 0xFFFF, false, false, true)]
        [InlineData(0x8000, 0x7FFF, false, true, false)]
        [InlineData(0x8001, 0x8000, false, false, true)]
        public void DEC_M16(ushort memoryValue, ushort expectedResult, bool zeroFlagSet, bool overflowFlagSet, bool signFlagSet)
        {
            Reset();

            mbbsEmuProtectedModeMemoryCore.AddSegment(2);
            mbbsEmuCpuRegisters.DS = 2;
            mbbsEmuMemoryCore.SetWord(2, 0, memoryValue);

            var instructions = new Assembler(16);
            instructions.dec(__word_ptr[0]);
            CreateCodeSegment(instructions);

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Results
            Assert.Equal(expectedResult, mbbsEmuMemoryCore.GetWord(2, 0));

            //Verify Flags
            Assert.Equal(zeroFlagSet, mbbsEmuCpuRegisters.ZeroFlag);
            Assert.Equal(overflowFlagSet, mbbsEmuCpuRegisters.OverflowFlag);
            Assert.Equal(signFlagSet, mbbsEmuCpuRegisters.SignFlag);
        }

        [Theory]
        [InlineData(1, 0, true, false, false)]
        [InlineData(0, 0xFFFFFFFF, false, false, true)]
        [InlineData(0x80000000, 0x7FFFFFFF, false, true, false)]
        [InlineData(0x80000001, 0x80000000, false, false, true)]
        public void DEC_R32(uint eaxValue, uint expectedResult, bool zeroFlagSet, bool overflowFlagSet, bool signFlagSet)
        {
            Reset();

            mbbsEmuCpuRegisters.EAX = eaxValue;

            var instructions = new Assembler(16);
            instructions.dec(eax);
            CreateCodeSegment(instructions);

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Results
            Assert.Equal(expectedResult, mbbsEmuCpuRegisters.EAX);

            //Verify Flags
            Assert.Equal(zeroFlagSet, mbbsEmuCpuRegisters.ZeroFlag);
            Assert.Equal(overflowFlagSet, mbbsEmuCpuRegisters.OverflowFlag);
            Assert.Equal(signFlagSet, mbbsEmuCpuRegisters.SignFlag);
        }

        [Theory]
        [InlineData(0x80000000, 0x7FFFFFFF)]
        [InlineData(0x00000000, 0xFFFFFFFF)]
        public void DEC_M32(uint memoryValue, uint expectedResult)
        {
            Reset();

            mbbsEmuProtectedModeMemoryCore.AddSegment(2);
            mbbsEmuCpuRegisters.DS = 2;
            mbbsEmuMemoryCore.SetDWord(2, 0, memoryValue);

            var instructions = new Assembler(16);
            instructions.dec(__dword_ptr[0]);
            CreateCodeSegment(instructions);

            //Process Instruction
            mbbsEmuCpuCore.Tick();

            //Verify Results
            Assert.Equal(expectedResult, mbbsEmuMemoryCore.GetDWord(2, 0));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void DEC_PreservesCarryFlag(bool carryFlag)
        {
            Reset();

            mbbsEmuCpuRegisters.AL = 0;
            mbbsEmuCpuRegisters.BX = 0;
            mbbsEmuCpuRegisters.ECX = 0;

            var instructions = new Assembler(16);
            instructions.dec(al);
            instructions.dec(bx);
            instructions.dec(ecx);
            CreateCodeSegment(instructions);

            //Each DEC wraps below zero, which would borrow in a SUB; DEC must leave CF alone
            for (var i = 0; i < 3; i++)
            {
                mbbsEmuCpuRegisters.CarryFlag = carryFlag;
                mbbsEmuCpuCore.Tick();
                Assert.Equal(carryFlag, mbbsEmuCpuRegisters.CarryFlag);
            }

            Assert.Equal(0xFF, mbbsEmuCpuRegisters.AL);
            Assert.Equal(0xFFFF, mbbsEmuCpuRegisters.BX);
            Assert.Equal(0xFFFFFFFFu, mbbsEmuCpuRegisters.ECX);
        }

        [Theory]
        [InlineData(0x00000000u, true)]
        [InlineData(0x00000010u, true)]
        [InlineData(0x00000001u, false)]
        [InlineData(0x0000001Fu, false)]
        public void DEC_AuxiliaryCarryFlag(uint value, bool expectedAuxiliaryCarryFlag)
        {
            Reset();

            mbbsEmuCpuRegisters.AL = (byte)value;
            mbbsEmuCpuRegisters.BX = (ushort)value;
            mbbsEmuCpuRegisters.ECX = value;

            var instructions = new Assembler(16);
            instructions.dec(al);
            instructions.dec(bx);
            instructions.dec(ecx);
            CreateCodeSegment(instructions);

            for (var i = 0; i < 3; i++)
            {
                mbbsEmuCpuRegisters.AuxiliaryCarryFlag = !expectedAuxiliaryCarryFlag;
                mbbsEmuCpuCore.Tick();
                Assert.Equal(expectedAuxiliaryCarryFlag, mbbsEmuCpuRegisters.AuxiliaryCarryFlag);
            }
        }
    }
}
