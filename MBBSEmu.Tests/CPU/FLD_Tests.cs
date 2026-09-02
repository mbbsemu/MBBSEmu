using Iced.Intel;
using System;
using System.Collections.Generic;
using Xunit;
using static Iced.Intel.AssemblerRegisters;

namespace MBBSEmu.Tests.CPU
{
    public class FLD_Tests : CpuTestBase
    {

        [Theory]
        [InlineData(0.0f)]
        [InlineData(float.MaxValue)]
        [InlineData(float.MinValue)]
        public void FLD_Test_M32(float valueToLoad)
        {
            Reset();

            CreateDataSegment(new ReadOnlySpan<byte>(), 2);
            mbbsEmuMemoryCore.SetArray(2, 0, BitConverter.GetBytes(valueToLoad));
            mbbsEmuCpuRegisters.DS = 2;

            var instructions = new Assembler(16);
            instructions.fld(__dword_ptr[0]);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(valueToLoad, mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackTop()]);
        }

        [Theory]
        [InlineData(1, 2)]
        [InlineData(float.MaxValue, float.MinValue)]
        public void FLD_Multiple_Test_M32(float st0, float st1)
        {
            Reset();

            CreateDataSegment(new ReadOnlySpan<byte>(), 2);
            mbbsEmuMemoryCore.SetArray(2, 0, BitConverter.GetBytes(st1)); //ST(0)
            mbbsEmuMemoryCore.SetArray(2, 4, BitConverter.GetBytes(st0)); //ST(0)->ST(1), new ST(0)
            mbbsEmuCpuRegisters.DS = 2;

            var instructions = new Assembler(16);
            instructions.fld(__dword_ptr[0]);
            instructions.fld(__dword_ptr[4]);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();
            mbbsEmuCpuCore.Tick();

            Assert.Equal(st0, mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackTop()]);
            Assert.Equal(st1, mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackPointer(Register.ST1)]);
        }

        [Theory]
        [InlineData(0.0f)]
        [InlineData(double.MaxValue)]
        [InlineData(double.MinValue)]
        public void FLD_Test_M64(double valueToLoad)
        {
            Reset();

            CreateDataSegment(new ReadOnlySpan<byte>(), 2);
            mbbsEmuMemoryCore.SetArray(2, 0, BitConverter.GetBytes(valueToLoad));
            mbbsEmuCpuRegisters.DS = 2;

            var instructions = new Assembler(16);
            instructions.fld(__qword_ptr[0]);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(valueToLoad, mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackTop()]);
        }

        [Theory]
        [InlineData(1, 2)]
        [InlineData(double.MaxValue, double.MinValue)]
        public void FLD_Multiple_Test_M64(double st0, double st1)
        {
            Reset();

            CreateDataSegment(new ReadOnlySpan<byte>(), 2);
            mbbsEmuMemoryCore.SetArray(2, 0, BitConverter.GetBytes(st1)); //ST(0)
            mbbsEmuMemoryCore.SetArray(2, 8, BitConverter.GetBytes(st0)); //ST(0)->ST(1), new ST(0)
            mbbsEmuCpuRegisters.DS = 2;

            var instructions = new Assembler(16);
            instructions.fld(__qword_ptr[0]);
            instructions.fld(__qword_ptr[8]);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();
            mbbsEmuCpuCore.Tick();

            Assert.Equal(st0, mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackTop()]);
            Assert.Equal(st1, mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackPointer(Register.ST1)]);
        }

        public static IEnumerable<object[]> M80TestData => new List<object[]>
        {
            new object[] { new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, 0.0d }, //+0.0
            new object[] { new byte[] { 0, 0, 0, 0, 0, 0, 0, 0x80, 0xFF, 0x3F }, 1.0d },
            new object[] { new byte[] { 0, 0, 0, 0, 0, 0, 0, 0x80, 0xFF, 0xBF }, -1.0d },
            new object[] { new byte[] { 0, 0, 0, 0, 0, 0, 0, 0x80, 0x00, 0x40 }, 2.0d },
            new object[] { new byte[] { 0, 0, 0, 0, 0, 0, 0, 0x80, 0xFE, 0x3F }, 0.5d },
        };

        [Theory]
        [MemberData(nameof(M80TestData))]
        public void FLD_Test_M80(byte[] tbyteValue, double expectedValue)
        {
            Reset();

            CreateDataSegment(new ReadOnlySpan<byte>(), 2);
            mbbsEmuMemoryCore.SetArray(2, 0, tbyteValue);
            mbbsEmuCpuRegisters.DS = 2;

            var instructions = new Assembler(16);
            instructions.fld(__tbyte_ptr[0]);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(expectedValue, mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackTop()]);
        }

        [Theory]
        [InlineData(0.0d)]
        [InlineData(1.0d)]
        [InlineData(-1.0d)]
        [InlineData(double.MaxValue)]
        [InlineData(double.MinValue)]
        [InlineData(double.Epsilon)]
        [InlineData(double.PositiveInfinity)]
        [InlineData(double.NegativeInfinity)]
        [InlineData(double.NaN)]
        [InlineData(1e32)]
        [InlineData(-1e300)]
        [InlineData(Math.PI)]
        public void FLD_FSTP_M80_RoundTrip(double value)
        {
            Reset();

            CreateDataSegment(new ReadOnlySpan<byte>(), 2);
            mbbsEmuCpuRegisters.DS = 2;
            mbbsEmuCpuRegisters.Fpu.SetStackTop(0);
            mbbsEmuCpuCore.FpuStack[0] = value;

            var instructions = new Assembler(16);
            instructions.fstp(__tbyte_ptr[0]);
            instructions.fld(__tbyte_ptr[0]);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();
            mbbsEmuCpuCore.Tick();

            Assert.Equal(value, mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackTop()]);
        }

        [Theory]
        [InlineData(42f)]
        [InlineData(double.MinValue)]
        [InlineData(double.MaxValue)]
        public void FLD_Test_STi(float initialRegisterValue)
        {
            Reset();

            mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackPointer(Register.ST1)] = initialRegisterValue;

            var instructions = new Assembler(16);
            instructions.fld(st1);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(initialRegisterValue, mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackTop()]);
        }

        [Theory]
        [InlineData(42f)]
        [InlineData(double.MinValue)]
        [InlineData(double.MaxValue)]
        public void FLD_Test_ST0(float initialRegisterValue)
        {
            Reset();

            mbbsEmuCpuRegisters.Fpu.PushStackTop();
            mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackPointer(Register.ST0)] = initialRegisterValue;

            var instructions = new Assembler(16);
            instructions.fld(st0);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(initialRegisterValue, mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackPointer(Register.ST0)]);
            Assert.Equal(initialRegisterValue, mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackPointer(Register.ST1)]);
        }
    }
}
