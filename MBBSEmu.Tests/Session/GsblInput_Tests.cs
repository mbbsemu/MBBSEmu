using MBBSEmu.HostProcess.Enums;
using MBBSEmu.Memory;
using MBBSEmu.Session;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;

namespace MBBSEmu.Tests.Session
{
    /// <summary>
    ///     GSBL line-mode input processing, ported from GALGSBL's input unit tests (test_input.cpp)
    /// </summary>
    public class GsblInput_Tests
    {
        private readonly TestSession _session = new TestSession(null, null);
        private readonly byte[] _translationTable = GsblInput.DefaultTranslationTable.ToArray();

        private void Type(string input, Func<byte, byte> interceptor = null)
        {
            foreach (var c in Encoding.Latin1.GetBytes(input))
                GsblInput.ProcessCharacter(_session, c, _translationTable, interceptor);
        }

        /// <summary>
        ///     Consumes the completed line in InputBuffer, as STTROU would
        /// </summary>
        private string Line()
        {
            var line = Encoding.Latin1.GetString(_session.InputBuffer.ToArray());
            _session.InputBuffer.SetLength(0);
            GsblInput.Synchronize(_session);
            return line;
        }

        private string Echo() => Encoding.Latin1.GetString(_session.DrainSentData());

        private List<EnumUserStatus> Statuses() => _session.Status.ToList();

        // --- line mode ---------------------------------------------------------------

        [Fact]
        public void ACompleteLineIsReadable()
        {
            Type("hello\r");

            Assert.Equal(new[] { EnumUserStatus.CR_TERMINATED_STRING_AVAILABLE }, Statuses());
            Assert.Equal("hello", Line());
        }

        [Fact]
        public void APartialLineIsNotReported()
        {
            Type("abc");

            Assert.Empty(Statuses());
            Assert.Equal("abc", Encoding.ASCII.GetString(_session.InputBuffer.ToArray()));
        }

        [Fact]
        public void InputIsEchoed()
        {
            Type("hi\r");

            Assert.Equal("hi\r\n", Echo());
        }

        [Fact]
        public void EchoCanBeTurnedOff()
        {
            _session.GsblInput.EchoEnabled = false;

            Type("hi\r");

            Assert.Equal("", Echo());
            Assert.Equal("hi", Line());
        }

        [Fact]
        public void TheLinefeedEchoIsConfigurable()
        {
            _session.GsblInput.LinefeedCharacter = (byte)'*';
            Type("a\r");
            Assert.Equal("a\r*", Echo());
            Line();

            _session.GsblInput.LinefeedCharacter = 0;
            Type("a\r");
            Assert.Equal("a\r", Echo());
        }

        [Fact]
        public void LinefeedAfterCrIsIgnored()
        {
            Type("a\r\nb\r\n");

            Assert.Equal("a", Line());
            Assert.Equal("b", Line());
            Assert.Equal("", Line());
            Assert.Equal((byte)'\n', _session.GsblInput.LastCharacter);
        }

        [Fact]
        public void ALoneLinefeedEndsTheLine()
        {
            Type("ab\n");

            Assert.Equal(new[] { EnumUserStatus.CR_TERMINATED_STRING_AVAILABLE }, Statuses());
            Assert.Equal("ab", Line());
        }

        [Fact]
        public void TheDefaultTranslationTable()
        {
            var x = GsblInput.DefaultTranslationTable;

            for (var c = 0x20; c < 0x7F; c++)
                Assert.Equal(c, x[c]);
            Assert.Equal((byte)'\b', x['\b']);
            Assert.Equal((byte)'\r', x['\r']);
            Assert.Equal((byte)'\r', x['\n']);
            Assert.Equal((byte)'\b', x[0x7F]);
            Assert.Equal(0, x[0x01]);
            Assert.Equal(0, x['\t']);
            Assert.Equal(0, x[0x1B]);
            Assert.Equal((byte)'a', x[0xE1]);
            Assert.Equal((byte)'\b', x[0xFF]);
            Assert.Equal(0, x[0x8A]);
        }

        [Fact]
        public void ControlCharactersAreDropped()
        {
            Type("a\x01\t\x1b" + "b\r");

            Assert.Equal("ab\r\n", Echo());
            Assert.Equal("ab", Line());
        }

        [Fact]
        public void HighBitCharactersAreStripped()
        {
            Type("\xC8\xE9\r");

            Assert.Equal("Hi", Line());
        }

        [Fact]
        public void BackspaceErases()
        {
            Type("abc\b\bx\r");

            Assert.Equal("abc\b \b\b \bx\r\n", Echo());
            Assert.Equal("ax", Line());
        }

        [Fact]
        public void DeleteActsAsBackspace()
        {
            Type("ab\x7f\r");

            Assert.Equal("a", Line());
        }

        [Fact]
        public void BackspaceAtTheStartOfALineIsIgnored()
        {
            Type("\b\ba\r");

            Assert.Equal("a\r\n", Echo());
            Assert.Equal("a", Line());
        }

        [Fact]
        public void ACustomBackspaceEchoIsSentAlone()
        {
            _session.GsblInput.BackspaceCharacter = (byte)'_';

            Type("ab\b\r");

            Assert.Equal("ab_\r\n", Echo());
        }

        [Fact]
        public void BackspaceHandlingCanBeTurnedOff()
        {
            _session.GsblInput.BackspaceCharacter = 0;

            Type("ab\b\r");

            Assert.Equal("ab\b", Line());
        }

        [Fact]
        public void BackspaceIsNotEchoedWithEchoOff()
        {
            _session.GsblInput.EchoEnabled = false;

            Type("ab\b\r");

            Assert.Equal("", Echo());
            Assert.Equal("a", Line());
        }

        [Fact]
        public void ACustomLineTerminator()
        {
            _session.GsblInput.LineTerminator = (byte)'#';

            Type("a\rb#");

            Assert.Equal(new[] { EnumUserStatus.CR_TERMINATED_STRING_AVAILABLE }, Statuses());
            Assert.Equal("a\rb", Line());
        }

        [Fact]
        public void LockedInputIsIgnored()
        {
            _session.InputLockout = true;
            Type("abc\r");

            Assert.Equal(0, _session.InputBuffer.Length);
            Assert.Equal("", Echo());
            Assert.Empty(Statuses());

            _session.InputLockout = false;
            Type("d\r");
            Assert.Equal("d", Line());
        }

        [Fact]
        public void XoffIsNeverInput()
        {
            Type("a\x13" + "b\r", c => c);

            Assert.Equal("ab", Line());
        }

        [Fact]
        public void TheOutputAbortCharacterIsNeverInput()
        {
            _session.GsblInput.OutputAbortCharacter = (byte)'!';

            Type("a!b\r");

            Assert.Equal("ab", Line());
        }

        [Fact]
        public void AnInterceptorSeesAndTranslatesInput()
        {
            var seen = new List<int>();
            byte Interceptor(byte c)
            {
                seen.Add(c);
                return c == 'x' ? (byte)0 : c == 'a' ? (byte)'A' : c;
            }

            Type("axb\x01\r", Interceptor);

            Assert.Equal(new[] { 'a', 'x', 'b', 1, '\r' }, seen);
            Assert.Equal("Ab\x01", Line()); //The translation table is bypassed
        }

        [Fact]
        public void AnInterceptorDropReportsIt()
        {
            var result = GsblInput.ProcessCharacter(_session, (byte)'x', _translationTable, _ => 0);

            Assert.Equal(EnumGsblInputResult.InterceptorDropped, result);
        }

        [Fact]
        public void TheEchoIsTheTranslatedCharacter()
        {
            Type("a\r", c => c == 'a' ? (byte)'A' : c);

            Assert.Equal("A\r\n", Echo());
        }

        [Fact]
        public void TheTranslationTableCanBeChanged()
        {
            var globalCache = new GlobalCache();
            var table = GsblInput.GetTranslationTable(globalCache);
            table['a'] = (byte)'b';
            table['c'] = 0;

            Assert.Same(table, GsblInput.GetTranslationTable(globalCache));

            foreach (var c in Encoding.ASCII.GetBytes("abc\r"))
                GsblInput.ProcessCharacter(_session, c, table, null);

            Assert.Equal("bb", Line());
        }

        [Fact]
        public void SecureEchoMasksAndLimitsInput()
        {
            _session.EchoSecureEnabled = true;
            _session.ExtUsrAcc.ech = (byte)'*';
            _session.ExtUsrAcc.wid = 3;

            Type("abcde\r");

            Assert.Equal("***\r\n", Echo());
            Assert.Equal("abc", Line());
        }

        [Fact]
        public void ACrWhileCyclingPreemptsTheCycle()
        {
            _session.Status.Enqueue(EnumUserStatus.CYCLE);

            Type("a\r");

            Assert.Equal(new[] { EnumUserStatus.CR_TERMINATED_STRING_AVAILABLE }, Statuses());
        }

        [Fact]
        public void ACrWhileCyclingWithEchoOffIsLeftForStsrou()
        {
            _session.GsblInput.EchoEnabled = false;
            _session.Status.Enqueue(EnumUserStatus.CYCLE);

            Type("a\r");

            Assert.Equal(new[] { EnumUserStatus.CYCLE }, Statuses());
            Assert.Equal("a\r", Encoding.ASCII.GetString(_session.InputBuffer.ToArray()));
        }

        // --- line length and word wrap -------------------------------------------------

        [Fact]
        public void TheLineLimitOverflows()
        {
            _session.GsblInput.MaxInputLength = 3;

            Type("abcde\r");

            Assert.Equal(new[] { EnumUserStatus.INPUT_BUFFER_OVERFLOW, EnumUserStatus.CR_TERMINATED_STRING_AVAILABLE }, Statuses());
            Assert.Equal("abc\r\n", Echo());
            Assert.Equal("abc", Line());
        }

        [Fact]
        public void WordWrapMovesTheLastWordToANewLine()
        {
            _session.GsblInput.MaxInputLength = 10;
            _session.GsblInput.WordWrap = true;

            Type("hello world\r");

            Assert.Equal("hello worl\b\b\b\b    \r\nworld\r\n", Echo());
            Assert.Equal("hello", Line());
            Assert.Equal(new[] { EnumUserStatus.CR_TERMINATED_STRING_AVAILABLE }, Statuses());
            Assert.Equal("world", Line());
        }

        [Fact]
        public void TheWrappedLineIsReportedOnceTheFirstIsConsumed()
        {
            _session.GsblInput.MaxInputLength = 10;
            _session.GsblInput.WordWrap = true;

            Type("hello world\r");
            _session.Status.Dequeue();

            Assert.Equal("hello", Line());
            Assert.Equal(new[] { EnumUserStatus.CR_TERMINATED_STRING_AVAILABLE }, Statuses());
            Assert.Equal("world", Line());
        }

        [Fact]
        public void TypingContinuesAfterAWrap()
        {
            _session.GsblInput.MaxInputLength = 10;
            _session.GsblInput.WordWrap = true;

            Type("hello world");
            Assert.Equal("hello", Line());
            Type("s\r");

            Assert.Equal("worlds", Line());
        }

        [Fact]
        public void WordWrapWithEchoOffEchoesNothing()
        {
            _session.GsblInput.MaxInputLength = 10;
            _session.GsblInput.WordWrap = true;
            _session.GsblInput.EchoEnabled = false;

            Type("hello world\r");

            Assert.Equal("", Echo());
        }

        [Fact]
        public void ASpaceAtTheLimitEndsTheLine()
        {
            _session.GsblInput.MaxInputLength = 5;
            _session.GsblInput.WordWrap = true;

            Type("hello there\r");

            Assert.Equal("hello", Line());
            Assert.Equal("there", Line());
        }

        [Fact]
        public void AWordTooLongToWrapOverflows()
        {
            _session.GsblInput.MaxInputLength = 5;
            _session.GsblInput.WordWrap = true;
            _session.GsblInput.EchoEnabled = false;

            Type("abcdefg\r");

            Assert.Equal(new[] { EnumUserStatus.INPUT_BUFFER_OVERFLOW, EnumUserStatus.CR_TERMINATED_STRING_AVAILABLE }, Statuses());
            Assert.Equal("\b\b", Echo()); //A backspace per attempt, even with echo off
            Assert.Equal("abcde", Line());
        }

        [Fact]
        public void ALineTypedWhileTwoAreUnconsumedOverflows()
        {
            Type("a\rb\rc");

            Assert.Equal(new[] { EnumUserStatus.CR_TERMINATED_STRING_AVAILABLE, EnumUserStatus.INPUT_BUFFER_OVERFLOW }, Statuses());
            Assert.Equal("a", Line());
            Assert.Equal("b", Line());
            Assert.Equal(0, _session.InputBuffer.Length);

            Type("\r");
            Assert.Equal(new[] { EnumUserStatus.CR_TERMINATED_STRING_AVAILABLE, EnumUserStatus.INPUT_BUFFER_OVERFLOW }, Statuses());
        }

        // --- trigger (block) mode --------------------------------------------------------

        [Fact]
        public void TriggerModeStoresRawBytesAndPostsOneStatusPerBlock()
        {
            _session.GsblInput.Trigger = 4;

            Type("ab\x01\r\b\x1bghij");

            Assert.Equal(new[] { EnumUserStatus.INBLK, EnumUserStatus.INBLK }, Statuses());
            Assert.Equal(2, _session.GsblInput.TriggerCount);
            Assert.Equal("ab\x01\r\b\x1bghij", Encoding.Latin1.GetString(_session.InputBuffer.ToArray()));
            Assert.Equal("", Echo());
        }

        [Fact]
        public void TriggerModeBypassesTheInterceptor()
        {
            _session.GsblInput.Trigger = 1;

            Type("a", _ => throw new InvalidOperationException("btuchi isn't called in trigger mode"));

            Assert.Equal("a", Encoding.ASCII.GetString(_session.InputBuffer.ToArray()));
        }

        // --- chiinp (driver line input) ----------------------------------------------------

        private void Driver(string input)
        {
            foreach (var c in Encoding.Latin1.GetBytes(input))
                GsblInput.ProcessDriverCharacter(_session, c);
        }

        [Fact]
        public void ChiinpBuildsLinesWithoutEchoOrTranslation()
        {
            Driver("a\x01" + "b\r");

            Assert.Equal(new[] { EnumUserStatus.CR_TERMINATED_STRING_AVAILABLE }, Statuses());
            Assert.Equal("", Echo());
            Assert.Equal("a\x01" + "b", Line());
        }

        [Fact]
        public void ChiinpHandlesBackspace()
        {
            Driver("\bab\b\bc\r");

            Assert.Equal("c", Line());
        }

        [Fact]
        public void ChiinpStoresBackspaceWhenHandlingIsOff()
        {
            _session.GsblInput.BackspaceCharacter = 0;

            Driver("a\b\r");

            Assert.Equal("a\b", Line());
        }

        [Fact]
        public void ChiinpAllowsOneCharacterPastTheLimit()
        {
            _session.GsblInput.MaxInputLength = 3;

            Driver("abcdef\r");

            Assert.Equal(new[] { EnumUserStatus.INPUT_BUFFER_OVERFLOW, EnumUserStatus.CR_TERMINATED_STRING_AVAILABLE }, Statuses());
            Assert.Equal("abcd", Line());
        }

        [Fact]
        public void ChiinpWordWrap()
        {
            _session.GsblInput.MaxInputLength = 4;
            _session.GsblInput.WordWrap = true;
            _session.GsblInput.EchoEnabled = false;

            Driver("ab cdef\r");

            Assert.Equal("ab", Line());
            Assert.Equal("cdef", Line());
        }

        [Fact]
        public void ChiinpSpaceAtTheLimitEndsTheLine()
        {
            _session.GsblInput.MaxInputLength = 2;
            _session.GsblInput.WordWrap = true;

            Driver("abc d\r");

            Assert.Equal("abc", Line());
            Assert.Equal("d", Line());
        }

        [Fact]
        public void ChiinpUnwrappableWordOverflows()
        {
            _session.GsblInput.MaxInputLength = 2;
            _session.GsblInput.WordWrap = true;
            _session.GsblInput.EchoEnabled = false;

            Driver("abcd\r");

            Assert.Equal(new[] { EnumUserStatus.INPUT_BUFFER_OVERFLOW, EnumUserStatus.CR_TERMINATED_STRING_AVAILABLE }, Statuses());
        }

        // --- reset -------------------------------------------------------------------------

        [Fact]
        public void ResetRestoresTheGsblDefaults()
        {
            var gsbl = _session.GsblInput;
            gsbl.EchoEnabled = false;
            gsbl.BackspaceCharacter = 0;
            gsbl.LineTerminator = (byte)'#';
            gsbl.LinefeedCharacter = 0;
            gsbl.MaxInputLength = 3;
            gsbl.WordWrap = true;
            gsbl.XoffCharacter = 0;
            gsbl.OutputAbortCharacter = (byte)'!';
            gsbl.Trigger = 4;

            gsbl.Reset();

            Assert.True(gsbl.EchoEnabled);
            Assert.Equal(0x08, gsbl.BackspaceCharacter);
            Assert.Equal(0x0D, gsbl.LineTerminator);
            Assert.Equal(0x0A, gsbl.LinefeedCharacter);
            Assert.Equal(GsblChannelInput.UnlimitedLineLength, gsbl.MaxInputLength);
            Assert.False(gsbl.WordWrap);
            Assert.Equal(0x13, gsbl.XoffCharacter);
            Assert.Equal(0, gsbl.OutputAbortCharacter);
            Assert.Equal(0, gsbl.Trigger);
        }
    }
}
