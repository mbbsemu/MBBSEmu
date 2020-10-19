using Iced.Intel;
using System;
using Xunit;
using static Iced.Intel.AssemblerRegisters;

namespace MBBSEmu.Tests.CPU
{
    public class DIV_Tests : CpuTestBase
    {
        [Theory]
        [InlineData(2, 2, 1, 0)]
        [InlineData(3, 2, 1, 1)]
        [InlineData(byte.MaxValue, byte.MaxValue, 1, 0)]
        [InlineData(0, byte.MaxValue, 0, 0)]
        [InlineData(byte.MaxValue, 0, 0, 0, true)]
        [InlineData(ushort.MaxValue, byte.MaxValue, 0, 0, false, true)]
        [InlineData(32767, byte.MaxValue, 128, 127)]
        public void DIV_Test_M8(ushort dividend, byte divisor, byte expectedQuotient, byte expectedRemainder, bool divideByZeroException = false, bool overflowException = false)
        {
            Reset();

            CreateDataSegment(new ReadOnlySpan<byte>(), 2);
            mbbsEmuMemoryCore.SetByte(2, 0, divisor);
            mbbsEmuCpuRegisters.DS = 2;
            mbbsEmuCpuRegisters.AX = dividend;
            var instructions = new Assembler(16);
            instructions.div(__byte_ptr[0]);
            CreateCodeSegment(instructions);

            if (divideByZeroException)
                Assert.Throws<DivideByZeroException>(mbbsEmuCpuCore.Tick);
            else if (overflowException)
                Assert.Throws<OverflowException>(mbbsEmuCpuCore.Tick);
            else
            {
                mbbsEmuCpuCore.Tick();
                Assert.Equal(expectedQuotient, mbbsEmuCpuRegisters.AL);
                Assert.Equal(expectedRemainder, mbbsEmuCpuRegisters.AH);
            }
        }

        [Theory]
        [InlineData(2, 2, 1, 0)]
        [InlineData(3, 2, 1, 1)]
        [InlineData(byte.MaxValue, byte.MaxValue, 1, 0)]
        [InlineData(0, byte.MaxValue, 0, 0)]
        [InlineData(byte.MaxValue, 0, 0, 0, true)]
        [InlineData(ushort.MaxValue, byte.MaxValue, 0, 0, false, true)]
        [InlineData(32767, byte.MaxValue, 128, 127)]
        public void DIV_Test_R8(ushort dividend, byte divisor, byte expectedQuotient, byte expectedRemainder, bool divideByZeroException = false, bool overflowException = false)
        {
            Reset();
            mbbsEmuCpuRegisters.AX = dividend;
            mbbsEmuCpuRegisters.BL = divisor;
            var instructions = new Assembler(16);
            instructions.div(bl);
            CreateCodeSegment(instructions);

            if (divideByZeroException)
                Assert.Throws<DivideByZeroException>(mbbsEmuCpuCore.Tick);
            else if (overflowException)
                Assert.Throws<OverflowException>(mbbsEmuCpuCore.Tick);
            else
            {
                mbbsEmuCpuCore.Tick();
                Assert.Equal(expectedQuotient, mbbsEmuCpuRegisters.AL);
                Assert.Equal(expectedRemainder, mbbsEmuCpuRegisters.AH);
            }
        }

        [Theory]
        [InlineData(2, 2, 1, 0)]
        [InlineData(3, 2, 1, 1)]
        [InlineData(ushort.MaxValue, ushort.MaxValue, 1, 0)]
        [InlineData(0, ushort.MaxValue, 0, 0)]
        [InlineData(ushort.MaxValue, 0, 0, 0, true)]
        [InlineData(uint.MaxValue, ushort.MaxValue, 0, 0, false, true)]
        [InlineData(2147483647, ushort.MaxValue, 32768, 32767)]
        public void DIV_Test_M16(uint dividend, ushort divisor, ushort expectedQuotient, ushort expectedRemainder, bool divideByZeroException = false, bool overflowException = false)
        {
            Reset();

            CreateDataSegment(new ReadOnlySpan<byte>(), 2);
            mbbsEmuMemoryCore.SetWord(2, 0, divisor);
            mbbsEmuCpuRegisters.DS = 2;
            mbbsEmuCpuRegisters.AX = (ushort)(dividend & 0xFFFF);
            mbbsEmuCpuRegisters.DX = (ushort)(dividend >> 16);
            var instructions = new Assembler(16);
            instructions.div(__word_ptr[0]);
            CreateCodeSegment(instructions);

            if (divideByZeroException)
                Assert.Throws<DivideByZeroException>(mbbsEmuCpuCore.Tick);
            else if (overflowException)
                Assert.Throws<OverflowException>(mbbsEmuCpuCore.Tick);
            else
            {
                mbbsEmuCpuCore.Tick();
                Assert.Equal(expectedQuotient, mbbsEmuCpuRegisters.AX);
                Assert.Equal(expectedRemainder, mbbsEmuCpuRegisters.DX);
            }
        }

        [Theory]
        [InlineData(2, 2, 1, 0)]
        [InlineData(3, 2, 1, 1)]
        [InlineData(ushort.MaxValue, ushort.MaxValue, 1, 0)]
        [InlineData(0, ushort.MaxValue, 0, 0)]
        [InlineData(ushort.MaxValue, 0, 0, 0, true)]
        [InlineData(uint.MaxValue, ushort.MaxValue, 0, 0, false, true)]
        [InlineData(2147483647, ushort.MaxValue, 32768, 32767)]
        public void DIV_Test_R16(uint dividend, ushort divisor, ushort expectedQuotient, ushort expectedRemainder, bool divideByZeroException = false, bool overflowException = false)
        {
            Reset();

            mbbsEmuCpuRegisters.BX = divisor;
            mbbsEmuCpuRegisters.AX = (ushort)(dividend & 0xFFFF);
            mbbsEmuCpuRegisters.DX = (ushort)(dividend >> 16);
            var instructions = new Assembler(16);
            instructions.div(bx);
            CreateCodeSegment(instructions);

            if (divideByZeroException)
                Assert.Throws<DivideByZeroException>(mbbsEmuCpuCore.Tick);
            else if (overflowException)
                Assert.Throws<OverflowException>(mbbsEmuCpuCore.Tick);
            else
            {
                mbbsEmuCpuCore.Tick();
                Assert.Equal(expectedQuotient, mbbsEmuCpuRegisters.AX);
                Assert.Equal(expectedRemainder, mbbsEmuCpuRegisters.DX);
            }
        }
    }
}
