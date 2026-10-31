using Iced.Intel;
using MBBSEmu.CPU;
using Xunit;
using static Iced.Intel.AssemblerRegisters;

namespace MBBSEmu.Tests.CPU
{
    public class FCLEX_Tests : CpuTestBase
    {
        [Fact]
        public void FCLEX_Test()
        {
            Reset();
            mbbsEmuCpuRegisters.Fpu.StatusWord = 0x3F; //Set all 6 Error Flags

            var instructions = new Assembler(16);
            instructions.fclex();
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();
            mbbsEmuCpuCore.Tick();

            Assert.Equal(0, mbbsEmuCpuRegisters.Fpu.StatusWord);
        }

        /// <summary>
        ///     FCLEX clears the flags of the status word only: after 1/0 (ZE) the status word is clear and the control
        ///     word keeps its exception masks (coprocessor: FNSTCW after FNCLEX gives 037F)
        /// </summary>
        [Fact]
        public void FCLEX_KeepsControlWord()
        {
            Reset();

            mbbsEmuCpuRegisters.Fpu.SetStackTop(1);
            mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackPointer(Register.ST0)] = 1d;
            mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackPointer(Register.ST1)] = 0d;

            var instructions = new Assembler(16);
            instructions.fdiv(st0, st1);
            instructions.fnclex();
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();
            Assert.Equal(0x0004, mbbsEmuCpuRegisters.Fpu.StatusWord & 0x3F);

            mbbsEmuCpuCore.Tick();
            Assert.Equal(0, mbbsEmuCpuRegisters.Fpu.StatusWord & 0x3F);
            Assert.Equal(0x37F, mbbsEmuCpuRegisters.Fpu.ControlWord);
        }
    }
}
