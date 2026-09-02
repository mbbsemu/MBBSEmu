using Iced.Intel;
using MBBSEmu.CPU;
using MBBSEmu.Extensions;
using System;
using Xunit;

namespace MBBSEmu.Tests.CPU
{
    public class FYL2X_Tests : CpuTestBase
    {
        [Theory]
        [InlineData(8d, 1d)]
        [InlineData(2d, 3d)]
        [InlineData(100d, 1d)]
        [InlineData(0d, 5d)]
        [InlineData(0d, -5d)]
        [InlineData(double.PositiveInfinity, 5d)]
        [InlineData(double.PositiveInfinity, -5d)]
        public void FYL2X_Test(double ST0Value, double ST1Value)
        {
            Reset();

            mbbsEmuCpuRegisters.Fpu.SetStackTop(1);
            mbbsEmuCpuCore.FpuStack[1] = ST0Value; //ST0
            mbbsEmuCpuCore.FpuStack[0] = ST1Value; //ST1

            var instructions = new Assembler(16);
            instructions.fyl2x();
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(ST1Value * Math.Log2(ST0Value), mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackTop()]);
        }

        [Theory]
        [InlineData(-4d, 5d)] // negative ST(0) is an invalid operand
        [InlineData(double.NaN, 5d)]
        [InlineData(4d, double.NaN)]
        [InlineData(0d, 0d)] // 0 * log2(0) == 0 * -Infinity, indeterminate
        [InlineData(double.PositiveInfinity, 0d)] // 0 * log2(+Infinity) == 0 * +Infinity, indeterminate
        public void FYL2X_InvalidOperand_ReturnsNaNAndSetsInvalidOperationFlag(double ST0Value, double ST1Value)
        {
            Reset();

            mbbsEmuCpuRegisters.Fpu.SetStackTop(1);
            mbbsEmuCpuCore.FpuStack[1] = ST0Value; //ST0
            mbbsEmuCpuCore.FpuStack[0] = ST1Value; //ST1

            var instructions = new Assembler(16);
            instructions.fyl2x();
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(double.NaN, mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackTop()]);
            Assert.True(mbbsEmuCpuRegisters.Fpu.ControlWord.IsFlagSet((ushort)EnumFpuControlWordFlags.InvalidOperation));
        }
    }
}
