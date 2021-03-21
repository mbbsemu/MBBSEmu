using Iced.Intel;
using MBBSEmu.CPU;
using MBBSEmu.Extensions;
using Xunit;
using static Iced.Intel.AssemblerRegisters;

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

        [Fact]
        public void memcpy()
        {
            Reset();
            mbbsEmuProtectedModeMemoryCore.AddSegment(2);
            mbbsEmuCpuRegisters.DS = mbbsEmuCpuRegisters.ES = 2;
            mbbsEmuCpuRegisters.SS = 0;
            mbbsEmuCpuRegisters.SP = 0x100;

            // fill first half with 0xFF
            mbbsEmuMemoryCore.FillArray(mbbsEmuCpuRegisters.DS, offset: 0, count: 0x7FFF, value: 0xFF);
            // second half with 0x00
            mbbsEmuMemoryCore.FillArray(mbbsEmuCpuRegisters.ES, offset: 0x8000, count: 0x7FFF, value: 0x0);

            var instructions = new Assembler(16);
            var memcpy = instructions.CreateLabel();
            var evenLength = instructions.CreateLabel();

            instructions.push(0x00FF); // length, copy 255 bytes
            instructions.push(0x9000); // src, will be 0s
            instructions.push(0x0010); // dst, will be 0xFF
            instructions.call(memcpy);
            instructions.hlt();

            /*
            dst             = word ptr  2
            src             = word ptr  4
            length          = word ptr  6
            */
            instructions.Label(ref memcpy);
            instructions.push(bp);
            instructions.mov(bp, sp);
            instructions.push(si);
            instructions.push(di);
            instructions.push(ds);
            instructions.pop(es);
            instructions.mov(di, __word_ptr[bp + 2]); // dst
            instructions.mov(si, __word_ptr[bp + 4]); // src
            instructions.mov(cx, __word_ptr[bp + 6]); // length
            instructions.shr(cx, 1);
            instructions.cld();
            instructions.rep.movsw();
            instructions.jae(evenLength); // jnb
            instructions.movsb();
            instructions.Label(ref evenLength);
            instructions.mov(ax, __word_ptr[bp + 2]); // dst, return value
            instructions.pop(di);
            instructions.pop(si);
            instructions.pop(bp);
            instructions.ret();
            CreateCodeSegment(instructions);

            while (!mbbsEmuCpuRegisters.Halt)
                mbbsEmuCpuCore.Tick();
        }
    }
}
