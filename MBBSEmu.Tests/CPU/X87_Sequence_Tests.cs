using Iced.Intel;
using System;
using Xunit;
using static Iced.Intel.AssemblerRegisters;

namespace MBBSEmu.Tests.CPU
{
    /// <summary>
    ///     x87 instruction sequences as Borland C++ compiles them, with results from a coprocessor
    /// </summary>
    public class X87_Sequence_Tests : CpuTestBase
    {
        /// <summary>
        ///     Borland's float-to-long helper (FTOL): FNSTCW, rounding control "chop", FISTP m64, FLDCW, and the low
        ///     32 bits of the integer as the (unsigned) long. 3000000000.5 converts to 3000000000 (0xB2D05E00), above
        ///     the signed 32-bit range (coprocessor: FISTP m64 with "chop" gives 00000000B2D05E00)
        /// </summary>
        [Fact]
        public void Borland_FloatToLong_UnsignedAbove2Pow31()
        {
            Reset();

            CreateDataSegment(new byte[0x20], 2);
            mbbsEmuCpuRegisters.DS = 2;
            mbbsEmuCpuRegisters.Fpu.SetStackTop(0);
            mbbsEmuCpuCore.FpuStack[0] = 3000000000.5d;

            var instructions = new Assembler(16);
            instructions.fnstcw(__word_ptr[0]);
            instructions.mov(ax, __word_ptr[0]);
            instructions.or(__word_ptr[0], 0x0C00);
            instructions.fldcw(__word_ptr[0]);
            instructions.fistp(__qword_ptr[8]);
            instructions.mov(__word_ptr[0], ax);
            instructions.fldcw(__word_ptr[0]);
            instructions.mov(ax, __word_ptr[8]);
            instructions.mov(dx, __word_ptr[10]);
            CreateCodeSegment(instructions);

            for (var i = 0; i < 9; i++)
                mbbsEmuCpuCore.Tick();

            Assert.Equal(0xB2D05E00u, (uint)mbbsEmuCpuRegisters.GetLong());
            Assert.Equal(0x037F, mbbsEmuCpuRegisters.Fpu.ControlWord);
        }

        /// <summary>
        ///     (unsigned long)((regno * 0.03 * 7.0f - 2000.0f) * 6.0f - 300.0f + 11.0f) for regno = 10000: FILD m64,
        ///     FMUL m64, FMUL m32, FSUB m32, FMUL m32, FSUB m32, FADD m32 in extended precision, then the conversion.
        ///     A Borland C++ 3.0 program on a coprocessor gives 0x136 (310): the exact value lies just below 311, which
        ///     every step rounded to a 53-bit double would give
        /// </summary>
        [Fact]
        public void Borland_Expression_ExtendedPrecision()
        {
            Reset();

            var data = new byte[0x40];
            BitConverter.GetBytes(10000UL).CopyTo(data, 0x00);
            BitConverter.GetBytes(0.03d).CopyTo(data, 0x08);
            BitConverter.GetBytes(7.0f).CopyTo(data, 0x10);
            BitConverter.GetBytes(2000.0f).CopyTo(data, 0x14);
            BitConverter.GetBytes(6.0f).CopyTo(data, 0x18);
            BitConverter.GetBytes(300.0f).CopyTo(data, 0x1C);
            BitConverter.GetBytes(11.0f).CopyTo(data, 0x20);
            CreateDataSegment(data, 2);
            mbbsEmuCpuRegisters.DS = 2;

            var instructions = new Assembler(16);
            instructions.fild(__qword_ptr[0x00]);
            instructions.fmul(__qword_ptr[0x08]);
            instructions.fmul(__dword_ptr[0x10]);
            instructions.fsub(__dword_ptr[0x14]);
            instructions.fmul(__dword_ptr[0x18]);
            instructions.fsub(__dword_ptr[0x1C]);
            instructions.fadd(__dword_ptr[0x20]);
            instructions.fnstcw(__word_ptr[0x24]);
            instructions.mov(ax, __word_ptr[0x24]);
            instructions.or(__word_ptr[0x24], 0x0C00);
            instructions.fldcw(__word_ptr[0x24]);
            instructions.fistp(__qword_ptr[0x28]);
            instructions.mov(__word_ptr[0x24], ax);
            instructions.fldcw(__word_ptr[0x24]);
            CreateCodeSegment(instructions);

            for (var i = 0; i < 14; i++)
                mbbsEmuCpuCore.Tick();

            Assert.Equal(0x136u, BitConverter.ToUInt32(mbbsEmuMemoryCore.GetArray(2, 0x28, 4)));
        }
    }
}
