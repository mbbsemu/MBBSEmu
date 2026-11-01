using Iced.Intel;
using MBBSEmu.CPU;
using System;
using System.Collections.Generic;
using Xunit;
using static Iced.Intel.AssemblerRegisters;

namespace MBBSEmu.Tests.CPU
{
    public class FSTP_Tests : CpuTestBase
    {
        [Theory]
        [InlineData(double.MaxValue)]
        [InlineData(double.MinValue)]
        [InlineData(0.0d)]
        [InlineData(double.NaN)]
        public void FSTP_Test_ST1(double ST0Value)
        {
            Reset();
            mbbsEmuCpuRegisters.Fpu.SetStackTop(1);
            mbbsEmuCpuCore.FpuStack[1] = ST0Value;

            var instructions = new Assembler(16);
            instructions.fstp(st1);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(ST0Value, mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackTop()]);
            Assert.Equal(0, mbbsEmuCpuRegisters.Fpu.GetStackTop());
        }

        [Theory]
        [InlineData(double.MaxValue)]
        [InlineData(double.MinValue)]
        [InlineData(0.0d)]
        [InlineData(double.NaN)]
        public void FSTP_Test_M32(double ST0Value)
        {
            Reset();
            CreateDataSegment(new ReadOnlySpan<byte>(), 2);
            mbbsEmuCpuRegisters.DS = 2;
            mbbsEmuCpuRegisters.Fpu.SetStackTop(0);
            mbbsEmuCpuCore.FpuStack[0] = ST0Value;

            var instructions = new Assembler(16);
            instructions.fstp(__dword_ptr[0]);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal((float)ST0Value, BitConverter.ToSingle(mbbsEmuMemoryCore.GetArray(2, 0, 4)));
            Assert.Equal(7, mbbsEmuCpuRegisters.Fpu.GetStackTop());
        }

        [Theory]
        [InlineData(double.MaxValue)]
        [InlineData(double.MinValue)]
        [InlineData(0.0d)]
        [InlineData(double.NaN)]
        public void FSTP_Test_M64(double ST0Value)
        {
            Reset();
            CreateDataSegment(new ReadOnlySpan<byte>(), 2);
            mbbsEmuCpuRegisters.DS = 2;
            mbbsEmuCpuRegisters.Fpu.SetStackTop(0);
            mbbsEmuCpuCore.FpuStack[0] = ST0Value;

            var instructions = new Assembler(16);
            instructions.fstp(__qword_ptr[0]);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(ST0Value, BitConverter.ToDouble(mbbsEmuMemoryCore.GetArray(2, 0, 8)));
            Assert.Equal(7, mbbsEmuCpuRegisters.Fpu.GetStackTop());
        }

        public static IEnumerable<object[]> M80TestData => new List<object[]>
        {
            new object[] { 0.0d, new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 } }, //+0.0
            new object[] { 1.0d, new byte[] { 0, 0, 0, 0, 0, 0, 0, 0x80, 0xFF, 0x3F } },
            new object[] { -1.0d, new byte[] { 0, 0, 0, 0, 0, 0, 0, 0x80, 0xFF, 0xBF } },
            new object[] { 2.0d, new byte[] { 0, 0, 0, 0, 0, 0, 0, 0x80, 0x00, 0x40 } },
            new object[] { 0.5d, new byte[] { 0, 0, 0, 0, 0, 0, 0, 0x80, 0xFE, 0x3F } },
        };

        [Theory]
        [MemberData(nameof(M80TestData))]
        public void FSTP_Test_M80(double ST0Value, byte[] expectedTbyteValue)
        {
            Reset();
            CreateDataSegment(new ReadOnlySpan<byte>(), 2);
            mbbsEmuCpuRegisters.DS = 2;
            mbbsEmuCpuRegisters.Fpu.SetStackTop(0);
            mbbsEmuCpuCore.FpuStack[0] = ST0Value;

            var instructions = new Assembler(16);
            instructions.fstp(__tbyte_ptr[0]);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(expectedTbyteValue, mbbsEmuMemoryCore.GetArray(2, 0, 10));
            Assert.Equal(7, mbbsEmuCpuRegisters.Fpu.GetStackTop());
        }

        /// <summary>
        ///     FLD m80 and FSTP m80 copy all 80 bits, whatever they encode: a 64-bit significand, a signalling NaN,
        ///     an unnormal, a pseudo-denormal, a negative denormal, a pseudo-infinity, the real indefinite
        ///     (coprocessor: each comes back unchanged, no exception)
        /// </summary>
        [Theory]
        [InlineData("4000C90FDAA22168C235")]
        [InlineData("7FFFA000000000000001")]
        [InlineData("3FFF4000000000000000")]
        [InlineData("00008000000000000000")]
        [InlineData("80000000000000000001")]
        [InlineData("7FFF0000000000000000")]
        [InlineData("FFFFC000000000000000")]
        public void FLD_FSTP_M80_RoundTrip(string value)
        {
            Reset();

            var bytes = Convert.FromHexString(value);
            Array.Reverse(bytes);
            CreateDataSegment(bytes, 2);
            mbbsEmuCpuRegisters.DS = 2;

            var instructions = new Assembler(16);
            instructions.fld(__tbyte_ptr[0]);
            instructions.fstp(__tbyte_ptr[10]);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();
            mbbsEmuCpuCore.Tick();

            Assert.Equal(bytes, mbbsEmuMemoryCore.GetArray(2, 10, 10).ToArray());
            Assert.Equal(0, mbbsEmuCpuRegisters.Fpu.StatusWord & 0x3F);
        }
    }
}
