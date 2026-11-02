using Iced.Intel;
using MBBSEmu.CPU;
using System;
using Xunit;
using static Iced.Intel.AssemblerRegisters;

namespace MBBSEmu.Tests.CPU
{
    public class FBSTP_Tests : CpuTestBase
    {
        [Theory]
        [InlineData(0d, "00000000000000000000", 0)]
        [InlineData(7d, "07000000000000000000", 0)]
        [InlineData(1234567890d, "90785634120000000000", 0)]
        [InlineData(-42d, "42000000000000000080", 0)]
        [InlineData(12.5d, "12000000000000000000", 0)] //Rounds to nearest even
        [InlineData(13.5d, "14000000000000000000", 0)]
        [InlineData(999999999999999872d, "72989999999999999900", 0)] //Largest double below 10^18
        [InlineData(1e18d, "00000000000000C0FFFF", 1)]
        [InlineData(-1e18d, "00000000000000C0FFFF", 1)]
        [InlineData(1e19d, "00000000000000C0FFFF", 1)] //Past 64 bits
        [InlineData(double.PositiveInfinity, "00000000000000C0FFFF", 1)]
        [InlineData(double.NaN, "00000000000000C0FFFF", 1)]
        public void FBSTP_Test(double ST0Value, string expectedHex, ushort expectedInvalidOperation)
        {
            Reset();
            mbbsEmuProtectedModeMemoryCore.AddSegment(2);
            mbbsEmuCpuRegisters.DS = 2;
            mbbsEmuCpuRegisters.Fpu.StatusWord = 0;
            mbbsEmuCpuRegisters.Fpu.SetStackTop(1);
            mbbsEmuCpuCore.FpuStack[1] = ST0Value;

            var instructions = new Assembler(16);
            instructions.fbstp(__tword_ptr[0]);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(expectedHex, Convert.ToHexString(mbbsEmuMemoryCore.GetArray(2, 0, 10)));
            Assert.Equal(0, mbbsEmuCpuRegisters.Fpu.GetStackTop());
            Assert.Equal(expectedInvalidOperation, mbbsEmuCpuRegisters.Fpu.StatusWord & (ushort)EnumFpuStatusFlags.InvalidOperationException);
        }
    }
}
