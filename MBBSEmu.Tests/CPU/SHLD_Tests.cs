using Iced.Intel;
using MBBSEmu.CPU;
using MBBSEmu.Extensions;
using Xunit;
using static Iced.Intel.AssemblerRegisters;

namespace MBBSEmu.Tests.CPU
{
    public class SHLD_Tests : CpuTestBase
    {
        [Theory]
        [InlineData(0xFFFFFFFF, 0x11111111, 16, 0xFFFF1111, true, false, true, false)]
        [InlineData(0xFFFFFFFF, 0x11111111, 32, 0xFFFFFFFF, false, false, true, false)]
        [InlineData(0x00001111, 0x11110000, 16, 0x11111111, false, false, false, false)]
        [InlineData(0xFFFF0000, 0xFFFF0000, 16, 0x0000FFFF, true, false, false, false)]
        [InlineData(0x7FFFFFFF, 0xFFFFFFFF, 1, 0xFFFFFFFF, false, true, true, false)]
        [InlineData(0x80000000, 0x00000000, 1, 0x00000000, true, true, false, true)]
        public void SHLD_EAX_EBX_IMM8(uint eaxValue, uint ebxValue, byte count, uint expectedValue, bool carryFlag, bool overflowFlag, bool signFlag, bool zeroFlag)
        {
            Reset();
            mbbsEmuCpuRegisters.EAX = eaxValue;
            mbbsEmuCpuRegisters.EBX = ebxValue;

            var instructions = new Assembler(16);
            instructions.shld(eax, ebx, count);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            //Verify Results
            Assert.Equal(expectedValue, mbbsEmuCpuRegisters.EAX);
            Assert.Equal(carryFlag, mbbsEmuCpuRegisters.CarryFlag);
            Assert.Equal(overflowFlag, mbbsEmuCpuRegisters.OverflowFlag);
            Assert.Equal(signFlag, mbbsEmuCpuRegisters.SignFlag);
            Assert.Equal(zeroFlag, mbbsEmuCpuRegisters.ZeroFlag);
        }

        [Theory]
        [InlineData(0xFFFFFFFF, 0x11111111, 16, 0xFFFF1111, true, false, true, false)]
        [InlineData(0xFFFFFFFF, 0x11111111, 32, 0xFFFFFFFF, false, false, true, false)]
        [InlineData(0x00001111, 0x11110000, 16, 0x11111111, false, false, false, false)]
        [InlineData(0xFFFF0000, 0xFFFF0000, 16, 0x0000FFFF, true, false, false, false)]
        [InlineData(0x7FFFFFFF, 0xFFFFFFFF, 1, 0xFFFFFFFF, false, true, true, false)]
        [InlineData(0x80000000, 0x00000000, 1, 0x00000000, true, true, false, true)]
        public void SHLD_EAX_EBX_CL(uint eaxValue, uint ebxValue, byte count, uint expectedValue, bool carryFlag, bool overflowFlag, bool signFlag, bool zeroFlag)
        {
            Reset();
            mbbsEmuCpuRegisters.EAX = eaxValue;
            mbbsEmuCpuRegisters.EBX = ebxValue;
            mbbsEmuCpuRegisters.CL = count;

            var instructions = new Assembler(16);
            instructions.shld(eax, ebx, cl);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            //Verify Results
            Assert.Equal(expectedValue, mbbsEmuCpuRegisters.EAX);
            Assert.Equal(carryFlag, mbbsEmuCpuRegisters.CarryFlag);
            Assert.Equal(overflowFlag, mbbsEmuCpuRegisters.OverflowFlag);
            Assert.Equal(signFlag, mbbsEmuCpuRegisters.SignFlag);
            Assert.Equal(zeroFlag, mbbsEmuCpuRegisters.ZeroFlag);
        }
    }
}
