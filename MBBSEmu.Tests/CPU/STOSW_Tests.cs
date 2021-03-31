using Iced.Intel;
using MBBSEmu.CPU;
using MBBSEmu.Extensions;
using Xunit;

namespace MBBSEmu.Tests.CPU
{
    public class STOSW_Tests : CpuTestBase
    {
        [Fact]
        public void SWOSW_Test()
        {
            Reset();
            mbbsEmuProtectedModeMemoryCore.AddSegment(2);
            mbbsEmuCpuRegisters.AX = 0xFFFF;
            mbbsEmuCpuRegisters.DS = 2;
            mbbsEmuCpuRegisters.ES = 2;
            mbbsEmuCpuRegisters.DI = 0;

            var instructions = new Assembler(16);

            instructions.stosw();
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(0xFFFF, mbbsEmuMemoryCore.GetWord(2,0));
        }

        [Fact]
        public void SWOSW_Rep_Test()
        {
            Reset();
            mbbsEmuProtectedModeMemoryCore.AddSegment(2);
            mbbsEmuCpuRegisters.AX = 0xFFFF;
            mbbsEmuCpuRegisters.DS = 2;
            mbbsEmuCpuRegisters.ES = 2;
            mbbsEmuCpuRegisters.DI = 0;
            mbbsEmuCpuRegisters.CX = 1;

            var instructions = new Assembler(16);

            instructions.rep.stosw();
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(0xFFFF, mbbsEmuMemoryCore.GetWord(2,0));
            Assert.Equal(0, mbbsEmuMemoryCore.GetWord(2, 2));
            Assert.Equal(0, mbbsEmuCpuRegisters.CX);
        }

        [Fact]
        public void SWOSW_Rep_DF_Test()
        {
            Reset();
            mbbsEmuProtectedModeMemoryCore.AddSegment(2);
            mbbsEmuCpuRegisters.AX = 0xFFFF;
            mbbsEmuCpuRegisters.DS = 2;
            mbbsEmuCpuRegisters.ES = 2;
            mbbsEmuCpuRegisters.DI = 4;
            mbbsEmuCpuRegisters.CX = 1;
            mbbsEmuCpuRegisters.DirectionFlag = true;
            var instructions = new Assembler(16);

            instructions.rep.stosw();
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(0, mbbsEmuMemoryCore.GetWord(2, 2));
            Assert.Equal(0xFFFF, mbbsEmuMemoryCore.GetWord(2, 4));
            Assert.Equal(2, mbbsEmuCpuRegisters.DI);
        }
    }
}
