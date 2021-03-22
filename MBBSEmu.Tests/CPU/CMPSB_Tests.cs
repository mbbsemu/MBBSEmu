using FluentAssertions;
using Iced.Intel;
using MBBSEmu.CPU;
using MBBSEmu.Extensions;
using Xunit;
using static Iced.Intel.AssemblerRegisters;

namespace MBBSEmu.Tests.CPU
{
    public class CMPSB_Tests : CpuTestBase
    {
        [Fact]
        public void CMPSB_Test()
        {
            Reset();

            var ptr1 = mbbsEmuMemoryCore.Malloc(16);
            var ptr2 = mbbsEmuMemoryCore.Malloc(16);

            mbbsEmuMemoryCore.FillArray(ptr1, 16, 0xAE);
            mbbsEmuMemoryCore.FillArray(ptr2, 16, 0xAE);

            /*
            ; (prologue)
            mov     esi, [ebp+arg_0]    ; Move first pointer to esi
            mov     edi, [ebp+arg_4]    ; Move second pointer to edi
            mov     ecx, [ebp+arg_8]    ; Move length to ecx

            cld                         ; Clear DF, the direction flag, so comparisons happen
                                        ; at increasing addresses
            cmp     ecx, ecx            ; Special case: If length parameter to memcmp is
                                        ; zero, don't compare any bytes.
            repe cmpsb                  ; Compare bytes at DS:ESI and ES:EDI, setting flags
                                        ; Repeat this while equal ZF is set
            setz    al                  ; Set al (return value) to 1 if ZF is still set
                                        ; (all bytes were equal).
            ; (epilogue)
            */

            // set pointers
            mbbsEmuCpuRegisters.DS = ptr1.Segment;
            mbbsEmuCpuRegisters.SI = ptr1.Offset;

            mbbsEmuCpuRegisters.ES = ptr2.Segment;
            mbbsEmuCpuRegisters.DI = ptr2.Offset;

            mbbsEmuCpuRegisters.CX = 16;

            var instructions = new Assembler(16);
            instructions.cld();
            instructions.cmp(ecx, ecx);
            instructions.repe.cmpsb();
            instructions.hlt();
            CreateCodeSegment(instructions);

            //Process Instruction
            while (!mbbsEmuCpuRegisters.Halt)
               mbbsEmuCpuCore.Tick();

            //Verify Flags
            mbbsEmuCpuRegisters.SI.Should().Be((ushort)(ptr1.Offset + 16));
            mbbsEmuCpuRegisters.DI.Should().Be((ushort)(ptr2.Offset + 16));
            mbbsEmuCpuRegisters.CX.Should().Be(0);
            mbbsEmuCpuRegisters.F.IsFlagSet((ushort)EnumFlags.ZF).Should().BeTrue();
        }
    }
}
