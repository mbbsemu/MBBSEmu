using Iced.Intel;
using MBBSEmu.CPU;
using MBBSEmu.Extensions;
using Xunit;

namespace MBBSEmu.Tests.CPU
{
    public class STOSB_Tests : CpuTestBase
    {
        [Fact]
        public void SWOSB_Test()
        {
            Reset();
            mbbsEmuProtectedModeMemoryCore.AddSegment(2);
            mbbsEmuCpuRegisters.AL = 0xFF;
            mbbsEmuCpuRegisters.DS = 2;
            mbbsEmuCpuRegisters.ES = 2;
            mbbsEmuCpuRegisters.DI = 0;

            var instructions = new Assembler(16);

            instructions.stosb();
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(0xFF, mbbsEmuMemoryCore.GetByte(2, 0));
        }

        [Fact]
        public void SWOSB_Rep_Test()
        {
            Reset();
            mbbsEmuProtectedModeMemoryCore.AddSegment(2);
            mbbsEmuCpuRegisters.AL = 0xFF;
            mbbsEmuCpuRegisters.DS = 2;
            mbbsEmuCpuRegisters.ES = 2;
            mbbsEmuCpuRegisters.DI = 0;
            mbbsEmuCpuRegisters.CX = 1;

            var instructions = new Assembler(16);

            instructions.rep.stosb();
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(0xFF, mbbsEmuMemoryCore.GetByte(2, 0));
            Assert.Equal(0xFF, mbbsEmuMemoryCore.GetByte(2, 1));
            Assert.Equal(0, mbbsEmuMemoryCore.GetByte(2, 2));
            Assert.Equal(2, mbbsEmuCpuRegisters.DI);
            Assert.Equal(0, mbbsEmuCpuRegisters.CX);
        }

        [Fact]
        public void SWOSB_Rep_DF_Test()
        {
            Reset();
            mbbsEmuProtectedModeMemoryCore.AddSegment(2);
            mbbsEmuCpuRegisters.AX = 0xFF;
            mbbsEmuCpuRegisters.DS = 2;
            mbbsEmuCpuRegisters.ES = 2;
            mbbsEmuCpuRegisters.DI = 2;
            mbbsEmuCpuRegisters.CX = 1;
            mbbsEmuCpuRegisters.F = mbbsEmuCpuRegisters.F.SetFlag((ushort)EnumFlags.DF);
            var instructions = new Assembler(16);

            instructions.rep.stosb();
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(0, mbbsEmuMemoryCore.GetByte(2, 0));
            Assert.Equal(0xFF, mbbsEmuMemoryCore.GetByte(2, 1));
            Assert.Equal(0xFF, mbbsEmuMemoryCore.GetByte(2, 2));
            Assert.Equal(0, mbbsEmuCpuRegisters.DI);
            Assert.Equal(0, mbbsEmuCpuRegisters.CX);
        }
    }
}
