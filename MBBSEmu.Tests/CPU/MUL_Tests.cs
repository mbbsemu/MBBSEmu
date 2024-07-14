using Iced.Intel;
using Xunit;
using static Iced.Intel.AssemblerRegisters;

namespace MBBSEmu.Tests.CPU
{
    public class MUL_Tests : CpuTestBase
    {
        [Theory]
        [InlineData(1, 0, 0, false, false)]
        [InlineData(5, 5, 25, false, false)]
        [InlineData(255, 255, 65025, true, true)]
        public void MUL_8_R8_Test(byte alValue, byte valueToMultiply, ushort expectedValue, bool carryFlag, bool overflowFlag)
        {
            Reset();
            mbbsEmuCpuRegisters.AL = (byte)alValue;
            mbbsEmuCpuRegisters.BL = (byte)valueToMultiply;

            var instructions = new Assembler(16);
            instructions.mul(bl);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            //Verify Results
            Assert.Equal(expectedValue, mbbsEmuCpuRegisters.AX);
            Assert.Equal(carryFlag, mbbsEmuCpuRegisters.CarryFlag);
            Assert.Equal(overflowFlag, mbbsEmuCpuRegisters.OverflowFlag);
        }

        [Theory]
        [InlineData(1, 0, 0, false, false)]
        [InlineData(5, 5, 25, false, false)]
        [InlineData(255, 255, 65025, true, true)]
        public void MUL_8_M8_Test(byte alValue, byte valueToMultiply, ushort expectedValue, bool carryFlag, bool overflowFlag)
        {
            Reset();

            mbbsEmuCpuRegisters.AL = (byte)alValue;
            mbbsEmuProtectedModeMemoryCore.AddSegment(2);
            mbbsEmuCpuRegisters.DS = 2;
            mbbsEmuCpuRegisters.BX = 0;
            mbbsEmuMemoryCore.SetByte(2,0, (byte)valueToMultiply);

            var instructions = new Assembler(16);
            instructions.mul(__byte_ptr[bx]);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            //Verify Results
            Assert.Equal(expectedValue, mbbsEmuCpuRegisters.AX);
            Assert.Equal(carryFlag, mbbsEmuCpuRegisters.CarryFlag);
            Assert.Equal(overflowFlag, mbbsEmuCpuRegisters.OverflowFlag);
        }

        [Theory]
        [InlineData(1, 0, 0, false, false)]
        [InlineData(5, 5, 25, false, false)]
        [InlineData(65535, 65535, 4294836225, true, true)]
        public void MUL_16_R16_Test(ushort axValue, ushort valueToMultiply, uint expectedValue, bool carryFlag, bool overflowFlag)
        {
            Reset();
            mbbsEmuCpuRegisters.AX = axValue;
            mbbsEmuCpuRegisters.BX = valueToMultiply;

            var instructions = new Assembler(16);
            instructions.mul(bx);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            //Verify Results
            Assert.Equal(expectedValue, (uint)(mbbsEmuCpuRegisters.DX << 16) | mbbsEmuCpuRegisters.AX);
            Assert.Equal(carryFlag, mbbsEmuCpuRegisters.CarryFlag);
            Assert.Equal(overflowFlag, mbbsEmuCpuRegisters.OverflowFlag);
        }

        [Theory]
        [InlineData(1, 0, 0, false, false)]
        [InlineData(5, 5, 25, false, false)]
        [InlineData(65535, 65535, 4294836225, true, true)]
        public void MUL_16_M16_Test(ushort axValue, ushort valueToMultiply, uint expectedValue, bool carryFlag, bool overflowFlag)
        {
            Reset();

            mbbsEmuCpuRegisters.AX = axValue;
            mbbsEmuProtectedModeMemoryCore.AddSegment(2);
            mbbsEmuCpuRegisters.DS = 2;
            mbbsEmuCpuRegisters.BX = 0;
            mbbsEmuMemoryCore.SetWord(2, 0, valueToMultiply);

            var instructions = new Assembler(16);
            instructions.mul(__word_ptr[bx]);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            //Verify Results
            Assert.Equal(expectedValue, (uint)(mbbsEmuCpuRegisters.DX << 16) | mbbsEmuCpuRegisters.AX);
            Assert.Equal(carryFlag, mbbsEmuCpuRegisters.CarryFlag);
            Assert.Equal(overflowFlag, mbbsEmuCpuRegisters.OverflowFlag);
        }

        [Theory]
        [InlineData(1, 0, 0, false, false)]
        [InlineData(5, 5, 25, false, false)]
        [InlineData(4294967295, 4294967295, 18446744065119617025, true, true)]
        public void MUL_32_R32_Test(uint eaxValue, uint valueToMultiply, ulong expectedValue, bool carryFlag, bool overflowFlag)
        {
            Reset();
            mbbsEmuCpuRegisters.EAX = eaxValue;
            mbbsEmuCpuRegisters.EBX = valueToMultiply;

            var instructions = new Assembler(16);
            instructions.mul(ebx);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            //Verify Results
            Assert.Equal(expectedValue, ((ulong)mbbsEmuCpuRegisters.EDX << 32) | mbbsEmuCpuRegisters.EAX);
            Assert.Equal(carryFlag, mbbsEmuCpuRegisters.CarryFlag);
            Assert.Equal(overflowFlag, mbbsEmuCpuRegisters.OverflowFlag);
        }

        [Theory]
        [InlineData(1, 0, 0, false, false)]
        [InlineData(5, 5, 25, false, false)]
        [InlineData(4294967295, 4294967295, 18446744065119617025, true, true)]
        public void MUL_32_M32_Test(uint eaxValue, uint valueToMultiply, ulong expectedValue, bool carryFlag, bool overflowFlag)
        {
            Reset();

            mbbsEmuCpuRegisters.EAX = eaxValue;
            mbbsEmuProtectedModeMemoryCore.AddSegment(2);
            mbbsEmuCpuRegisters.DS = 2;
            mbbsEmuCpuRegisters.BX = 0;
            mbbsEmuMemoryCore.SetDWord(2, 0, valueToMultiply);

            var instructions = new Assembler(16);
            instructions.mul(__dword_ptr[bx]);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            //Verify Results
            Assert.Equal(expectedValue, ((ulong)mbbsEmuCpuRegisters.EDX << 32) | mbbsEmuCpuRegisters.EAX);
            Assert.Equal(carryFlag, mbbsEmuCpuRegisters.CarryFlag);
            Assert.Equal(overflowFlag, mbbsEmuCpuRegisters.OverflowFlag);
        }
    }
}
