using Iced.Intel;
using Xunit;

namespace MBBSEmu.Tests.CPU
{
    public class FPREM_Tests : CpuTestBase
    {
        [Theory]
        [InlineData(5.3d, 2d)]
        [InlineData(-5.3d, 2d)]
        [InlineData(10d, 3d)]
        public void FPREM_Test(double ST0Value, double ST1Value)
        {
            Reset();

            mbbsEmuCpuRegisters.Fpu.SetStackTop(1);
            mbbsEmuCpuCore.FpuStack[1] = ST0Value; //ST0
            mbbsEmuCpuCore.FpuStack[0] = ST1Value; //ST1

            var instructions = new Assembler(16);
            instructions.fprem();
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(ST0Value % ST1Value, mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackTop()]);
        }

        [Fact]
        public void FPREM_BorlandFmodSequence()
        {
            Reset();

            mbbsEmuCpuRegisters.Fpu.SetStackTop(1);
            mbbsEmuCpuCore.FpuStack[1] = 5.3d; // ST0
            mbbsEmuCpuCore.FpuStack[0] = 2d; // ST1

            // FPREM; FNSTSW AX; SAHF; JP back to FPREM
            CreateCodeSegment(new byte[] { 0xD9, 0xF8, 0xDF, 0xE0, 0x9E, 0x7A, 0xF9 });

            mbbsEmuCpuCore.Tick();
            mbbsEmuCpuCore.Tick();
            mbbsEmuCpuCore.Tick();
            mbbsEmuCpuCore.Tick();

            Assert.Equal(5.3d % 2d, mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackTop()]);
            Assert.False(mbbsEmuCpuRegisters.ParityFlag);
            Assert.Equal(7, mbbsEmuCpuRegisters.IP);
        }
    }
}
