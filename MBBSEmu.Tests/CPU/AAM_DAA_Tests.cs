using FluentAssertions;
using Iced.Intel;
using MBBSEmu.CPU;
using MBBSEmu.Extensions;
using System.Text;
using Xunit;
using static Iced.Intel.AssemblerRegisters;

namespace MBBSEmu.Tests.CPU
{
    public class AAM_DAA_Tests : CpuTestBase
    {
        [Theory]
        [InlineData(0xABCD, "ABCD")]
        [InlineData(0xFFFF, "FFFF")]
        [InlineData(0x0000, "0000")]
        [InlineData(0x02F0, "02F0")]
        [InlineData(0x1B2D, "1B2D")]
        public void Test(ushort value, string expected)
        {
            Reset();

            mbbsEmuProtectedModeMemoryCore.AddSegment(0);
            mbbsEmuProtectedModeMemoryCore.AddSegment(2);
            mbbsEmuCpuRegisters.DS = mbbsEmuCpuRegisters.ES = 2;
            mbbsEmuCpuRegisters.SS = 0;
            mbbsEmuCpuRegisters.SP = 0x100;

            /*
            static void near pascal Hex4( void )

            Description     Convert 16 bit parameter (in dx) to 4 hex digits at ES: [di].

                NOTE: TC does not realize that "stosb" implies DI, so DI is
                not pushed/popped.  That is nice, but one day it may cease
                to be true...

                The calling code expects di to be incremented by 4 as a
                side effect of this function.
            */
            /*
            I  Hex4    proc near
            I          mov     al,dh
            I          call    Byte2Ascii
            I          mov     al,dl

            I Byte2Ascii:                         // convert byte in al to ASCII
            I          aam                        // AAM trick to separate nibbles in al
            I          xchg    ah,al
            I          call    Nibble2Ascii
            I          xchg    ah,al

             Nibble2Ascii:                       // convert hex digit in al to ASCII
            I          add     al,90h
            I          daa
            I          adc     al,40h
            I          daa
            I          stosb
            I          ret
            */

            // set pointers
            mbbsEmuCpuRegisters.DI = 0;
            mbbsEmuCpuRegisters.DX = value;

            var instructions = new Assembler(16);
            var Hex4 = instructions.CreateLabel("Hex4");
            var Byte2Ascii = instructions.CreateLabel("Byte2Ascii");
            var Nibble2Ascii = instructions.CreateLabel("Nibble2Ascii");

            instructions.call(Hex4);
            instructions.hlt();

            instructions.Label(ref Hex4);
            instructions.mov(al, dh);
            instructions.call(Byte2Ascii);
            instructions.mov(al, dl);

            instructions.Label(ref Byte2Ascii);
            instructions.aam(16);
            instructions.xchg(ah, al);
            instructions.call(Nibble2Ascii);
            instructions.xchg(ah, al);

            instructions.Label(ref Nibble2Ascii);
            instructions.add(al, 0x90);
            instructions.daa();
            instructions.adc(al, 0x40);
            instructions.daa();
            instructions.stosb();
            instructions.ret();

            CreateCodeSegment(instructions);

            //Process Instruction
            while (!mbbsEmuCpuRegisters.Halt)
               mbbsEmuCpuCore.Tick();

            Encoding.ASCII.GetString(mbbsEmuMemoryCore.GetArray(2, 0, 4)).Should().Be(expected);
        }
    }
}
