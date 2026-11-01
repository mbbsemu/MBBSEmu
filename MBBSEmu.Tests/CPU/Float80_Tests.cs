using FluentAssertions;
using MBBSEmu.CPU;
using System;
using System.Globalization;
using Xunit;

namespace MBBSEmu.Tests.CPU
{
    /// <summary>
    ///     Float80 on cases worth reading one by one. Every expected value is what an x87 coprocessor gave for the same
    ///     instruction, operands and control word (the full sets are in Float80_Coprocessor_Tests).
    ///     Status bits: IE 0x0001, DE 0x0002, ZE 0x0004, OE 0x0008, UE 0x0010, PE 0x0020, C0 0x0100, C1 0x0200,
    ///     C2 0x0400, C3 0x4000.
    /// </summary>
    public class Float80_Tests
    {
        private static Float80 Extended(string hex) =>
            new(ushort.Parse(hex.AsSpan(0, 4), NumberStyles.HexNumber), ulong.Parse(hex.AsSpan(4, 16), NumberStyles.HexNumber));

        private static string Hex(Float80 value) => $"{value.SignExponent:X4}{value.Significand:X16}";

        /// <summary>
        ///     1/3 and -1/3 (FDIV) under every precision control (24, 53, 64 bits) and rounding control; C1 is set when
        ///     the magnitude was rounded up
        /// </summary>
        [Theory]
        [InlineData(0x003F, "3FFDAAAAAB0000000000", 0x0220, "BFFDAAAAAB0000000000", 0x0220)]
        [InlineData(0x043F, "3FFDAAAAAA0000000000", 0x0020, "BFFDAAAAAB0000000000", 0x0220)]
        [InlineData(0x083F, "3FFDAAAAAB0000000000", 0x0220, "BFFDAAAAAA0000000000", 0x0020)]
        [InlineData(0x0C3F, "3FFDAAAAAA0000000000", 0x0020, "BFFDAAAAAA0000000000", 0x0020)]
        [InlineData(0x023F, "3FFDAAAAAAAAAAAAA800", 0x0020, "BFFDAAAAAAAAAAAAA800", 0x0020)]
        [InlineData(0x063F, "3FFDAAAAAAAAAAAAA800", 0x0020, "BFFDAAAAAAAAAAAAB000", 0x0220)]
        [InlineData(0x0A3F, "3FFDAAAAAAAAAAAAB000", 0x0220, "BFFDAAAAAAAAAAAAA800", 0x0020)]
        [InlineData(0x0E3F, "3FFDAAAAAAAAAAAAA800", 0x0020, "BFFDAAAAAAAAAAAAA800", 0x0020)]
        [InlineData(0x033F, "3FFDAAAAAAAAAAAAAAAB", 0x0220, "BFFDAAAAAAAAAAAAAAAB", 0x0220)]
        [InlineData(0x073F, "3FFDAAAAAAAAAAAAAAAA", 0x0020, "BFFDAAAAAAAAAAAAAAAB", 0x0220)]
        [InlineData(0x0B3F, "3FFDAAAAAAAAAAAAAAAB", 0x0220, "BFFDAAAAAAAAAAAAAAAA", 0x0020)]
        [InlineData(0x0F3F, "3FFDAAAAAAAAAAAAAAAA", 0x0020, "BFFDAAAAAAAAAAAAAAAA", 0x0020)]
        public void Divide_PrecisionAndRoundingControl(ushort controlWord, string positive, ushort positiveStatus,
            string negative, ushort negativeStatus)
        {
            var three = Float80.FromInt64(3);

            ushort statusWord = 0;
            Hex(Float80.Divide(Float80.One, three, controlWord, ref statusWord)).Should().Be(positive);
            statusWord.Should().Be(positiveStatus);

            statusWord = 0;
            Hex(Float80.Divide(Float80.One.Negate(), three, controlWord, ref statusWord)).Should().Be(negative);
            statusWord.Should().Be(negativeStatus);
        }

        /// <summary>
        ///     FISTP m64: rounding control "chop" (Borland's float-to-integer conversion) keeps all 64 bits, and a value
        ///     outside 64 bits gives the integer indefinite with IE only
        /// </summary>
        [Theory]
        [InlineData(0x0F7F, "401F8000000080000000", 0x0000000100000001UL, 0x0000)]
        [InlineData(0x0F7F, "401FFFFFFFFF80000000", 0x00000001FFFFFFFFUL, 0x0000)]
        [InlineData(0x037F, "403E8000000000000000", 0x8000000000000000UL, 0x0001)]
        [InlineData(0x037F, "C03E8000000000000000", 0x8000000000000000UL, 0x0000)]
        [InlineData(0x037F, "C03E8000000000000001", 0x8000000000000000UL, 0x0001)]
        [InlineData(0x037F, "403DFFFFFFFFFFFFFFFF", 0x8000000000000000UL, 0x0001)]
        [InlineData(0x037F, "4040FFFFFFFFFFFFFFFF", 0x8000000000000000UL, 0x0001)]
        public void ToInt64(ushort controlWord, string value, ulong expected, ushort expectedStatus)
        {
            ushort statusWord = 0;
            ((ulong)Extended(value).ToInt64(controlWord, ref statusWord)).Should().Be(expected);
            statusWord.Should().Be(expectedStatus);
        }

        /// <summary>
        ///     FIST m16 and m32: rounding by the rounding control, the integer indefinite outside the range
        /// </summary>
        [Theory]
        [InlineData(16, 0x037F, "400E8000000000000000", 0x8000U, 0x0001)]
        [InlineData(16, 0x037F, "C00E8000000000000000", 0x8000U, 0x0000)]
        [InlineData(16, 0x037F, "400DFFFF800000000000", 0x8000U, 0x0001)]
        [InlineData(16, 0x0F7F, "400DFFFF800000000000", 0x7FFFU, 0x0020)]
        [InlineData(16, 0x037F, "3FFE8000000000000000", 0x0000U, 0x0020)]
        [InlineData(16, 0x037F, "3FFEC000000000000000", 0x0001U, 0x0220)]
        [InlineData(16, 0x077F, "BFFE8000000000000000", 0xFFFFU, 0x0220)]
        [InlineData(16, 0x037F, "7FFFC000000000000000", 0x8000U, 0x0001)]
        [InlineData(32, 0x037F, "401E8000000000000000", 0x80000000U, 0x0001)]
        [InlineData(32, 0x037F, "C01E8000000000000000", 0x80000000U, 0x0000)]
        [InlineData(32, 0x037F, "401DFFFFFFFF80000000", 0x80000000U, 0x0001)]
        [InlineData(32, 0x037F, "401DFFFFFFFE80000000", 0x7FFFFFFFU, 0x0020)]
        public void ToInt16_ToInt32(int bits, ushort controlWord, string value, uint expected, ushort expectedStatus)
        {
            ushort statusWord = 0;
            var actual = bits == 16
                ? (ushort)Extended(value).ToInt16(controlWord, ref statusWord)
                : (uint)Extended(value).ToInt32(controlWord, ref statusWord);
            actual.Should().Be(expected);
            statusWord.Should().Be(expectedStatus);
        }

        /// <summary>
        ///     FLDPI, FLDL2E, FLDL2T, FLDLG2 and FLDLN2 under each rounding control (the precision control does not
        ///     apply); no flags
        /// </summary>
        [Theory]
        [InlineData(0x037F, "4000C90FDAA22168C235", "3FFFB8AA3B295C17F0BC", "4000D49A784BCD1B8AFE", "3FFD9A209A84FBCFF799", "3FFEB17217F7D1CF79AC")]
        [InlineData(0x077F, "4000C90FDAA22168C234", "3FFFB8AA3B295C17F0BB", "4000D49A784BCD1B8AFE", "3FFD9A209A84FBCFF798", "3FFEB17217F7D1CF79AB")]
        [InlineData(0x0B7F, "4000C90FDAA22168C235", "3FFFB8AA3B295C17F0BC", "4000D49A784BCD1B8AFF", "3FFD9A209A84FBCFF799", "3FFEB17217F7D1CF79AC")]
        [InlineData(0x0F7F, "4000C90FDAA22168C234", "3FFFB8AA3B295C17F0BB", "4000D49A784BCD1B8AFE", "3FFD9A209A84FBCFF798", "3FFEB17217F7D1CF79AB")]
        [InlineData(0x007F, "4000C90FDAA22168C235", "3FFFB8AA3B295C17F0BC", "4000D49A784BCD1B8AFE", "3FFD9A209A84FBCFF799", "3FFEB17217F7D1CF79AC")]
        public void Constants(ushort controlWord, string pi, string log2E, string log2Ten, string log10Two, string lnTwo)
        {
            Hex(Float80.Pi(controlWord)).Should().Be(pi);
            Hex(Float80.Log2E(controlWord)).Should().Be(log2E);
            Hex(Float80.Log2Ten(controlWord)).Should().Be(log2Ten);
            Hex(Float80.Log10Two(controlWord)).Should().Be(log10Two);
            Hex(Float80.LnTwo(controlWord)).Should().Be(lnTwo);
            Hex(Float80.One).Should().Be("3FFF8000000000000000");
            Hex(Float80.Zero).Should().Be("00000000000000000000");
        }

        /// <summary>
        ///     Overflow (FMUL of the largest power of two by 2): infinity (C1 set) when the rounding goes away from zero,
        ///     otherwise the largest finite value of the precision control
        /// </summary>
        [Theory]
        [InlineData(0x037F, "7FFE8000000000000000", "7FFF8000000000000000", 0x0228)]
        [InlineData(0x077F, "7FFE8000000000000000", "7FFEFFFFFFFFFFFFFFFF", 0x0028)]
        [InlineData(0x0B7F, "7FFE8000000000000000", "7FFF8000000000000000", 0x0228)]
        [InlineData(0x0F7F, "7FFE8000000000000000", "7FFEFFFFFFFFFFFFFFFF", 0x0028)]
        [InlineData(0x077F, "FFFE8000000000000000", "FFFF8000000000000000", 0x0228)]
        [InlineData(0x0B7F, "FFFE8000000000000000", "FFFEFFFFFFFFFFFFFFFF", 0x0028)]
        [InlineData(0x0C7F, "7FFE8000000000000000", "7FFEFFFFFF0000000000", 0x0028)]
        [InlineData(0x0E7F, "7FFE8000000000000000", "7FFEFFFFFFFFFFFFF800", 0x0028)]
        public void Multiply_Overflow(ushort controlWord, string value, string expected, ushort expectedStatus)
        {
            ushort statusWord = 0;
            Hex(Float80.Multiply(Extended(value), Float80.FromInt64(2), controlWord, ref statusWord)).Should().Be(expected);
            statusWord.Should().Be(expectedStatus);
        }

        /// <summary>
        ///     Underflow: a tiny inexact result is a denormal with UE and PE; an exact denormal raises nothing; tininess
        ///     is judged after rounding (a value just under the smallest normal that rounds onto it is not tiny), and a
        ///     denormal keeps the precision control's width counted from the smallest normal exponent
        /// </summary>
        [Theory]
        [InlineData(0x037F, "00018000000000000001", "3FFE8000000000000000", "00004000000000000000", 0x0030)]
        [InlineData(0x037F, "00018000000000000000", "3FFE8000000000000000", "00004000000000000000", 0x0000)]
        [InlineData(0x007F, "0001FFFFFF8000000000", "3FFE8000000000000000", "00018000000000000000", 0x0220)]
        [InlineData(0x007F, "0001FFFFFE8000000000", "3FFE8000000000000000", "00007FFFFF0000000000", 0x0030)]
        [InlineData(0x027F, "0001C000000000000000", "3FF08000000000000000", "00000001800000000000", 0x0000)]
        public void Multiply_Underflow(ushort controlWord, string a, string b, string expected, ushort expectedStatus)
        {
            ushort statusWord = 0;
            Hex(Float80.Multiply(Extended(a), Extended(b), controlWord, ref statusWord)).Should().Be(expected);
            statusWord.Should().Be(expectedStatus);
        }

        /// <summary>
        ///     NaN operands (FADD): the larger significand wins, the positive NaN when the significands are equal; a
        ///     signalling NaN is made quiet and raises IE; an unsupported encoding (here an unnormal) comes before NaNs
        ///     and gives the real indefinite
        /// </summary>
        [Theory]
        [InlineData("7FFFC000000000000001", "7FFFC000000000000002", "7FFFC000000000000002", 0x0000)]
        [InlineData("FFFFC000000000000002", "7FFFC000000000000001", "FFFFC000000000000002", 0x0000)]
        [InlineData("FFFFC000000000000005", "7FFFC000000000000005", "7FFFC000000000000005", 0x0000)]
        [InlineData("7FFFA000000000000001", "7FFFC000000000000005", "7FFFC000000000000005", 0x0001)]
        [InlineData("7FFFA000000000000001", "7FFFA000000000000002", "7FFFE000000000000002", 0x0001)]
        [InlineData("3FFF8000000000000000", "7FFFA000000000000000", "7FFFE000000000000000", 0x0001)]
        [InlineData("3FFF0000000000000000", "7FFFC000000000000001", "FFFFC000000000000000", 0x0001)]
        [InlineData("7FFF8000000000000000", "FFFF8000000000000000", "FFFFC000000000000000", 0x0001)]
        [InlineData("7FFFC000000000000001", "00000000000000000001", "7FFFC000000000000001", 0x0000)]
        public void Add_SpecialOperands(string a, string b, string expected, ushort expectedStatus)
        {
            ushort statusWord = 0;
            Hex(Float80.Add(Extended(a), Extended(b), 0x037F, ref statusWord)).Should().Be(expected);
            statusWord.Should().Be(expectedStatus);
        }

        /// <summary>
        ///     FPREM: the remainder with the sign of the dividend; C0, C3, C1 = bits 2, 1, 0 of the quotient when the
        ///     reduction is complete, C2 set when it is not (the exponent difference was 64 or more)
        /// </summary>
        [Theory]
        [InlineData("403E8000000000000000", "3FFFC000000000000000", "3FFE8000000000000000", 0x0300)]
        [InlineData("4002E000000000000000", "3FFFC000000000000000", "3FFE8000000000000000", 0x0200)]
        [InlineData("4002E000000000000000", "BFFFC000000000000000", "3FFE8000000000000000", 0x0200)]
        [InlineData("C0018000000000000000", "3FFFC000000000000000", "BFFF8000000000000000", 0x4000)]
        [InlineData("40038000000000000000", "40038000000000000000", "00000000000000000000", 0x0200)]
        [InlineData("40408000000000000000", "3FFFC000000000000000", "401E8000000000000000", 0x0400)]
        [InlineData("4400ABCDEF0123456789", "3FFFC000000000000001", "43DAD159E206BB5AFFA0", 0x0400)]
        [InlineData("7FFF8000000000000000", "3FFFC000000000000000", "FFFFC000000000000000", 0x0001)]
        [InlineData("3FFF8000000000000000", "7FFF8000000000000000", "3FFF8000000000000000", 0x0000)]
        public void PartialRemainder(string a, string b, string expected, ushort expectedStatus)
        {
            ushort statusWord = 0;
            Hex(Float80.PartialRemainder(Extended(a), Extended(b), ref statusWord)).Should().Be(expected);
            statusWord.Should().Be(expectedStatus);
        }

        /// <summary>
        ///     FCOM: C3 C2 C0 = 000 above, 001 below, 100 equal (-0 equals +0), 111 unordered with IE (any NaN)
        /// </summary>
        [Theory]
        [InlineData("3FFF8000000000000000", "4000C000000000000000", 0x0100)]
        [InlineData("4000C000000000000000", "3FFF8000000000000000", 0x0000)]
        [InlineData("00000000000000000000", "80000000000000000000", 0x4000)]
        [InlineData("7FFF8000000000000000", "7FFF8000000000000000", 0x4000)]
        [InlineData("7FFF8000000000000000", "3FFF8000000000000000", 0x0000)]
        [InlineData("7FFFC000000000000000", "3FFF8000000000000000", 0x4501)]
        [InlineData("FFFF8000000000000000", "00000000000000000001", 0x0102)]
        public void Compare(string a, string b, ushort expectedStatus)
        {
            ushort statusWord = 0x0200;
            Float80.Compare(Extended(a), Extended(b), ref statusWord);
            statusWord.Should().Be(expectedStatus);
        }

        /// <summary>
        ///     FST m32 and m64 round by the rounding control; ties go to even; an overflow is infinity or the largest
        ///     finite value; a result rounded up to the smallest normal is tiny when the unbounded rounding is not
        /// </summary>
        [Theory]
        [InlineData(0x037F, "3FFF8000008000000000", 0x3F800000U, 0x0020)]
        [InlineData(0x037F, "3FFF8000018000000000", 0x3F800002U, 0x0220)]
        [InlineData(0x037F, "407F8000000000000000", 0x7F800000U, 0x0228)]
        [InlineData(0x077F, "407F8000000000000000", 0x7F7FFFFFU, 0x0028)]
        [InlineData(0x037F, "3F80FFFFFF8000000000", 0x00800000U, 0x0220)]
        [InlineData(0x037F, "3F80FFFFFF0000000000", 0x00800000U, 0x0230)]
        [InlineData(0x037F, "3F698000000000000000", 0x00000000U, 0x0030)]
        [InlineData(0x037F, "7FFFA000000000000000", 0x7FE00000U, 0x0001)]
        [InlineData(0x037F, "3FFF0000000000000000", 0xFFC00000U, 0x0001)]
        public void ToSingle(ushort controlWord, string value, uint expected, ushort expectedStatus)
        {
            ushort statusWord = 0;
            Extended(value).ToSingle(controlWord, ref statusWord).Should().Be(expected);
            statusWord.Should().Be(expectedStatus);
        }

        /// <summary>
        ///     FLD m32 and m64: exact; a signalling NaN becomes quiet with IE, a denormal raises DE
        /// </summary>
        [Theory]
        [InlineData(0x7F800001U, "7FFFC000010000000000", 0x0001)]
        [InlineData(0x7FC00001U, "7FFFC000010000000000", 0x0000)]
        [InlineData(0x00000001U, "3F6A8000000000000000", 0x0002)]
        [InlineData(0xFF800000U, "FFFF8000000000000000", 0x0000)]
        public void FromSingle(uint bits, string expected, ushort expectedStatus)
        {
            ushort statusWord = 0x0200;
            Hex(Float80.FromSingle(bits, ref statusWord)).Should().Be(expected);
            statusWord.Should().Be(expectedStatus);
        }

        [Theory]
        [InlineData(0x7FF0000000000001UL, "7FFFC000000000000800", 0x0001)]
        [InlineData(0x0000000000000001UL, "3BCD8000000000000000", 0x0002)]
        [InlineData(0x8000000000000000UL, "80000000000000000000", 0x0000)]
        public void FromDouble(ulong bits, string expected, ushort expectedStatus)
        {
            ushort statusWord = 0;
            Hex(Float80.FromDouble(bits, ref statusWord)).Should().Be(expected);
            statusWord.Should().Be(expectedStatus);
        }

        /// <summary>
        ///     FLD m80 and FSTP m80 copy the 10 bytes unchanged, whatever they encode
        /// </summary>
        [Theory]
        [InlineData("4000C90FDAA22168C235")]
        [InlineData("7FFFA000000000000000")]
        [InlineData("3FFF0000000000000000")]
        [InlineData("00008000000000000000")]
        [InlineData("80000000000000000001")]
        public void Bytes_RoundTrip(string value)
        {
            var bytes = new byte[10];
            Extended(value).ToBytes(bytes);

            //Little-endian in memory: the significand first, then the sign and exponent
            var expected = Convert.FromHexString(value);
            Array.Reverse(expected);
            bytes.Should().Equal(expected);
            Hex(Float80.FromBytes(bytes)).Should().Be(value);
        }
    }
}
