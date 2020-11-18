using Iced.Intel;
using MBBSEmu.CPU;
using MBBSEmu.Extensions;
using Xunit;

namespace MBBSEmu.Tests.CPU
{
    public class LODSW_Tests : CpuTestBase
    {
        [Fact]
        public void LODSW_Test()
        {
            Reset();
            mbbsEmuMemoryCore.AddSegment(2);
            mbbsEmuMemoryCore.SetWord(2, 2, 0xFFFF);
            mbbsEmuCpuRegisters.AX = 0;
            mbbsEmuCpuRegisters.DS = 2;
            mbbsEmuCpuRegisters.SI = 2;

            var instructions = new Assembler(16);

            instructions.lodsw();
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(0xFFFF, mbbsEmuCpuRegisters.AX);
            Assert.Equal(4, mbbsEmuCpuRegisters.SI);
        }

        [Fact]
        public void LODSW_DF_Test()
        {
            Reset();
            mbbsEmuMemoryCore.AddSegment(2);
            mbbsEmuMemoryCore.SetWord(2, 2, 0xFFFF);
            mbbsEmuCpuRegisters.AX = 0;
            mbbsEmuCpuRegisters.DS = 2;
            mbbsEmuCpuRegisters.SI = 2;
            mbbsEmuCpuRegisters.F = mbbsEmuCpuRegisters.F.SetFlag((ushort) EnumFlags.DF);

            var instructions = new Assembler(16);

            instructions.lodsw();
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(0xFFFF, mbbsEmuCpuRegisters.AX);
            Assert.Equal(0, mbbsEmuCpuRegisters.SI);
        }
    }
}
