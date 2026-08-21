using MBBSEmu.Session.LocalConsole;
using System;
using System.Text;
using Xunit;

namespace MBBSEmu.Tests.Session.LocalConsole
{
    public class LocalConsoleSession_Tests
    {
        /// <summary>
        ///     CP437 0xBA, the double vertical line that draws the border of most module ANSI
        ///     art. LocalConsoleSession maps it to U+2551 before writing it to the console.
        /// </summary>
        private const ushort BOX_DRAWINGS_DOUBLE_VERTICAL = 0x2551;

        /// <summary>
        ///     Produces the bytes a terminal receives for one extended character, reproducing
        ///     the conversion in LocalConsoleSession.UnicodeANSIOutput: the lookup table's
        ///     UTF-16 code unit is turned into a string, which the console's output encoding
        ///     then encodes. Console strips the encoding preamble itself, so encoding the
        ///     string directly is what a single Console.Write emits.
        /// </summary>
        private static byte[] Encode(ushort unicodeCodePoint, Encoding encoding) =>
            encoding.GetBytes(Encoding.Unicode.GetString(BitConverter.GetBytes(unicodeCodePoint)));

        [Fact]
        public void NonWindows_EncodesExtendedCharactersAsUTF8()
        {
            var encoding = LocalConsoleSession.GetConsoleOutputEncoding(isWindows: false);

            //U+2551 is three bytes in UTF-8, which is what a Unix terminal expects
            Assert.Equal(new byte[] { 0xE2, 0x95, 0x91 }, Encode(BOX_DRAWINGS_DOUBLE_VERTICAL, encoding));
        }

        [Fact]
        public void NonWindows_LeavesASCIIUntouched()
        {
            var encoding = LocalConsoleSession.GetConsoleOutputEncoding(isWindows: false);

            Assert.Equal(new byte[] { 0x41 }, Encode('A', encoding));
        }

        [Fact]
        public void NonWindows_EmitsNoByteOrderMark()
        {
            var encoding = LocalConsoleSession.GetConsoleOutputEncoding(isWindows: false);

            Assert.Empty(encoding.GetPreamble());
        }

        [Fact]
        public void Windows_KeepsUTF16SoWriteConsoleWStillReceivesIt()
        {
            var encoding = LocalConsoleSession.GetConsoleOutputEncoding(isWindows: true);

            Assert.Equal(Encoding.Unicode, encoding);
        }

        /// <summary>
        ///     Pins the defect this fix exists for: under the previous unconditional UTF-16
        ///     output, U+2551 reached a UTF-8 terminal as 0x51 0x25 and rendered as "Q%".
        /// </summary>
        [Fact]
        public void UTF16Output_IsWhatMangledExtendedCharactersOnUnix()
        {
            var mangled = Encode(BOX_DRAWINGS_DOUBLE_VERTICAL, Encoding.Unicode);

            Assert.Equal(new byte[] { 0x51, 0x25 }, mangled);
            Assert.Equal("Q%", Encoding.ASCII.GetString(mangled));
        }

        /// <summary>
        ///     ASCII survived the UTF-16 output only because its high byte is NUL, which
        ///     terminals discard. This is why the defect looked cosmetic rather than total.
        /// </summary>
        [Fact]
        public void UTF16Output_PaddedASCIIWithANullByte()
        {
            Assert.Equal(new byte[] { 0x41, 0x00 }, Encode('A', Encoding.Unicode));
        }
    }
}
