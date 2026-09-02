using Iced.Intel;
using MBBSEmu.CPU;
using MBBSEmu.Extensions;
using System;
using Xunit;

namespace MBBSEmu.Tests.CPU
{
    public class FYL2XP1_Tests : CpuTestBase
    {
        [Theory]
        [InlineData(0.25d, 1d)]
        [InlineData(-0.5d, 3d)]
        [InlineData(0d, 1d)]
        [InlineData(-1d, 5d)] // (ST0 + 1.0) == 0, ST1 nonzero -> signed Infinity, still valid
        public void FYL2XP1_Test(double ST0Value, double ST1Value)
        {
            Reset();

            mbbsEmuCpuRegisters.Fpu.SetStackTop(1);
            mbbsEmuCpuCore.FpuStack[1] = ST0Value; //ST0
            mbbsEmuCpuCore.FpuStack[0] = ST1Value; //ST1

            var instructions = new Assembler(16);
            instructions.fyl2xp1();
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(ST1Value * Math.Log2(ST0Value + 1.0), mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackTop()]);
        }

        [Theory]
        [InlineData(-4d, 5d)] // (ST0 + 1.0) negative is an invalid operand
        [InlineData(double.NaN, 5d)]
        [InlineData(4d, double.NaN)]
        [InlineData(-1d, 0d)] // (ST0 + 1.0) == 0, ST1 == 0 -> indeterminate
        public void FYL2XP1_InvalidOperand_ReturnsNaNAndSetsInvalidOperationFlag(double ST0Value, double ST1Value)
        {
            Reset();

            mbbsEmuCpuRegisters.Fpu.SetStackTop(1);
            mbbsEmuCpuCore.FpuStack[1] = ST0Value; //ST0
            mbbsEmuCpuCore.FpuStack[0] = ST1Value; //ST1

            var instructions = new Assembler(16);
            instructions.fyl2xp1();
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(double.NaN, mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackTop()]);
            Assert.True(mbbsEmuCpuRegisters.Fpu.ControlWord.IsFlagSet((ushort)EnumFpuControlWordFlags.InvalidOperation));
        }
    }
}
