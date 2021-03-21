using Iced.Intel;
using MBBSEmu.CPU;
using MBBSEmu.Extensions;
using Xunit;

namespace MBBSEmu.Tests.CPU
{
    public class MOVSB_Tests : CpuTestBase
    {
        [Fact]
        public void MOVSB_Test()
        {
            Reset();
            mbbsEmuProtectedModeMemoryCore.AddSegment(2);
            mbbsEmuProtectedModeMemoryCore.AddSegment(3);
            mbbsEmuCpuRegisters.DS = 2;
            mbbsEmuCpuRegisters.SI = 0;
            mbbsEmuCpuRegisters.ES = 3;
            mbbsEmuCpuRegisters.DI = 0;
            mbbsEmuMemoryCore.SetByte(mbbsEmuCpuRegisters.DS, mbbsEmuCpuRegisters.SI, 0xFF);
            mbbsEmuMemoryCore.SetByte(mbbsEmuCpuRegisters.ES, mbbsEmuCpuRegisters.DI, 0x0);

            var instructions = new Assembler(16);

            instructions.movsb();
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(0xFF, mbbsEmuMemoryCore.GetByte(mbbsEmuCpuRegisters.ES, 0));
            Assert.Equal(1, mbbsEmuCpuRegisters.SI);
            Assert.Equal(1, mbbsEmuCpuRegisters.DI);
        }

        [Fact]
        public void MOVSB_Rep_Test()
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

            instructions.rep.movsb();
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            //Verify the 10 bytes were copies
            for (ushort i = 0; i < 10; i++)
                Assert.Equal(0xFF, mbbsEmuMemoryCore.GetByte(mbbsEmuCpuRegisters.ES, i));

            Assert.Equal(0, mbbsEmuMemoryCore.GetByte(mbbsEmuCpuRegisters.ES, 11));

            Assert.Equal(0, mbbsEmuCpuRegisters.CX);
            Assert.Equal(10, mbbsEmuCpuRegisters.SI);
            Assert.Equal(10, mbbsEmuCpuRegisters.DI);
        }

        [Fact]
        public void MOVSB_Rep_DF_Test()
        {
            Reset();
            mbbsEmuProtectedModeMemoryCore.AddSegment(2);
            mbbsEmuProtectedModeMemoryCore.AddSegment(3);
            mbbsEmuCpuRegisters.DS = 2;
            mbbsEmuCpuRegisters.SI = 11;
            mbbsEmuCpuRegisters.ES = 3;
            mbbsEmuCpuRegisters.DI = 11;
            mbbsEmuCpuRegisters.CX = 10;
            mbbsEmuCpuRegisters.F = mbbsEmuCpuRegisters.F.SetFlag((ushort) EnumFlags.DF);

            mbbsEmuMemoryCore.FillArray(mbbsEmuCpuRegisters.DS, 0, 0xFF, 0xFF);
            mbbsEmuMemoryCore.FillArray(mbbsEmuCpuRegisters.ES, 0, 0xFF, 0x0);

            var instructions = new Assembler(16);

            instructions.rep.movsb();
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            //Verify the 10 Words were copies
            for (ushort i = 11; i > 1; i--)
                Assert.Equal(0xFF, mbbsEmuMemoryCore.GetByte(mbbsEmuCpuRegisters.ES, i));

            Assert.Equal(0, mbbsEmuMemoryCore.GetByte(mbbsEmuCpuRegisters.ES, 0));

            Assert.Equal(0, mbbsEmuCpuRegisters.CX);
            Assert.Equal(1, mbbsEmuCpuRegisters.SI);
            Assert.Equal(1, mbbsEmuCpuRegisters.DI);
        }
    }
}
