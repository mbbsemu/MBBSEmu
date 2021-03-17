using Iced.Intel;
using MBBSEmu.CPU;
using MBBSEmu.Extensions;
using Xunit;

namespace MBBSEmu.Tests.CPU
{
    public class MOVSW_Tests : CpuTestBase
    {
        [Fact]
        public void MOVSW_Test()
        {
            Reset();
            mbbsEmuProtectedModeMemoryCore.AddSegment(2);
            mbbsEmuProtectedModeMemoryCore.AddSegment(3);
            mbbsEmuCpuRegisters.DS = 2;
            mbbsEmuCpuRegisters.SI = 0;
            mbbsEmuCpuRegisters.ES = 3;
            mbbsEmuCpuRegisters.DI = 0;
            mbbsEmuMemoryCore.SetWord(mbbsEmuCpuRegisters.DS, mbbsEmuCpuRegisters.SI, 0xFFFF);
            mbbsEmuMemoryCore.SetWord(mbbsEmuCpuRegisters.ES, mbbsEmuCpuRegisters.DI, 0x0);

            var instructions = new Assembler(16);

            instructions.movsw();
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(0xFFFF, mbbsEmuMemoryCore.GetWord(mbbsEmuCpuRegisters.ES, 0));
            Assert.Equal(2, mbbsEmuCpuRegisters.SI);
            Assert.Equal(2, mbbsEmuCpuRegisters.DI);
        }

        [Fact]
        public void MOVSW_Rep_Test()
        {
            Reset();
            mbbsEmuProtectedModeMemoryCore.AddSegment(2);
            mbbsEmuProtectedModeMemoryCore.AddSegment(3);
            mbbsEmuCpuRegisters.DS = 2;
            mbbsEmuCpuRegisters.SI = 0;
            mbbsEmuCpuRegisters.ES = 3;
            mbbsEmuCpuRegisters.DI = 0;
            mbbsEmuCpuRegisters.CX = 10;
            mbbsEmuMemoryCore.FillArray(mbbsEmuCpuRegisters.DS, 0, 0xFF, 0xFF);
            mbbsEmuMemoryCore.FillArray(mbbsEmuCpuRegisters.ES, 0, 0xFF, 0x0);

            var instructions = new Assembler(16);

            instructions.rep.movsw();
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            //Verify the 10 Words were copies
            for (ushort i = 0; i < 20; i+= 2)
                Assert.Equal(0xFFFF, mbbsEmuMemoryCore.GetWord(mbbsEmuCpuRegisters.ES, i));

            Assert.Equal(0, mbbsEmuMemoryCore.GetWord(mbbsEmuCpuRegisters.ES, 22));

            Assert.Equal(0, mbbsEmuCpuRegisters.CX);
            Assert.Equal(22, mbbsEmuCpuRegisters.SI);
            Assert.Equal(22, mbbsEmuCpuRegisters.DI);
        }

        [Fact]
        public void MOVSW_Rep_DF_Test()
        {
            Reset();
            mbbsEmuProtectedModeMemoryCore.AddSegment(2);
            mbbsEmuProtectedModeMemoryCore.AddSegment(3);
            mbbsEmuCpuRegisters.DS = 2;
            mbbsEmuCpuRegisters.SI = 22;
            mbbsEmuCpuRegisters.ES = 3;
            mbbsEmuCpuRegisters.DI = 22;
            mbbsEmuCpuRegisters.CX = 10;
            mbbsEmuCpuRegisters.F = mbbsEmuCpuRegisters.F.SetFlag((ushort) EnumFlags.DF);

            mbbsEmuMemoryCore.FillArray(mbbsEmuCpuRegisters.DS, 0, 0xFF, 0xFF);
            mbbsEmuMemoryCore.FillArray(mbbsEmuCpuRegisters.ES, 0, 0xFF, 0x0);

            var instructions = new Assembler(16);

            instructions.rep.movsw();
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            //Verify the 10 Words were copies
            for (ushort i = 22; i > 0; i -= 2)
                Assert.Equal(0xFFFF, mbbsEmuMemoryCore.GetWord(mbbsEmuCpuRegisters.ES, i));

            Assert.Equal(0, mbbsEmuMemoryCore.GetWord(mbbsEmuCpuRegisters.ES, 0));

            Assert.Equal(0, mbbsEmuCpuRegisters.CX);
            Assert.Equal(0, mbbsEmuCpuRegisters.SI);
            Assert.Equal(0, mbbsEmuCpuRegisters.DI);
        }
    }
}
