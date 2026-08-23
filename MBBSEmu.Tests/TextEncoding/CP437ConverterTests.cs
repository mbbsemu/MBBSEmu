using MBBSEmu.TextEncoding;
using System;
using System.Text;
using FluentAssertions;
using Xunit;

namespace MBBSEmu.Tests.TextEncoding
{
    public class CP437ConverterTests
    {
        #region Standard ASCII (0x00-0x7F) Passthrough

        [Fact]
        public void ConvertToUtf8_StandardAscii_PassesThroughUnchanged()
        {
            var input = Encoding.ASCII.GetBytes("Hello, World! 0123456789");
            var result = CP437Converter.ConvertToUtf8(input);

            result.Should().BeEquivalentTo(input);
        }

        [Fact]
        public void ConvertToUtf8_ControlCodes_PassThroughUnchanged()
        {
            // All C0 control codes pass through as-is (not converted to CP437 glyphs)
            byte[] input = { 0x00, 0x01, 0x07, 0x08, 0x09, 0x0A, 0x0D, 0x11, 0x13, 0x1B };
            var result = CP437Converter.ConvertToUtf8(input);

            result.Should().BeEquivalentTo(input);
        }

        [Fact]
        public void ConvertToUtf8_AllLowBytes_PassThroughUnchanged()
        {
            // Entire 0x00-0x1F range should pass through as control codes
            var input = new byte[0x20];
            for (byte i = 0; i < 0x20; i++)
                input[i] = i;

            var result = CP437Converter.ConvertToUtf8(input);
            result.Should().BeEquivalentTo(input);
        }

        [Fact]
        public void ConvertToUtf8_AnsiEscapeSequence_PreservedIntact()
        {
            // ANSI color sequence: ESC[1;31m (bold red)
            byte[] input = { 0x1B, 0x5B, 0x31, 0x3B, 0x33, 0x31, 0x6D };  // ESC[1;31m
            var result = CP437Converter.ConvertToUtf8(input);

            result.Should().BeEquivalentTo(input);
            Encoding.ASCII.GetString(result).Should().Be("\x1B[1;31m");
        }

        [Fact]
        public void ConvertToUtf8_AnsiCursorPosition_PreservedIntact()
        {
            // ANSI cursor position: ESC[10;20H
            byte[] input = { 0x1B, 0x5B, 0x31, 0x30, 0x3B, 0x32, 0x30, 0x48 };  // ESC[10;20H
            var result = CP437Converter.ConvertToUtf8(input);

            result.Should().BeEquivalentTo(input);
        }

        #endregion

        #region Accented Characters (0x80-0xAF)

        [Theory]
        [InlineData(0x80, 0x00C7, "Ç")]  // C with cedilla
        [InlineData(0x81, 0x00FC, "ü")]  // u with umlaut
        [InlineData(0x82, 0x00E9, "é")]  // e with acute
        [InlineData(0x84, 0x00E4, "ä")]  // a with umlaut
        [InlineData(0x8E, 0x00C4, "Ä")]  // A with umlaut
        [InlineData(0x99, 0x00D6, "Ö")]  // O with umlaut
        [InlineData(0x9A, 0x00DC, "Ü")]  // U with umlaut
        [InlineData(0xA4, 0x00F1, "ñ")]  // n with tilde
        [InlineData(0xA5, 0x00D1, "Ñ")]  // N with tilde
        public void ConvertToUtf8_AccentedCharacters_ConvertsCorrectly(byte cp437, ushort expectedCodePoint, string expectedChar)
        {
            byte[] input = { cp437 };
            var result = CP437Converter.ConvertToUtf8(input);
            var resultString = Encoding.UTF8.GetString(result);

            resultString.Should().Be(expectedChar);
            CP437Converter.GetUnicodeCodePoint(cp437).Should().Be(expectedCodePoint);
        }

        [Theory]
        [InlineData(0x9B, 0x00A2, "¢")]  // cent sign
        [InlineData(0x9C, 0x00A3, "£")]  // pound sign
        [InlineData(0x9D, 0x00A5, "¥")]  // yen sign
        [InlineData(0x9F, 0x0192, "ƒ")]  // florin
        public void ConvertToUtf8_CurrencySymbols_ConvertsCorrectly(byte cp437, ushort expectedCodePoint, string expectedChar)
        {
            byte[] input = { cp437 };
            var result = CP437Converter.ConvertToUtf8(input);
            var resultString = Encoding.UTF8.GetString(result);

            resultString.Should().Be(expectedChar);
            CP437Converter.GetUnicodeCodePoint(cp437).Should().Be(expectedCodePoint);
        }

        #endregion

        #region Box Drawing Characters (0xB0-0xDF)

        [Theory]
        [InlineData(0xB0, 0x2591, "░")]  // light shade
        [InlineData(0xB1, 0x2592, "▒")]  // medium shade
        [InlineData(0xB2, 0x2593, "▓")]  // dark shade
        [InlineData(0xB3, 0x2502, "│")]  // vertical line
        [InlineData(0xBA, 0x2551, "║")]  // double vertical
        [InlineData(0xC4, 0x2500, "─")]  // horizontal line
        [InlineData(0xCD, 0x2550, "═")]  // double horizontal
        [InlineData(0xC9, 0x2554, "╔")]  // double top-left corner
        [InlineData(0xBB, 0x2557, "╗")]  // double top-right corner
        [InlineData(0xC8, 0x255A, "╚")]  // double bottom-left corner
        [InlineData(0xBC, 0x255D, "╝")]  // double bottom-right corner
        [InlineData(0xDB, 0x2588, "█")]  // full block
        [InlineData(0xDC, 0x2584, "▄")]  // lower half block
        [InlineData(0xDF, 0x2580, "▀")]  // upper half block
        public void ConvertToUtf8_BoxDrawing_ConvertsCorrectly(byte cp437, ushort expectedCodePoint, string expectedChar)
        {
            byte[] input = { cp437 };
            var result = CP437Converter.ConvertToUtf8(input);
            var resultString = Encoding.UTF8.GetString(result);

            resultString.Should().Be(expectedChar);
            CP437Converter.GetUnicodeCodePoint(cp437).Should().Be(expectedCodePoint);
        }

        [Fact]
        public void ConvertToUtf8_BoxDrawingSequence_ConvertsCorrectly()
        {
            // A simple box: ╔═╗
            //               ║ ║
            //               ╚═╝
            byte[] input = { 0xC9, 0xCD, 0xBB, 0x0D, 0x0A,  // ╔═╗\r\n
                             0xBA, 0x20, 0xBA, 0x0D, 0x0A,  // ║ ║\r\n
                             0xC8, 0xCD, 0xBC };            // ╚═╝
            var result = CP437Converter.ConvertToUtf8(input);
            var resultString = Encoding.UTF8.GetString(result);

            resultString.Should().Be("╔═╗\r\n║ ║\r\n╚═╝");
        }

        #endregion

        #region Greek and Math Symbols (0xE0-0xFF)

        [Theory]
        [InlineData(0xE0, 0x03B1, "α")]  // alpha
        [InlineData(0xE1, 0x00DF, "ß")]  // German sharp s (not Cyrillic!)
        [InlineData(0xE2, 0x0393, "Γ")]  // Gamma
        [InlineData(0xE3, 0x03C0, "π")]  // pi
        [InlineData(0xE4, 0x03A3, "Σ")]  // Sigma
        [InlineData(0xE6, 0x00B5, "µ")]  // micro sign
        [InlineData(0xEC, 0x221E, "∞")]  // infinity
        [InlineData(0xF1, 0x00B1, "±")]  // plus-minus
        [InlineData(0xF6, 0x00F7, "÷")]  // division
        [InlineData(0xF8, 0x00B0, "°")]  // degree
        [InlineData(0xFB, 0x221A, "√")]  // square root
        [InlineData(0xFD, 0x00B2, "²")]  // superscript 2
        [InlineData(0xFE, 0x25A0, "■")]  // black square
        public void ConvertToUtf8_GreekAndMath_ConvertsCorrectly(byte cp437, ushort expectedCodePoint, string expectedChar)
        {
            byte[] input = { cp437 };
            var result = CP437Converter.ConvertToUtf8(input);
            var resultString = Encoding.UTF8.GetString(result);

            resultString.Should().Be(expectedChar);
            CP437Converter.GetUnicodeCodePoint(cp437).Should().Be(expectedCodePoint);
        }

        #endregion

        #region Regression Tests for Previously Broken Mappings

        [Fact]
        public void ConvertToUtf8_0xCF_IsNotByteSwapped()
        {
            // 0xCF was incorrectly 0x6725 (byte-swapped), should be 0x2567 (╧)
            byte[] input = { 0xCF };
            var result = CP437Converter.ConvertToUtf8(input);
            var resultString = Encoding.UTF8.GetString(result);

            resultString.Should().Be("╧");
            CP437Converter.GetUnicodeCodePoint(0xCF).Should().Be(0x2567);
        }

        [Fact]
        public void ConvertToUtf8_0xE1_IsGermanSharpS_NotCyrillic()
        {
            // 0xE1 was incorrectly 0x04F1 (Cyrillic), should be 0x00DF (ß)
            byte[] input = { 0xE1 };
            var result = CP437Converter.ConvertToUtf8(input);
            var resultString = Encoding.UTF8.GetString(result);

            resultString.Should().Be("ß");
            CP437Converter.GetUnicodeCodePoint(0xE1).Should().Be(0x00DF);
        }

        [Fact]
        public void ConvertToUtf8_0x80Range_IsNotC1ControlCodes()
        {
            // 0x80-0x9F were incorrectly identity-mapped to C1 control codes
            // They should map to accented characters
            CP437Converter.GetUnicodeCodePoint(0x80).Should().Be(0x00C7);  // Ç, not 0x0080
            CP437Converter.GetUnicodeCodePoint(0x81).Should().Be(0x00FC);  // ü, not 0x0081
            CP437Converter.GetUnicodeCodePoint(0x82).Should().Be(0x00E9);  // é, not 0x0082
        }

        [Fact]
        public void ConvertToUtf8_0xA0Range_IsNotLatin1Supplement()
        {
            // 0xA0-0xAF were incorrectly identity-mapped
            // They should map to different Unicode code points
            CP437Converter.GetUnicodeCodePoint(0xA0).Should().Be(0x00E1);  // á, not 0x00A0
            CP437Converter.GetUnicodeCodePoint(0xA1).Should().Be(0x00ED);  // í, not 0x00A1
            CP437Converter.GetUnicodeCodePoint(0xA9).Should().Be(0x2310);  // ⌐, not 0x00A9
        }

        #endregion

        #region Mixed Content Tests

        [Fact]
        public void ConvertToUtf8_MixedAsciiAndExtended_ConvertsCorrectly()
        {
            // "Price: 50¢" with CP437 cent sign
            byte[] input = { 0x50, 0x72, 0x69, 0x63, 0x65, 0x3A, 0x20, 0x35, 0x30, 0x9B };
            var result = CP437Converter.ConvertToUtf8(input);
            var resultString = Encoding.UTF8.GetString(result);

            resultString.Should().Be("Price: 50¢");
        }

        [Fact]
        public void ConvertToUtf8_AnsiWithBoxDrawing_ConvertsCorrectly()
        {
            // ESC[1;31m followed by box char followed by ESC[0m
            byte[] input = { 0x1B, 0x5B, 0x31, 0x3B, 0x33, 0x31, 0x6D,  // ESC[1;31m
                             0xDB,                                       // █
                             0x1B, 0x5B, 0x30, 0x6D };                   // ESC[0m
            var result = CP437Converter.ConvertToUtf8(input);
            var resultString = Encoding.UTF8.GetString(result);

            resultString.Should().Be("\x1B[1;31m█\x1B[0m");
        }

        [Fact]
        public void ConvertToUtf8_EmptyInput_ReturnsEmpty()
        {
            byte[] input = Array.Empty<byte>();
            var result = CP437Converter.ConvertToUtf8(input);

            result.Should().BeEmpty();
        }

        #endregion

        #region GetUnicodeCodePoint Tests

        [Fact]
        public void GetUnicodeCodePoint_FullRange_ReturnsExpectedValues()
        {
            // Standard ASCII
            CP437Converter.GetUnicodeCodePoint(0x00).Should().Be(0x0000);
            CP437Converter.GetUnicodeCodePoint(0x41).Should().Be(0x0041);  // 'A'
            CP437Converter.GetUnicodeCodePoint(0x7F).Should().Be(0x007F);

            // Extended - verify a few from each row
            CP437Converter.GetUnicodeCodePoint(0x80).Should().Be(0x00C7);  // Ç
            CP437Converter.GetUnicodeCodePoint(0x90).Should().Be(0x00C9);  // É
            CP437Converter.GetUnicodeCodePoint(0xA0).Should().Be(0x00E1);  // á
            CP437Converter.GetUnicodeCodePoint(0xB0).Should().Be(0x2591);  // ░
            CP437Converter.GetUnicodeCodePoint(0xC0).Should().Be(0x2514);  // └
            CP437Converter.GetUnicodeCodePoint(0xD0).Should().Be(0x2568);  // ╨
            CP437Converter.GetUnicodeCodePoint(0xE0).Should().Be(0x03B1);  // α
            CP437Converter.GetUnicodeCodePoint(0xF0).Should().Be(0x2261);  // ≡
            CP437Converter.GetUnicodeCodePoint(0xFF).Should().Be(0x00A0);  // NBSP
        }

        #endregion
    }
}
