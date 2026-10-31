using MBBSEmu.Memory;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class strtol_Tests : ExportedModuleTestBase
    {
        private const int STRTOL_ORDINAL = 586;

        [Theory]
        [InlineData("123456 Hello some text", 6, 10, 123456)]
        [InlineData("2345678 YESSS", 7, 10, 2345678)]
        [InlineData("+2345678 YESSS", 8, 10, 2345678)]
        [InlineData("   2345678 YESSS", 10, 10, 2345678)]
        [InlineData("78 A", 2, 10, 78)]
        [InlineData("0x23CACE A very big number", 8, 16, 2345678)]
        [InlineData("-2345678 A very big number", 8, 10, -2345678)]
        [InlineData("FFFFF is my favorite number", 5, 16, 1048575)]
        [InlineData("0x7FFFFFFF is pretty big", 10, 16, int.MaxValue)]
        [InlineData("", 0, 10, 0)]
        [InlineData("ThereIsNoNumberHere", 0, 10, 0)]
        public void strtol_Test(string srcString, ushort ushortPtr, ushort numBase, int expectedValue)
        {
            //Reset State
            Reset();

            //Allocate Variable to be Passed In
            var sourceStringPointer = mbbsEmuMemoryCore.AllocateVariable("SRC", (ushort)(srcString.Length));
            mbbsEmuMemoryCore.SetArray("SRC", Encoding.ASCII.GetBytes(srcString));

            //Get initial suffixPointer
            var sourceStringSuffixPointer = mbbsEmuMemoryCore.GetVariablePointer("SRC");

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, STRTOL_ORDINAL,
                new List<ushort>
                {
                    sourceStringPointer.Offset,
                    sourceStringPointer.Segment,
                    sourceStringSuffixPointer.Offset,
                    sourceStringSuffixPointer.Segment,
                    numBase
                });

            //Get returned suffixPointer
            var returnedSuffixPointer = mbbsEmuMemoryCore.GetPointer(sourceStringSuffixPointer);

            //Verify Results
            Assert.Equal((ushort)(expectedValue & 0xFFFF), mbbsEmuCpuRegisters.AX);
            Assert.Equal((ushort)(expectedValue >> 16), mbbsEmuCpuRegisters.DX);
            Assert.Equal(sourceStringPointer.Segment, returnedSuffixPointer.Segment);
            Assert.Equal(sourceStringPointer.Offset + ushortPtr, returnedSuffixPointer.Offset);
        }

        /// <summary>
        ///     Expected values are the result (DX:AX) and the offset *suffixPP was set to by the original kernels'
        ///     STRTOL export, executed on the same strings and radixes (The Major BBS 6.x and Worldgroup MAJORBBS.EXE
        ///     give identical results): saturation at LONG_MAX/LONG_MIN, radix 0 octal and hexadecimal prefixes, the
        ///     end of the digits read, and the start of the string when nothing was read.
        /// </summary>
        [Theory]
        [InlineData("123", 10, 0x0000007Bu, 3)]
        [InlineData("123abc", 10, 0x0000007Bu, 3)]
        [InlineData("  -42x", 10, 0xFFFFFFD6u, 5)]
        [InlineData("\t+7 8", 10, 0x00000007u, 3)]
        [InlineData("2147483647", 10, 0x7FFFFFFFu, 10)]
        [InlineData("2147483648", 10, 0x7FFFFFFFu, 10)]
        [InlineData("4000000000", 10, 0x7FFFFFFFu, 10)]
        [InlineData("-2147483648", 10, 0x80000000u, 11)]
        [InlineData("-2147483649", 10, 0x80000000u, 11)]
        [InlineData("99999999999999999999", 10, 0x7FFFFFFFu, 10)]
        [InlineData("", 10, 0x00000000u, 0)]
        [InlineData("abc", 10, 0x00000000u, 0)]
        [InlineData("+", 10, 0x00000000u, 0)]
        [InlineData("-", 10, 0x00000000u, 0)]
        [InlineData("   ", 10, 0x00000000u, 0)]
        [InlineData("0x1A", 0, 0x0000001Au, 4)]
        [InlineData("0X1a", 0, 0x0000001Au, 4)]
        [InlineData("012", 0, 0x0000000Au, 3)]
        [InlineData("09", 0, 0x00000000u, 1)]
        [InlineData("0", 0, 0x00000000u, 1)]
        [InlineData("0x", 0, 0x00000000u, 2)]
        [InlineData(" 0xZ", 0, 0x00000000u, 3)]
        [InlineData("1A", 16, 0x0000001Au, 2)]
        [InlineData("0x1A", 16, 0x0000001Au, 4)]
        [InlineData("FFFFFFFF", 16, 0x7FFFFFFFu, 8)]
        [InlineData("7FFFFFFF", 16, 0x7FFFFFFFu, 8)]
        [InlineData("80000000", 16, 0x7FFFFFFFu, 8)]
        [InlineData("-1", 16, 0xFFFFFFFFu, 2)]
        [InlineData("zz", 36, 0x0000050Fu, 2)]
        [InlineData("ZZ", 36, 0x0000050Fu, 2)]
        [InlineData("777", 8, 0x000001FFu, 3)]
        [InlineData("8", 8, 0x00000000u, 0)]
        [InlineData("101", 2, 0x00000005u, 3)]
        [InlineData("12", 2, 0x00000001u, 1)]
        [InlineData("123", 1, 0x00000000u, 0)]
        [InlineData("123", 37, 0x00000000u, 0)]
        [InlineData("5", -1, 0x00000000u, 0)]
        [InlineData("12345", 0, 0x00003039u, 5)]
        [InlineData("-0x10", 0, 0xFFFFFFF0u, 5)]
        [InlineData("+0x10", 0, 0x00000010u, 5)]
        [InlineData("21474836470", 10, 0x7FFFFFFFu, 11)]
        [InlineData("3000000000000", 10, 0x7FFFFFFFu, 11)]
        [InlineData("-99999999999", 10, 0x80000000u, 11)]
        [InlineData("-21474836480", 10, 0x80000000u, 12)]
        [InlineData("123456789ABC", 16, 0x7FFFFFFFu, 9)]
        [InlineData("0x", 16, 0x00000000u, 2)]
        [InlineData("0xG", 16, 0x00000000u, 2)]
        [InlineData("0x12", 8, 0x00000000u, 1)]
        [InlineData("0X", 0, 0x00000000u, 2)]
        [InlineData("-0x", 0, 0x80000000u, 3)]
        [InlineData("00x1", 0, 0x00000000u, 2)]
        [InlineData("0b1", 0, 0x00000000u, 1)]
        [InlineData("\v\f\r\n5", 10, 0x00000005u, 5)]
        [InlineData("\u00A05", 10, 0x00000000u, 0)]
        [InlineData("1 2", 10, 0x00000001u, 1)]
        [InlineData("2147483647x", 10, 0x7FFFFFFFu, 10)]
        [InlineData("-2147483648x", 10, 0x80000000u, 11)]
        [InlineData("4294967295", 10, 0x7FFFFFFFu, 10)]
        [InlineData("4294967296", 10, 0x7FFFFFFFu, 10)]
        [InlineData("-", 0, 0x00000000u, 0)]
        [InlineData("0", 8, 0x00000000u, 1)]
        [InlineData("-0x", 16, 0x80000000u, 3)]
        [InlineData("+0x", 0, 0x00000000u, 3)]
        [InlineData("-0", 0, 0x80000000u, 2)]
        [InlineData("-0", 10, 0x80000000u, 2)]
        [InlineData("-08", 0, 0x80000000u, 2)]
        [InlineData("-1x", 0, 0xFFFFFFFFu, 2)]
        [InlineData("0xFFFFFFFF", 0, 0x7FFFFFFFu, 10)]
        [InlineData("-0xFFFFFFFF", 0, 0x80000000u, 11)]
        [InlineData("-80000000", 16, 0x80000000u, 9)]
        [InlineData("-80000001", 16, 0x80000000u, 9)]
        public void strtol_MatchesKernel(string srcString, int radix, uint expectedValue, ushort expectedSuffixOffset)
        {
            Reset();

            var sourceBytes = Encoding.Latin1.GetBytes(srcString + "\0");
            var sourceStringPointer = mbbsEmuMemoryCore.AllocateVariable("SRC", (ushort)sourceBytes.Length);
            mbbsEmuMemoryCore.SetArray("SRC", sourceBytes);
            var suffixPointer = mbbsEmuMemoryCore.AllocateVariable("SUFFIX", FarPtr.Size);

            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, STRTOL_ORDINAL,
                new List<ushort>
                {
                    sourceStringPointer.Offset,
                    sourceStringPointer.Segment,
                    suffixPointer.Offset,
                    suffixPointer.Segment,
                    (ushort)radix
                });

            var returnedSuffixPointer = mbbsEmuMemoryCore.GetPointer(suffixPointer);

            Assert.Equal((ushort)(expectedValue & 0xFFFF), mbbsEmuCpuRegisters.AX);
            Assert.Equal((ushort)(expectedValue >> 16), mbbsEmuCpuRegisters.DX);
            Assert.Equal(sourceStringPointer.Segment, returnedSuffixPointer.Segment);
            Assert.Equal(sourceStringPointer.Offset + expectedSuffixOffset, returnedSuffixPointer.Offset);
        }

        [Fact]
        public void strtol_nullsuffix_Test()
        {
            Reset();

            const string srcString = "123456 Hello some text";
            const ushort numBase = 10;
            const int expectedValue = 123456;

            //Allocate Variable to be Passed In
            var sourceStringPointer = mbbsEmuMemoryCore.AllocateVariable("SRC", (ushort)(srcString.Length));
            mbbsEmuMemoryCore.SetArray("SRC", Encoding.ASCII.GetBytes(srcString));

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, STRTOL_ORDINAL,
                new List<ushort>
                {
                    sourceStringPointer.Offset,
                    sourceStringPointer.Segment,
                    0,
                    0,
                    numBase
                });

            //Verify Results
            Assert.Equal((ushort)(expectedValue & 0xFFFF), mbbsEmuCpuRegisters.AX);
            Assert.Equal((ushort)(expectedValue >> 16), mbbsEmuCpuRegisters.DX);
        }
    }
}
