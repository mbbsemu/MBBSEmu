using Iced.Intel;
using System.IO;
using Xunit;
using static Iced.Intel.AssemblerRegisters;

namespace MBBSEmu.Tests.CPU
{
    /// <summary>
    ///     Borland/Microsoft emit INT 34h-3Dh in place of x87 instructions; the CPU rewrites them
    ///     in place to the real instruction and executes that instead
    /// </summary>
    public class FpuEmulatorInterrupt_Tests : CpuTestBase
    {
        [Fact]
        public void INT3Bh_Is_Patched_To_FNSTSW_AX()
        {
            Reset();
            mbbsEmuCpuRegisters.Fpu.StatusWord = 0x0021;

            //CD 3B E0 => 9B DF E0 (FWAIT; FNSTSW AX)
            var instructions = new Assembler(16);
            instructions.@int(0x3B);
            instructions.db(0xE0);
            CreatePatchableCodeSegment(instructions);

            mbbsEmuCpuCore.Tick(); //INT 3Bh: patches, doesn't advance
            Assert.Equal(0, mbbsEmuCpuRegisters.IP);
            Assert.Equal(0x9B, mbbsEmuMemoryCore.GetByte(mbbsEmuCpuRegisters.CS, 0));
            Assert.Equal(0xDF, mbbsEmuMemoryCore.GetByte(mbbsEmuCpuRegisters.CS, 1));

            mbbsEmuCpuCore.Tick(); //FWAIT
            mbbsEmuCpuCore.Tick(); //FNSTSW AX

            Assert.Equal(0x0021, mbbsEmuCpuRegisters.AX);
        }

        [Fact]
        public void INT3Dh_Is_Patched_To_NOP_FWAIT()
        {
            Reset();

            var instructions = new Assembler(16);
            instructions.@int(0x3D);
            instructions.mov(ax, 1);
            CreatePatchableCodeSegment(instructions);

            mbbsEmuCpuCore.Tick(); //INT 3Dh: patches
            mbbsEmuCpuCore.Tick(); //NOP
            mbbsEmuCpuCore.Tick(); //FWAIT
            mbbsEmuCpuCore.Tick(); //MOV AX, 1

            Assert.Equal(0x90, mbbsEmuMemoryCore.GetByte(mbbsEmuCpuRegisters.CS, 0));
            Assert.Equal(0x9B, mbbsEmuMemoryCore.GetByte(mbbsEmuCpuRegisters.CS, 1));
            Assert.Equal(1, mbbsEmuCpuRegisters.AX);
        }

        /// <summary>
        ///     Module code segments loaded from an NE file have backing bytes as well as decoded
        ///     instructions; the patch writes to those bytes, so give the test segment both.
        /// </summary>
        private void CreatePatchableCodeSegment(Assembler instructions)
        {
            var stream = new MemoryStream();
            instructions.Assemble(new StreamCodeWriter(stream), 0);
            CreateDataSegment(stream.ToArray(), 1);
            CreateCodeSegment(stream.ToArray(), 1);
        }
    }
}
