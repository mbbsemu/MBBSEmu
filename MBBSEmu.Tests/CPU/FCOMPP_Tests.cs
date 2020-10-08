using Iced.Intel;
using MBBSEmu.CPU;
using Xunit;

namespace MBBSEmu.Tests.CPU
{
    public class FCOMPP_Tests : CpuTestBase
    {
        private readonly ushort FPU_CODE_MASK = (ushort) (EnumFpuStatusFlags.Code0 | EnumFpuStatusFlags.Code1 |
                                                          EnumFpuStatusFlags.Code2 | EnumFpuStatusFlags.Code3);

        [Theory]
        [InlineData(double.MaxValue, 0d, 0)]
        [InlineData(double.MinValue, 0d, (ushort)EnumFpuStatusFlags.Code0)]
        [InlineData(1d, 1d, (ushort)EnumFpuStatusFlags.Code3)]
        [InlineData(double.NaN, 0d, (ushort)(EnumFpuStatusFlags.Code0 | EnumFpuStatusFlags.Code2 | EnumFpuStatusFlags.Code3))]
        [InlineData(0d, double.NaN, (ushort)(EnumFpuStatusFlags.Code0 | EnumFpuStatusFlags.Code2 | EnumFpuStatusFlags.Code3))]
        public void FCOMPP_Test(double ST0Value, double ST1Value, ushort expectedFlags)
        {
            Reset();
            mbbsEmuCpuRegisters.Fpu.StatusWord = 0; //Manually Clear all Status Values
            mbbsEmuCpuRegisters.Fpu.SetStackTop(1);
            mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackPointer(Register.ST0)] = ST0Value;
            mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackPointer(Register.ST1)] = ST1Value;

            var instructions = new Assembler(16);
            instructions.fcompp();
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            var actualFPUCodes = mbbsEmuCpuRegisters.Fpu.StatusWord & FPU_CODE_MASK;

            Assert.Equal(expectedFlags, actualFPUCodes);
        }
    }
}
