using Iced.Intel;
using MBBSEmu.CPU;
using Xunit;

namespace MBBSEmu.Tests.CPU
{
    public class FTST_Tests : CpuTestBase
    {
        [Theory]
        [InlineData(1.0d, 0)]
        [InlineData(double.MaxValue, 0)]
        [InlineData(-1.0d, (ushort)(0 | EnumFpuStatusFlags.Code0))]
        [InlineData(double.MinValue, (ushort)(0 | EnumFpuStatusFlags.Code0))]
        [InlineData(0.0d, (ushort)(0 | EnumFpuStatusFlags.Code3))]
        [InlineData(double.NaN, (ushort)(0 | EnumFpuStatusFlags.Code3 | EnumFpuStatusFlags.Code2 | EnumFpuStatusFlags.Code0))]
        public void FTST_Test(double ST0Value, ushort expectedFlags)
        {
            Reset();
            mbbsEmuCpuRegisters.Fpu.StatusWord = 0; //Manually Clear all Status Values
            mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackTop()] = ST0Value;

            var instructions = new Assembler(16);
            instructions.ftst();
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(expectedFlags, mbbsEmuCpuRegisters.Fpu.StatusWord);
        }
    }
}
