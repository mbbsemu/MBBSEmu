using System;
using System.Collections.Generic;
using System.Text;
using Iced.Intel;
using Xunit;
using static Iced.Intel.AssemblerRegisters;

namespace MBBSEmu.Tests.CPU
{
    public class FSUBRP_Tests : CpuTestBase
    {
        [Theory]
        [InlineData(2, 1)]
        [InlineData(1, 2)]
        [InlineData(.5, 1.5)]
        public void FSUBRP_ST1_Test(float ST0Value, float ST1Value)
        {
            Reset();

            mbbsEmuCpuRegisters.Fpu.SetStackTop(1);
            mbbsEmuCpuCore.FpuStack[1] = ST0Value; //ST0
            mbbsEmuCpuCore.FpuStack[0] = ST1Value; //ST1

            var instructions = new Assembler(16);
            instructions.fsubrp(st1, st0);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            var expectedValue = ST0Value - ST1Value;

            Assert.Equal(expectedValue, mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackTop()]);
        }

        [Theory]
        [InlineData(2, 1)]
        [InlineData(1, 2)]
        [InlineData(.5, 1.5)]
        public void FSUBRP_STi_Test(float ST0Value, float STiValue)
        {
            Reset();

            mbbsEmuCpuRegisters.Fpu.SetStackTop(2);
            mbbsEmuCpuCore.FpuStack[2] = ST0Value; //ST0
            mbbsEmuCpuCore.FpuStack[0] = STiValue; //ST2

            var instructions = new Assembler(16);
            instructions.fsubrp(st2, st0);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            var expectedValue = ST0Value - STiValue;

            Assert.Equal(expectedValue, mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackTop()]);
        }
    }
}
