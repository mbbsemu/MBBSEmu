using Iced.Intel;
using System;
using Xunit;
using static Iced.Intel.AssemblerRegisters;

namespace MBBSEmu.Tests.CPU
{
    public class IDIV_Tests : CpuTestBase
    {
        [Theory]
        [InlineData(2, 2, 1, 0)]
        [InlineData(3, 2, 1, 1)]
        [InlineData(sbyte.MaxValue, sbyte.MaxValue, 1, 0)]
        [InlineData(0, sbyte.MaxValue, 0, 0)]
        [InlineData(sbyte.MaxValue, 0, 0, 0, true)]
        [InlineData(255, 2, 127, 1)]
        [InlineData(256, 2, 0, 0, false, true)]
        [InlineData(-255, -2, 127, -1)]
        [InlineData(-256, -2, 0, 0, false, true)]
        [InlineData(257, -2, -128, 1)]
        [InlineData(258, -2, 0, 0, false, true)]
        public void IDIV_Test_M8(short dividend, sbyte divisor, sbyte expectedQuotient, sbyte expectedRemainder, bool divideByZeroException = false, bool overflowException = false)
        {
            Reset();

            CreateDataSegment(new ReadOnlySpan<byte>(), 2);
            mbbsEmuMemoryCore.SetByte(2, 0, (byte)divisor);
            mbbsEmuCpuRegisters.DS = 2;
            mbbsEmuCpuRegisters.AX = (ushort)dividend;
            var instructions = new Assembler(16);
            instructions.idiv(__byte_ptr[0]);
            CreateCodeSegment(instructions);

            if (divideByZeroException)
                Assert.Throws<DivideByZeroException>(mbbsEmuCpuCore.Tick);
            else if (overflowException)
                Assert.Throws<OverflowException>(mbbsEmuCpuCore.Tick);
            else
            {
                mbbsEmuCpuCore.Tick();
                Assert.Equal(expectedQuotient, (sbyte)mbbsEmuCpuRegisters.AL);
                Assert.Equal(expectedRemainder, (sbyte)mbbsEmuCpuRegisters.AH);
            }
        }

        [Theory]
        [InlineData(2, 2, 1, 0)]
        [InlineData(3, 2, 1, 1)]
        [InlineData(sbyte.MaxValue, sbyte.MaxValue, 1, 0)]
        [InlineData(0, sbyte.MaxValue, 0, 0)]
        [InlineData(sbyte.MaxValue, 0, 0, 0, true)]
        [InlineData(255, 2, 127, 1)]
        [InlineData(256, 2, 0, 0, false, true)]
        [InlineData(-255, -2, 127, -1)]
        [InlineData(-256, -2, 0, 0, false, true)]
        [InlineData(257, -2, -128, 1)]
        [InlineData(258, -2, 0, 0, false, true)]
        public void IDIV_Test_R8(short dividend, sbyte divisor, sbyte expectedQuotient, sbyte expectedRemainder, bool divideByZeroException = false, bool overflowException = false)
        {
            Reset();

            mbbsEmuCpuRegisters.BL = (byte) divisor;
            mbbsEmuCpuRegisters.AX = (ushort)dividend;
            var instructions = new Assembler(16);
            instructions.idiv(bl);
            CreateCodeSegment(instructions);

            if (divideByZeroException)
                Assert.Throws<DivideByZeroException>(mbbsEmuCpuCore.Tick);
            else if (overflowException)
                Assert.Throws<OverflowException>(mbbsEmuCpuCore.Tick);
            else
            {
                mbbsEmuCpuCore.Tick();
                Assert.Equal(expectedQuotient, (sbyte)mbbsEmuCpuRegisters.AL);
                Assert.Equal(expectedRemainder, (sbyte)mbbsEmuCpuRegisters.AH);
            }
        }

        [Theory]
        [InlineData(2, 2, 1, 0)]
        [InlineData(3, 2, 1, 1)]
        [InlineData(short.MaxValue, short.MaxValue, 1, 0)]
        [InlineData(0, short.MaxValue, 0, 0)]
        [InlineData(short.MaxValue, 0, 0, 0, true)]
        [InlineData(65535, 2, 32767, 1)]
        [InlineData(65536, 2, 0, 0, false, true)]
        [InlineData(-65535, -2, 32767, -1)]
        [InlineData(-65536, -2, 0, 0, false, true)]
        [InlineData(65537, -2, -32768, 1)]
        [InlineData(65538, -2, 0, 0, false, true)]
        public void IDIV_Test_M16(int dividend, short divisor, short expectedQuotient, short expectedRemainder, bool divideByZeroException = false, bool overflowException = false)
        {
            Reset();

            CreateDataSegment(new ReadOnlySpan<byte>(), 2);
            mbbsEmuMemoryCore.SetWord(2, 0, (ushort)divisor);
            mbbsEmuCpuRegisters.DS = 2;
            mbbsEmuCpuRegisters.AX = (ushort)(dividend & 0xFFFF);
            mbbsEmuCpuRegisters.DX = (ushort)(dividend >> 16);
            var instructions = new Assembler(16);
            instructions.idiv(__word_ptr[0]);
            CreateCodeSegment(instructions);

            if (divideByZeroException)
                Assert.Throws<DivideByZeroException>(mbbsEmuCpuCore.Tick);
            else if (overflowException)
                Assert.Throws<OverflowException>(mbbsEmuCpuCore.Tick);
            else
            {
                mbbsEmuCpuCore.Tick();
                Assert.Equal(expectedQuotient, (short)mbbsEmuCpuRegisters.AX);
                Assert.Equal(expectedRemainder, (short)mbbsEmuCpuRegisters.DX);
            }
        }

        [Theory]
        [InlineData(2, 2, 1, 0)]
        [InlineData(3, 2, 1, 1)]
        [InlineData(short.MaxValue, short.MaxValue, 1, 0)]
        [InlineData(0, short.MaxValue, 0, 0)]
        [InlineData(short.MaxValue, 0, 0, 0, true)]
        [InlineData(65535, 2, 32767, 1)]
        [InlineData(65536, 2, 0, 0, false, true)]
        [InlineData(-65535, -2, 32767, -1)]
        [InlineData(-65536, -2, 0, 0, false, true)]
        [InlineData(65537, -2, -32768, 1)]
        [InlineData(65538, -2, 0, 0, false, true)]
        public void IDIV_Test_R16(int dividend, short divisor, short expectedQuotient, short expectedRemainder, bool divideByZeroException = false, bool overflowException = false)
        {
            Reset();

            mbbsEmuCpuRegisters.BX = (ushort)divisor;
            mbbsEmuCpuRegisters.AX = (ushort)(dividend & 0xFFFF);
            mbbsEmuCpuRegisters.DX = (ushort)(dividend >> 16);
            var instructions = new Assembler(16);
            instructions.idiv(bx);
            CreateCodeSegment(instructions);

            if (divideByZeroException)
                Assert.Throws<DivideByZeroException>(mbbsEmuCpuCore.Tick);
            else if (overflowException)
                Assert.Throws<OverflowException>(mbbsEmuCpuCore.Tick);
            else
            {
                mbbsEmuCpuCore.Tick();
                Assert.Equal(expectedQuotient, (short)mbbsEmuCpuRegisters.AX);
                Assert.Equal(expectedRemainder, (short)mbbsEmuCpuRegisters.DX);
            }
        }

        [Theory]
        [InlineData(2, 2, 1, 0)]
        [InlineData(3, 2, 1, 1)]
        [InlineData(short.MaxValue, short.MaxValue, 1, 0)]
        [InlineData(0, short.MaxValue, 0, 0)]
        [InlineData(short.MaxValue, 0, 0, 0, true)]
        [InlineData(4294967295, 2, 2147483647, 1)]
        [InlineData(4294967296, 2, 0, 0, false, true)]
        [InlineData(-4294967295, -2, 2147483647, -1)]
        [InlineData(-4294967296, -2, 0, 0, false, true)]
        [InlineData(4294967297, -2, -2147483648, 1)]
        [InlineData(4294967298, -2, 0, 0, false, true)]
        public void IDIV_Test_M32(long dividend, int divisor, int expectedQuotient, int expectedRemainder, bool divideByZeroException = false, bool overflowException = false)
        {
            Reset();

            CreateDataSegment(new ReadOnlySpan<byte>(), 2);
            mbbsEmuMemoryCore.SetDWord(2, 0, (uint)divisor);
            mbbsEmuCpuRegisters.DS = 2;
            mbbsEmuCpuRegisters.EAX = (uint)(dividend & 0xFFFFFFFF);
            mbbsEmuCpuRegisters.EDX = (uint)(dividend >> 32);
            var instructions = new Assembler(16);
            instructions.idiv(__dword_ptr[0]);
            CreateCodeSegment(instructions);

            if (divideByZeroException)
                Assert.Throws<DivideByZeroException>(mbbsEmuCpuCore.Tick);
            else if (overflowException)
                Assert.Throws<OverflowException>(mbbsEmuCpuCore.Tick);
            else
            {
                mbbsEmuCpuCore.Tick();
                Assert.Equal(expectedQuotient, (int)mbbsEmuCpuRegisters.EAX);
                Assert.Equal(expectedRemainder, (int)mbbsEmuCpuRegisters.EDX);
            }
        }

        [Theory]
        [InlineData(2, 2, 1, 0)]
        [InlineData(3, 2, 1, 1)]
        [InlineData(short.MaxValue, short.MaxValue, 1, 0)]
        [InlineData(0, short.MaxValue, 0, 0)]
        [InlineData(short.MaxValue, 0, 0, 0, true)]
        [InlineData(4294967295, 2, 2147483647, 1)]
        [InlineData(4294967296, 2, 0, 0, false, true)]
        [InlineData(-4294967295, -2, 2147483647, -1)]
        [InlineData(-4294967296, -2, 0, 0, false, true)]
        [InlineData(4294967297, -2, -2147483648, 1)]
        [InlineData(4294967298, -2, 0, 0, false, true)]
        public void IDIV_Test_R32(long dividend, int divisor, int expectedQuotient, int expectedRemainder, bool divideByZeroException = false, bool overflowException = false)
        {
            Reset();

            mbbsEmuCpuRegisters.EBX = (uint)divisor;
            mbbsEmuCpuRegisters.EAX = (uint)(dividend & 0xFFFFFFFF);
            mbbsEmuCpuRegisters.EDX = (uint)(dividend >> 32);
            var instructions = new Assembler(16);
            instructions.idiv(ebx);
            CreateCodeSegment(instructions);

            if (divideByZeroException)
                Assert.Throws<DivideByZeroException>(mbbsEmuCpuCore.Tick);
            else if (overflowException)
                Assert.Throws<OverflowException>(mbbsEmuCpuCore.Tick);
            else
            {
                mbbsEmuCpuCore.Tick();
                Assert.Equal(expectedQuotient, (int)mbbsEmuCpuRegisters.EAX);
                Assert.Equal(expectedRemainder, (int)mbbsEmuCpuRegisters.EDX);
            }
        }
    }
}
