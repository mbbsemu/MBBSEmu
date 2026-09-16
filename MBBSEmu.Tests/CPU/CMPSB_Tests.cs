using FluentAssertions;
using Iced.Intel;
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
            mbbsEmuCpuRegisters.ZeroFlag.Should().BeTrue();
        }

        [Theory]
        [InlineData(0x02, 0x01, false, false, false, false, false)]
        [InlineData(0x01, 0x02, true, false, true, true, false)] // Unsigned below: CF drives JB
        [InlineData(0x80, 0x01, false, true, false, true, false)] // Signed overflow: 0x80 - 0x01 == 0x7F
        [InlineData(0x10, 0x01, false, false, false, true, false)]
        [InlineData(0x41, 0x41, false, false, false, false, true)]
        public void CMPSB_ArithmeticFlags(byte sourceByte, byte destinationByte, bool expectedCF, bool expectedOF, bool expectedSF, bool expectedAF, bool expectedZF)
        {
            Reset();

            var ptr1 = mbbsEmuMemoryCore.Malloc(1);
            var ptr2 = mbbsEmuMemoryCore.Malloc(1);
            mbbsEmuMemoryCore.SetByte(ptr1, sourceByte);
            mbbsEmuMemoryCore.SetByte(ptr2, destinationByte);

            mbbsEmuCpuRegisters.DS = ptr1.Segment;
            mbbsEmuCpuRegisters.SI = ptr1.Offset;
            mbbsEmuCpuRegisters.ES = ptr2.Segment;
            mbbsEmuCpuRegisters.DI = ptr2.Offset;

            var instructions = new Assembler(16);
            instructions.cmpsb();
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            //CMPSB computes [DS:SI] - [ES:DI]
            mbbsEmuCpuRegisters.CarryFlag.Should().Be(expectedCF);
            mbbsEmuCpuRegisters.OverflowFlag.Should().Be(expectedOF);
            mbbsEmuCpuRegisters.SignFlag.Should().Be(expectedSF);
            mbbsEmuCpuRegisters.AuxiliaryCarryFlag.Should().Be(expectedAF);
            mbbsEmuCpuRegisters.ZeroFlag.Should().Be(expectedZF);
        }
    }
}
