using Iced.Intel;
using System;
using Xunit;
using static Iced.Intel.AssemblerRegisters;

namespace MBBSEmu.Tests.CPU
{
    public class FLDCW_Tests : CpuTestBase
    {
        /// <summary>
        ///     Loads the FPU Control Word from Memory and sets the FPU Control Word
        ///
        ///     We'll verify this by not only setting the Control Word, but also verify
        ///     the Round Mode is set correctly
        ///
        ///     Rounding Flag Conversion:
        ///     Rounding Mode = (ControlWord >> 10) &amp; 0x3;
        ///     0 => MidpointRounding.ToEven
        ///     1 => MidpointRounding.ToNegativeInfinity
        ///     2 => MidpointRounding.ToPositiveInfinity
        ///     3 => MidpointRounding.ToZero
        /// </summary>
        [Theory]
        [InlineData(0x0000, MidpointRounding.ToEven)]
        [InlineData(0x0400, MidpointRounding.ToNegativeInfinity)]
        [InlineData(0x0800, MidpointRounding.ToPositiveInfinity)]
        [InlineData(0x0C00, MidpointRounding.ToZero)]
        public void FLDCW_Test(ushort controlWord, MidpointRounding roundingMode)
        {
            Reset();
            mbbsEmuCpuRegisters.Fpu.ControlWord = 0;
            CreateDataSegment(new ReadOnlySpan<byte>(), 2);
            mbbsEmuMemoryCore.SetArray(2, 0, [0, 0, 0]); //Set some junk data ahead of the actual address
            mbbsEmuMemoryCore.SetWord(2,3, controlWord);
            mbbsEmuCpuRegisters.DS = 2;

            var instructions = new Assembler(16);
            instructions.fldcw(__word_ptr[3]);
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(controlWord, mbbsEmuCpuRegisters.Fpu.ControlWord);
            Assert.Equal(roundingMode, mbbsEmuCpuRegisters.Fpu.GetRoundingControl());
        }
    }
}
