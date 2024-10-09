using Iced.Intel;
using Xunit;

namespace MBBSEmu.Tests.CPU
{
    public class FRNDINT_Tests : CpuTestBase
    {
        /// <summary>
        ///     Tests the FRNDINT Instruction which rounds the value in ST(0) to the nearest integer
        ///
        ///     We'll test this by setting the FPU Control Mode to Round to different modes and verify.
        /// 
        ///     Rounding Flag Conversion:
        ///     Rounding Mode = (ControlWord >> 10) &amp; 0x3;
        ///     0 => MidpointRounding.ToEven
        ///     1 => MidpointRounding.ToNegativeInfinity
        ///     2 => MidpointRounding.ToPositiveInfinity
        ///     3 => MidpointRounding.ToZero
        /// </summary>
        /// <param name="ST0Value"></param>
        /// <param name="expectedValue"></param>
        [Theory]
        //Round to Even
        [InlineData(1.5, 2.0, 0x0000)]
        [InlineData(-1.5, -2.0, 0x0000)]
        //Round to Negative Infinity
        [InlineData(1.5, 1.0, 0x0400)]
        [InlineData(-1.5, -2.0, 0x0400)]
        //Round to Positive Infinity
        [InlineData(1.5, 2.0, 0x0800)]
        [InlineData(-1.5, -1.0, 0x0800)]
        //Round to Zero
        [InlineData(1.5, 1.0, 0x0C00)]
        [InlineData(-1.5, -1.0, 0x0C00)]
        public void FRNDINT_Test(double ST0Value, double expectedValue, ushort controlWord)
        {
            Reset();
            mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackTop()] = ST0Value;

            //Set FPU Control Word to set rounding to even
            mbbsEmuCpuRegisters.Fpu.ControlWord = controlWord;

            var instructions = new Assembler(16);
            instructions.frndint();
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(expectedValue, mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackTop()]);
        }
    }
}
