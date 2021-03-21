using FluentAssertions;
using Iced.Intel;
using MBBSEmu.CPU;
using MBBSEmu.Extensions;
using System.Text;
using Xunit;
using static Iced.Intel.AssemblerRegisters;

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

        [Theory]
        [InlineData("test", 4)]
        public void strlen(string str, ushort expectedLength)
        {
            /*
            Func@   strlen, _EXPFUNC, _RTLENTRYF, <pointer s>

                    Link@   edi
                    mov     edi, s
                    mov     ecx, -1
                    xor     al, al          ; search for null
                    cld
                    repne   scasb           ; scan one character past null
                    not     ecx
                    lea     eax, [ecx-1]
                    Unlink@ edi
                    Return@

            EndFunc@ strlen
            */

            Reset();
            mbbsEmuProtectedModeMemoryCore.AddSegment(0);
            mbbsEmuProtectedModeMemoryCore.AddSegment(2);
            mbbsEmuCpuRegisters.DS = mbbsEmuCpuRegisters.ES = 2;
            mbbsEmuCpuRegisters.SS = 0;
            mbbsEmuCpuRegisters.SP = 0x100;

            mbbsEmuMemoryCore.SetArray(mbbsEmuCpuRegisters.DS, 0x1000, Encoding.ASCII.GetBytes(str + "\0"));

            var instructions = new Assembler(16);
            var strlen = instructions.CreateLabel();

            instructions.push(0x1000); // src, will be 0s
            instructions.call(strlen);
            instructions.hlt();

            /*
            str             = word ptr  4
            */
            instructions.Label(ref strlen);
            instructions.push(di);
            instructions.push(cx);
            instructions.mov(di, __word_ptr[bp + 4]); // str
            instructions.mov(cx, -1);
            instructions.xor(al, al); // search for null
            instructions.cld();
            instructions.repne.scasb();
            instructions.not(cx);
            //instructions.lea(ax, __word_ptr[cx - 1]);
            instructions.mov(ax, cx);
            instructions.pop(cx);
            instructions.pop(di);
            instructions.ret();
            CreateCodeSegment(instructions);

            while (!mbbsEmuCpuRegisters.Halt)
                mbbsEmuCpuCore.Tick();

            mbbsEmuCpuRegisters.AX.Should().Be(expectedLength);
        }
    }
}
