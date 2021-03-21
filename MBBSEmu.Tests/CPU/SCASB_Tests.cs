using Iced.Intel;
using MBBSEmu.CPU;
using MBBSEmu.Extensions;
using Xunit;

namespace MBBSEmu.Tests.CPU
{
    public class SCASB_Tests : CpuTestBase
    {
        [Fact]
        public void SCASB_DirectionClear_Test()
        {
            Reset();
            mbbsEmuProtectedModeMemoryCore.AddSegment(2);
            mbbsEmuCpuRegisters.F = 0;
            mbbsEmuCpuRegisters.AL = 0xFF;
            mbbsEmuCpuRegisters.DS = 2;
            mbbsEmuCpuRegisters.ES = 2;
            mbbsEmuCpuRegisters.DI = 0;
            mbbsEmuMemoryCore.SetArray(mbbsEmuCpuRegisters.ES, mbbsEmuCpuRegisters.DI,
                new byte[] {0xFF });

            var instructions = new Assembler(16);

            instructions.scasb();
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(0x1, mbbsEmuCpuRegisters.DI);
            Assert.True(mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.ZF));
        }

        [Fact]
        public void SCASB_DirectionSet_Test()
        {
            Reset();
            mbbsEmuProtectedModeMemoryCore.AddSegment(2);
            mbbsEmuCpuRegisters.F = mbbsEmuCpuRegisters.F.SetFlag((ushort)EnumFlags.DF);
            mbbsEmuCpuRegisters.AL = 0xFF;
            mbbsEmuCpuRegisters.DS = 2;
            mbbsEmuCpuRegisters.ES = 2;
            mbbsEmuCpuRegisters.DI = 0;
            mbbsEmuMemoryCore.SetArray(mbbsEmuCpuRegisters.ES, mbbsEmuCpuRegisters.DI,
                new byte[] { 0xFF });

            var instructions = new Assembler(16);

            instructions.scasb();
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(0xFFFF, mbbsEmuCpuRegisters.DI); //DI is decremented after the comparison, which would overflow from 0x0->0xFFFF
            Assert.True(mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.ZF));
        }

        [Fact]
        public void SCASB_Repne_DirectionClear_Test()
        {
            Reset();
            mbbsEmuProtectedModeMemoryCore.AddSegment(2);
            mbbsEmuCpuRegisters.F = 0;
            mbbsEmuCpuRegisters.AL = 0xFF;
            mbbsEmuCpuRegisters.CX = 0xA;
            mbbsEmuCpuRegisters.DS = 2;
            mbbsEmuCpuRegisters.ES = 2;
            mbbsEmuCpuRegisters.DI = 0;
            mbbsEmuMemoryCore.SetArray(mbbsEmuCpuRegisters.ES, mbbsEmuCpuRegisters.DI,
                new byte[] {0x0, 0x0, 0x0, 0x0, 0x0, 0xFF});

            var instructions = new Assembler(16);

            instructions.repne.scasb();
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(0x4, mbbsEmuCpuRegisters.CX);
            Assert.Equal(0x6, mbbsEmuCpuRegisters.DI);
            Assert.True(mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.ZF));
        }

        [Fact]
        public void SCASB_Repne_DirectionClear_ExhaustCX_Test()
        {
            Reset();
            mbbsEmuProtectedModeMemoryCore.AddSegment(2);
            mbbsEmuCpuRegisters.F = 0;
            mbbsEmuCpuRegisters.AL = 0xFF;
            mbbsEmuCpuRegisters.CX = 0xA;
            mbbsEmuCpuRegisters.DS = 2;
            mbbsEmuCpuRegisters.ES = 2;
            mbbsEmuCpuRegisters.DI = 0;

            var instructions = new Assembler(16);

            instructions.repne.scasb();
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(0, mbbsEmuCpuRegisters.CX);
            Assert.Equal(0xA, mbbsEmuCpuRegisters.DI);
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.ZF));
        }

        [Fact]
        public void SCASB_Repne_DirectionSet_Test()
        {
            Reset();
            mbbsEmuProtectedModeMemoryCore.AddSegment(2);
            mbbsEmuCpuRegisters.F = mbbsEmuCpuRegisters.F.SetFlag((ushort) EnumFlags.DF);
            mbbsEmuCpuRegisters.AL = 0xFF;
            mbbsEmuCpuRegisters.CX = 0xA;
            mbbsEmuCpuRegisters.DS = 2;
            mbbsEmuCpuRegisters.ES = 2;
            mbbsEmuCpuRegisters.DI = 5;
            mbbsEmuMemoryCore.SetArray(mbbsEmuCpuRegisters.ES, 0,
                new byte[] { 0xFF, 0x0, 0x0, 0x0, 0x0, 0x0 });

            var instructions = new Assembler(16);

            instructions.repne.scasb();
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(0x4, mbbsEmuCpuRegisters.CX);
            Assert.Equal(0xFFFF, mbbsEmuCpuRegisters.DI); //DI is decremented after the comparison, which would overflow from 0x0->0xFFFF
            Assert.True(mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.ZF));
        }

        [Fact]
        public void SCASB_Repe_DirectionClear_Test()
        {
            Reset();
            mbbsEmuProtectedModeMemoryCore.AddSegment(2);
            mbbsEmuCpuRegisters.F = 0;
            mbbsEmuCpuRegisters.AL = 0x0;
            mbbsEmuCpuRegisters.CX = 0xA;
            mbbsEmuCpuRegisters.DS = 2;
            mbbsEmuCpuRegisters.ES = 2;
            mbbsEmuCpuRegisters.DI = 0;
            mbbsEmuMemoryCore.SetArray(mbbsEmuCpuRegisters.ES, mbbsEmuCpuRegisters.DI,
                new byte[] { 0x0, 0x0, 0x0, 0x0, 0x0, 0xFF });

            var instructions = new Assembler(16);

            instructions.repe.scasb();
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(0x4, mbbsEmuCpuRegisters.CX);
            Assert.Equal(0x6, mbbsEmuCpuRegisters.DI);
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.ZF));
        }

        [Fact]
        public void SCASB_Repe_DirectionSet_Test()
        {
            Reset();
            mbbsEmuProtectedModeMemoryCore.AddSegment(2);
            mbbsEmuCpuRegisters.F = mbbsEmuCpuRegisters.F.SetFlag((ushort)EnumFlags.DF);
            mbbsEmuCpuRegisters.AL = 0x0;
            mbbsEmuCpuRegisters.CX = 0xA;
            mbbsEmuCpuRegisters.DS = 2;
            mbbsEmuCpuRegisters.ES = 2;
            mbbsEmuCpuRegisters.DI = 5;
            mbbsEmuMemoryCore.SetArray(mbbsEmuCpuRegisters.ES, 0,
                new byte[] { 0xFF, 0x0, 0x0, 0x0, 0x0, 0x0 });

            var instructions = new Assembler(16);

            instructions.repe.scasb();
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(0x4, mbbsEmuCpuRegisters.CX);
            Assert.Equal(0xFFFF, mbbsEmuCpuRegisters.DI); //DI is decremented after the comparison, which would overflow from 0x0->0xFFFF
            Assert.False(mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.ZF));
        }
    }
}
