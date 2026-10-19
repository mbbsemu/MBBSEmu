using FluentAssertions;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class sprintf_Tests : ExportedModuleTestBase
    {
        private const int SPRINTF_ORDINAL = 560;

        private List<ushort> parameters = new List<ushort>();

        [Theory]
        [InlineData("%d", "1", (ushort)1)]
        [InlineData("%d", "0", (ushort)0)]
        [InlineData("%d", "-1", (ushort)0xFFFF)]
        [InlineData("%u", "1", (ushort)1)]
        [InlineData("%u", "0", (ushort)0)]
        [InlineData("%u", "65535", (ushort)0xFFFF)]
        [InlineData("ITEM%3.3d", "ITEM010", (ushort)10)]
        [InlineData("ITEM%3d", "ITEM 10", (ushort)10)]
        [InlineData("ITEM%3.3d", "ITEM100", (ushort)100)]
        [InlineData("ITEM%3d", "ITEM100", (ushort)100)]
        [InlineData("Level: %5d", "Level:     3", (ushort)3)]
        [InlineData("Level: %-5d", "Level: 3    ", (ushort)3)]
        [InlineData("Level: %5.5d", "Level: 00003", (ushort)3)]
        [InlineData("Level: %-5.5d", "Level: 00003", (ushort)3)]
        [InlineData("%s-%d", "TEST-1", "TEST", (ushort)1)]
        [InlineData("%s-%ld", "TEST-2147483647", "TEST", 2147483647)]
        [InlineData("%s-%ld-%d-%s", "TEST-2147483647-1-FOO", "TEST", 2147483647, (ushort)1, "FOO")]
        [InlineData("%s-%ld-%d-%s", "TEST--1-1-FOO", "TEST", (uint)0xFFFFFFFF, (ushort)1, "FOO")]
        [InlineData("%s-%lu-%d-%s", "TEST-2147483647-1-FOO", "TEST", 2147483647u, (ushort)1, "FOO")]
        [InlineData("%s-%lu-%d-%s", "TEST-3147483647-1-FOO", "TEST", 3147483647u, (ushort)1, "FOO")]
        [InlineData("99% of the time, this will print %s", "9910f the time, this will print TEST", (ushort)8, "TEST")] //"% o" is a valid octal conversion
        [InlineData("Mid 50% Test", "Mid 50% Test", null)] //Unescaped %
        [InlineData("End 50% ", "End 50% ", null)] //Unescaped %
        [InlineData("End 50%", "End 50%", null)] //Unescaped %
        [InlineData("This is 100%% accurate", "This is 100% accurate", null)] //Escaped %
        [InlineData("%%%%", "%%", null)] //Escaped %
        [InlineData("%%%%%", "%%%", null)] //Escaped & Unescaped %
        [InlineData("%%%%% ", "%%% ", null)] //Escaped & Unescaped %
        [InlineData("%-8s", "gold crowns", "gold crowns")]
        [InlineData("%-8s", "gold    ", "gold")]
        [InlineData("%f", "12.345600", 12.3456)]
        [InlineData("%.2f", "12.35", 12.3456)]
        [InlineData("%.0f", "12", 12.3456)]
        [InlineData("%8.2f", "   12.35", 12.3456)]
        [InlineData("%-8.2f", "12.35   ", 12.3456)]
        //Hex conversions (#683): accepted by PRINTF_SPECIFIERS but previously unimplemented
        [InlineData("%x", "ff", (ushort)255)]
        [InlineData("%X", "FF", (ushort)255)]
        [InlineData("0x%x", "0x13", (ushort)19)]
        [InlineData("%lx", "deadbeef", (uint)0xDEADBEEF)]
        //Unknown conversion characters are output as-is
        [InlineData("%q", "%q", null)]
        [InlineData("50%-", "50%-", null)]
        //Flags
        [InlineData("%02d", "05", (ushort)5)]
        [InlineData("%05d", "-0005", (ushort)0xFFFB)]
        [InlineData("%+d", "+5", (ushort)5)]
        [InlineData("%+d", "-5", (ushort)0xFFFB)]
        [InlineData("% d", " 5", (ushort)5)]
        [InlineData("%+ d", "+5", (ushort)5)]
        [InlineData("% u", "5", (ushort)5)]
        [InlineData("%-05d|", "5    |", (ushort)5)]
        //Integer precision is the minimum number of digits, and disables the 0 flag
        [InlineData("%.3d", "007", (ushort)7)]
        [InlineData("%5.3d", "  007", (ushort)7)]
        [InlineData("%-5.3d|", "007  |", (ushort)7)]
        [InlineData("%05.3d", "  007", (ushort)7)]
        [InlineData("%.0d", "0", (ushort)0)]
        [InlineData("%.3d", "-007", (ushort)0xFFF9)]
        //Octal, hex and the alternate form
        [InlineData("%o", "10", (ushort)8)]
        [InlineData("%#o", "010", (ushort)8)]
        [InlineData("%#x", "0xff", (ushort)255)]
        [InlineData("%#X", "0XFF", (ushort)255)]
        [InlineData("%#x", "0", (ushort)0)]
        [InlineData("%#06x", "0x00ff", (ushort)255)]
        [InlineData("%08lX", "DEADBEEF", (uint)0xDEADBEEF)]
        [InlineData("%lo", "37777777777", (uint)0xFFFFFFFF)]
        //Size modifiers
        [InlineData("%hd", "-1", (ushort)0xFFFF)]
        [InlineData("%hu", "65535", (ushort)0xFFFF)]
        [InlineData("%ld", "-2147483648", (uint)0x80000000)]
        //Width and precision from the argument list
        [InlineData("%*d", "   42", (ushort)5, (ushort)42)]
        [InlineData("%*d|", "42   |", (ushort)0xFFFB, (ushort)42)]
        [InlineData("%.*d", "0042", (ushort)4, (ushort)42)]
        [InlineData("[%.*s]", "[ab]", (ushort)2, "abcdef")]
        //Strings and characters
        [InlineData("[%.3s]", "[abc]", "abcdefgh")]
        [InlineData("[%-6.2s]", "[ab    ]", "abcdefgh")]
        [InlineData("[%6s]", "[   abc]", "abc")]
        [InlineData("[%06s]", "[   abc]", "abc")]
        [InlineData("[%.0s]", "[]", "abc")]
        [InlineData("%c%c", "AB", (ushort)'A', (ushort)'B')]
        [InlineData("[%3c]", "[  A]", (ushort)'A')]
        //Backslashes are ordinary characters at runtime
        [InlineData("C:\\temp\\new\\x41", "C:\\temp\\new\\x41", null)]
        //Floating point
        [InlineData("%e", "1.234560e+01", 12.3456)]
        [InlineData("%E", "1.234560E+01", 12.3456)]
        [InlineData("%.2e", "1.23e-05", 0.0000123)]
        [InlineData("%.0e", "1e+02", 123.0)]
        [InlineData("%#.0e", "1.e+02", 123.0)]
        [InlineData("%e", "0.000000e+00", 0.0)]
        [InlineData("%g", "12.3456", 12.3456)]
        [InlineData("%g", "1e-05", 0.00001)]
        [InlineData("%g", "0.0001", 0.0001)]
        [InlineData("%g", "1.23457e+06", 1234567.0)]
        [InlineData("%g", "100000", 100000.0)]
        [InlineData("%G", "1E+20", 1e20)]
        [InlineData("%g", "0", 0.0)]
        [InlineData("%#g", "1.00000", 1.0)]
        [InlineData("%.3g", "3.14", 3.14159)]
        [InlineData("%#.0f", "12.", 12.3456)]
        [InlineData("%+.1f", "+2.5", 2.5)]
        [InlineData("% .1f", " 2.5", 2.5)]
        [InlineData("%08.2f", "-0012.35", -12.3456)]
        [InlineData("%f", "-0.000000", -0.0)]
        [InlineData("%f", "+INF", double.PositiveInfinity)]
        [InlineData("%f", "-INF", double.NegativeInfinity)]
        [InlineData("%8f|", "    +INF|", double.PositiveInfinity)]
        [InlineData("%lf", "1.500000", 1.5)]
        public void sprintf_Test(string formatString, string expectedString, params object[] values)
        {
            Reset();

            var destBuffer = mbbsEmuMemoryCore.Malloc((ushort)(expectedString.Length * 2));
            var formatStringParameterPointer = mbbsEmuMemoryCore.Malloc((ushort)(formatString.Length + 1));
            mbbsEmuMemoryCore.SetArray(formatStringParameterPointer, Encoding.ASCII.GetBytes(formatString));

            parameters.Add(destBuffer.Offset);
            parameters.Add(destBuffer.Segment);

            parameters.Add(formatStringParameterPointer.Offset);
            parameters.Add(formatStringParameterPointer.Segment);

            if (values != null)
            {
                var parameterList = GenerateParameters(values);
                foreach (var p in parameterList)
                    parameters.Add(p);
            }

            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, SPRINTF_ORDINAL, parameters);

            Encoding.ASCII.GetString(mbbsEmuMemoryCore.GetString(destBuffer, true)).Should().Be(expectedString);
        }

        /// <summary>
        ///     Runs sprintf() with raw argument words. Call Reset() first.
        /// </summary>
        private string Sprintf(string formatString, params ushort[] arguments)
        {
            var destBuffer = mbbsEmuMemoryCore.Malloc(256);
            var formatStringParameterPointer = mbbsEmuMemoryCore.Malloc((ushort)(formatString.Length + 1));
            mbbsEmuMemoryCore.SetArray(formatStringParameterPointer, Encoding.ASCII.GetBytes(formatString));

            parameters.Add(destBuffer.Offset);
            parameters.Add(destBuffer.Segment);
            parameters.Add(formatStringParameterPointer.Offset);
            parameters.Add(formatStringParameterPointer.Segment);
            parameters.AddRange(arguments);

            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, SPRINTF_ORDINAL, parameters);

            return Encoding.ASCII.GetString(mbbsEmuMemoryCore.GetString(destBuffer, true));
        }

        [Fact]
        public void sprintf_NullString_PrintsNull()
        {
            Reset();
            Sprintf("[%s]", 0, 0).Should().Be("[(null)]");
        }

        [Fact]
        public void sprintf_FarPointer()
        {
            Reset();
            Sprintf("%p", 0x1234, 0xABCD).Should().Be("ABCD:1234");
        }

        [Fact]
        public void sprintf_NearPointer()
        {
            Reset();
            Sprintf("%Np|%d", 0x1234, 7).Should().Be("1234|7");
        }

        [Fact]
        public void sprintf_LongDouble()
        {
            Reset();
            //1.5 as an 80-bit extended: mantissa 0xC000000000000000, biased exponent 0x3FFF
            Sprintf("%Lf|%d", 0x0000, 0x0000, 0x0000, 0xC000, 0x3FFF, 7).Should().Be("1.500000|7");
        }

        [Fact]
        public void sprintf_CharactersWritten()
        {
            Reset();
            var count = mbbsEmuMemoryCore.Malloc(2);
            mbbsEmuMemoryCore.SetWord(count, 0xFFFF);

            Sprintf("ab%ncd", count.Offset, count.Segment).Should().Be("abcd");
            mbbsEmuMemoryCore.GetWord(count).Should().Be(2);
        }
    }

    public class vsprintf_Tests : ExportedModuleTestBase
    {
        private const int VSPRINTF_ORDINAL = 643;

        [Theory]
        [InlineData("%02d:%02d", "07:05", (ushort)7, (ushort)5)]
        [InlineData("%s=%ld", "KEY=-2", "KEY", (uint)0xFFFFFFFE)]
        [InlineData("[%-5.2s]%c", "[ab   ]Z", "abc", (ushort)'Z')]
        [InlineData("%.2f %d", "1.50 3", 1.5, (ushort)3)]
        [InlineData("%*d", "   42", (ushort)5, (ushort)42)]
        public void vsprintf_Test(string formatString, string expectedString, params object[] values)
        {
            Reset();

            var destBuffer = mbbsEmuMemoryCore.Malloc(256);
            var formatStringParameterPointer = mbbsEmuMemoryCore.Malloc((ushort)(formatString.Length + 1));
            mbbsEmuMemoryCore.SetArray(formatStringParameterPointer, Encoding.ASCII.GetBytes(formatString));

            //va_list: the arguments laid out in memory exactly as they would be on the stack
            var arguments = GenerateParameters(values);
            var vaList = mbbsEmuMemoryCore.Malloc((ushort)(arguments.Count * 2));
            for (var i = 0; i < arguments.Count; i++)
                mbbsEmuMemoryCore.SetWord(vaList + (i * 2), arguments[i]);

            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, VSPRINTF_ORDINAL, new List<ushort>
            {
                destBuffer.Offset, destBuffer.Segment,
                formatStringParameterPointer.Offset, formatStringParameterPointer.Segment,
                vaList.Offset, vaList.Segment
            });

            Encoding.ASCII.GetString(mbbsEmuMemoryCore.GetString(destBuffer, true)).Should().Be(expectedString);
        }
    }
}
