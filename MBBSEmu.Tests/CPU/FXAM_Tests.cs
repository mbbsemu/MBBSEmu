using Iced.Intel;
using MBBSEmu.CPU;
using Xunit;

namespace MBBSEmu.Tests.CPU
{
    public class FXAM_Tests : CpuTestBase
    {
        [Theory]
        [InlineData(1.0d, (ushort)EnumFpuStatusFlags.Code2)]
        [InlineData(-1.0d, (ushort)(EnumFpuStatusFlags.Code2 | EnumFpuStatusFlags.Code1))]
        [InlineData(0.0d, (ushort)EnumFpuStatusFlags.Code3)]
        [InlineData(-0.0d, (ushort)(EnumFpuStatusFlags.Code3 | EnumFpuStatusFlags.Code1))]
        [InlineData(double.Epsilon, (ushort)EnumFpuStatusFlags.Code2)] //A double denormal is a normal 80-bit value
        [InlineData(double.PositiveInfinity, (ushort)(EnumFpuStatusFlags.Code2 | EnumFpuStatusFlags.Code0))]
        [InlineData(double.NegativeInfinity, (ushort)(EnumFpuStatusFlags.Code2 | EnumFpuStatusFlags.Code1 | EnumFpuStatusFlags.Code0))]
        [InlineData(double.NaN, (ushort)(EnumFpuStatusFlags.Code1 | EnumFpuStatusFlags.Code0))]
        public void FXAM_Test(double ST0Value, ushort expectedFlags)
        {
            Reset();
            mbbsEmuCpuRegisters.Fpu.StatusWord = 0; //Manually Clear all Status Values
            mbbsEmuCpuRegisters.Fpu.SetFlag(EnumFpuStatusFlags.Code0 | EnumFpuStatusFlags.Code1 | EnumFpuStatusFlags.Code2 | EnumFpuStatusFlags.Code3);
            mbbsEmuCpuRegisters.Fpu.SetStackTop(0);
            mbbsEmuCpuCore.FpuStack[0] = ST0Value;

            var instructions = new Assembler(16);
            instructions.fxam();
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(expectedFlags, mbbsEmuCpuRegisters.Fpu.StatusWord);
            Assert.Equal(ST0Value, mbbsEmuCpuCore.FpuStack[0]);
        }

        [Theory]
        [InlineData(0x0000, 0x0000_0000_0000_0001, (ushort)(EnumFpuStatusFlags.Code3 | EnumFpuStatusFlags.Code2))] //Denormal
        [InlineData(0x8000, 0x0000_0000_0000_0001, (ushort)(EnumFpuStatusFlags.Code3 | EnumFpuStatusFlags.Code2 | EnumFpuStatusFlags.Code1))]
        [InlineData(0x4000, 0x4000_0000_0000_0000, (ushort)0)] //Unnormal: unsupported, C3/C2/C0 clear
        public void FXAM_Float80_Test(ushort signExponent, ulong significand, ushort expectedFlags)
        {
            Reset();
            mbbsEmuCpuRegisters.Fpu.StatusWord = 0;
            mbbsEmuCpuRegisters.Fpu.SetFlag(EnumFpuStatusFlags.Code0 | EnumFpuStatusFlags.Code1 | EnumFpuStatusFlags.Code2 | EnumFpuStatusFlags.Code3);
            mbbsEmuCpuRegisters.Fpu.SetStackTop(0);
            mbbsEmuCpuCore.FpuStack[0] = new Float80(signExponent, significand);

            var instructions = new Assembler(16);
            instructions.fxam();
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(expectedFlags, mbbsEmuCpuRegisters.Fpu.StatusWord);
        }
    }
}
