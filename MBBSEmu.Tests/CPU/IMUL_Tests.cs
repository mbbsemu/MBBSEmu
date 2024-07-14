using Iced.Intel;
using Xunit;
using static Iced.Intel.AssemblerRegisters;

namespace MBBSEmu.Tests.CPU
{
    public class IMUL_Tests : CpuTestBase
    {
        [Theory]
        [InlineData(1, -1, -1, false, false)]
        [InlineData(-1, -1, 1, false, false)]
        [InlineData(-127, -1, 127, false, false)]
        [InlineData(127, -1, -127, false, false)]
        public void IMUL_8_R8_Test(sbyte alValue, sbyte valueToMultiply, short expectedValue, bool carryFlag,
            bool overflowFlag)
        {
            Reset();
            mbbsEmuCpuRegisters.AL = (byte)alValue;
            mbbsEmuCpuRegisters.BL = (byte)valueToMultiply;

            var instructions = new Assembler(16);
            instructions.imul(bl);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            //Verify Results
            Assert.Equal(expectedValue, (sbyte)mbbsEmuCpuRegisters.AL);
            Assert.Equal(carryFlag, mbbsEmuCpuRegisters.CarryFlag);
            Assert.Equal(overflowFlag, mbbsEmuCpuRegisters.OverflowFlag);
        }

        [Theory]
        [InlineData(1, -1, -1, false, false)]
        [InlineData(-1, -1, 1, false, false)]
        [InlineData(-127, -1, 127, false, false)]
        [InlineData(127, -1, -127, false, false)]
        [InlineData(short.MaxValue, -1, short.MinValue + 1, false, false)]
        public void IMUL_16_R16_Test(short axValue, short valueToMultiply, short expectedValue, bool carryFlag,
            bool overflowFlag)
        {
            Reset();
            mbbsEmuCpuRegisters.AX = (ushort)axValue;
            mbbsEmuCpuRegisters.BX = (ushort)valueToMultiply;

            var instructions = new Assembler(16);
            instructions.imul(bx);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            //Verify Results
            Assert.Equal(expectedValue, (short)mbbsEmuCpuRegisters.AX);
            Assert.Equal(carryFlag, mbbsEmuCpuRegisters.CarryFlag);
            Assert.Equal(overflowFlag, mbbsEmuCpuRegisters.OverflowFlag);
        }


        [Theory]
        [InlineData(1, -1, -1, false, false)]
        [InlineData(-1, -1, 1, false, false)]
        [InlineData(-127, -1, 127, false, false)]
        [InlineData(127, -1, -127, false, false)]
        [InlineData(short.MaxValue, -1, short.MinValue + 1, false, false)]
        [InlineData(short.MaxValue, -2, 2, true, true)]
        public void IMUL_16_R16_3OP_Test(short bxValue, short valueToMultiply, short expectedValue, bool carryFlag,
            bool overflowFlag)
        {
            Reset();
            mbbsEmuCpuRegisters.BX = (ushort)bxValue;

            var instructions = new Assembler(16);
            //AX = BX * ValueToMultiply
            instructions.imul(ax, bx, valueToMultiply);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            //Verify Results
            Assert.Equal(expectedValue, (short)mbbsEmuCpuRegisters.AX);
            Assert.Equal(carryFlag, mbbsEmuCpuRegisters.CarryFlag);
            Assert.Equal(overflowFlag, mbbsEmuCpuRegisters.OverflowFlag);
        }

        [Theory]
        [InlineData(1, -1, -1, false, false)]
        [InlineData(5, 10, 50, false, false)]
        [InlineData(-1, -1, 1, false, false)]
        [InlineData(-127, -1, 127, false, false)]
        [InlineData(127, -1, -127, false, false)]
        [InlineData(short.MaxValue, -1, short.MinValue + 1, false, false)]
        [InlineData(short.MaxValue, -2, 2, true, true)]
        public void IMUL_16_M16_3OP_Test(short memoryValue, short valueToMultiply, short expectedValue, bool carryFlag,
            bool overflowFlag)
        {
            Reset();

            //Setup Memory
            CreateDataSegment(new byte[ushort.MaxValue]);
            mbbsEmuMemoryCore.SetWord(2,0, (ushort)memoryValue);
            mbbsEmuCpuRegisters.DS = 2;

            var instructions = new Assembler(16);
            //AX == DS:[0] * ValueToMultiply
            instructions.imul(ax, __word_ptr[0], valueToMultiply);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            //Verify Results
            Assert.Equal(expectedValue, (short)mbbsEmuCpuRegisters.AX);
            Assert.Equal(carryFlag, mbbsEmuCpuRegisters.CarryFlag);
            Assert.Equal(overflowFlag, mbbsEmuCpuRegisters.OverflowFlag);
        }
    }
}
