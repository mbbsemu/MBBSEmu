using FluentAssertions;
using MBBSEmu.HostProcess.Enums;
using MBBSEmu.Session;
using MBBSEmu.Session.Enums;
using System;
using System.Text;
using System.Text.RegularExpressions;
using Xunit;

namespace MBBSEmu.Tests.Session
{
    public class SessionBase_Tests
    {
        private const string RED = "\x1B[31;48;4m";
        private const string RESET_TERM = "\x1B[2J";

        private readonly TestSession testSession = new TestSession(null, null);

        [Fact]
        public void normalBreaks()
        {
            testSession.WordWrapWidth = 80;
            testSession.SendToClient("Testing one two three four five six seven eight nine ten eleven twelve thirteen fourteen fifteen sixteen\r\n");
            testSession.GetLine(TimeSpan.FromMilliseconds(100)).Should().Be("Testing one two three four five six seven eight nine ten eleven twelve thirteen");
            testSession.GetLine(TimeSpan.FromMilliseconds(100)).Should().Be("fourteen fifteen sixteen");
        }

        [Fact]
        public void normalBreaksWithAnsi()
        {
            testSession.WordWrapWidth = 80;
            testSession.SendToClient($"{RESET_TERM}{RED}Testing {RED}one{RED} {RED}two three four five six seven eight nine ten eleven twelve{RED} {RED}thirteen{RED} {RED}fourteen fifteen sixteen\r\n");
            testSession.GetLine(TimeSpan.FromMilliseconds(100)).Should().Be($"{RESET_TERM}{RED}Testing {RED}one{RED} {RED}two three four five six seven eight nine ten eleven twelve{RED} {RED}thirteen{RED}");
            testSession.GetLine(TimeSpan.FromMilliseconds(100)).Should().Be($"{RED}fourteen fifteen sixteen");
        }

        [Fact]
        public void longBreakWithWhitespaceAfterwards()
        {
            testSession.WordWrapWidth = 80;
            testSession.SendToClient("01234567890123456789012345678901234567890123456789012345678901234567890123456789 testing\r\n");
            testSession.GetLine(TimeSpan.FromMilliseconds(100)).Should().Be("01234567890123456789012345678901234567890123456789012345678901234567890123456789");
            testSession.GetLine(TimeSpan.FromMilliseconds(100)).Should().Be("testing");
        }

        [Fact]
        public void longBreakWithNoWhitespaceAfterwards()
        {
            // GSBL never cuts a word: the line runs on to the next space
            testSession.WordWrapWidth = 80;
            testSession.SendToClient("012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789 testing\r\n");
            testSession.GetLine(TimeSpan.FromMilliseconds(100)).Should().Be("012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789");
            testSession.GetLine(TimeSpan.FromMilliseconds(100)).Should().Be("testing");
        }

        [Fact]
        public void longBreakWithNoWhitespaceAfterwardsMultiline()
        {
            testSession.WordWrapWidth = 80;
            testSession.SendToClient("012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789 testing champion agreement platitude advancement antidisestablishmentarianism\r\n");
            testSession.GetLine(TimeSpan.FromMilliseconds(100)).Should().Be("012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789");
            testSession.GetLine(TimeSpan.FromMilliseconds(100)).Should().Be("testing champion agreement platitude advancement antidisestablishmentarianism");
        }

        [Fact]
        public void manyLongBreaks()
        {
            testSession.WordWrapWidth = 80;
            testSession.SendToClient(
                "01234567890123456789012345678901234567890123456789012345678901234567890123456789 " +
                "01234567890123456789012345678901234567890123456789012345678901234567890123456789 " +
                "01234567890123456789012345678901234567890123456789012345678901234567890123456789 testing\n");

            testSession.GetLine(TimeSpan.FromMilliseconds(100)).Should().Be("01234567890123456789012345678901234567890123456789012345678901234567890123456789");
            testSession.GetLine(TimeSpan.FromMilliseconds(100)).Should().Be("01234567890123456789012345678901234567890123456789012345678901234567890123456789");
            testSession.GetLine(TimeSpan.FromMilliseconds(100)).Should().Be("01234567890123456789012345678901234567890123456789012345678901234567890123456789");
            testSession.GetLine(TimeSpan.FromMilliseconds(100)).Should().Be("testing");
        }

        [Fact]
        public void lineFillingScreenWidthBreaksEarly()
        {
            // MajorMUD sets btutsw(79) for an 80 column terminal; a line must never fill all 80 columns or
            // terminals that wrap immediately at the last column will show an extra blank line (#555)
            testSession.WordWrapWidth = 79;
            testSession.SendToClient("You are carrying padded pants (Legs), padded gloves (Hands), club (Weapon Hand), padded boots, padded helm, padded vest\r\n");
            testSession.GetLine(TimeSpan.FromMilliseconds(100)).Should().Be("You are carrying padded pants (Legs), padded gloves (Hands), club (Weapon");
            testSession.GetLine(TimeSpan.FromMilliseconds(100)).Should().Be("Hand), padded boots, padded helm, padded vest");
        }

        [Theory]
        [InlineData(4, "test one two three\r\n", "test", "one", "two", "three")]
        [InlineData(0, "test one two three\r\n", "test one two three")]
        [InlineData(4, "test\r\none two three\r\n", "test", "one", "two", "three")]
        public void edgeCasesTest(int wordWrapWidth, params string[] strings)
        {
            testSession.WordWrapWidth = wordWrapWidth;

            testSession.SendToClient(strings[0]);

            for (int i = 1; i < strings.Length; ++i)
            {
                testSession.GetLine(TimeSpan.FromMilliseconds(100)).Should().Be(strings[i]);
            }
        }

        [Fact]
        public void lineSentInPiecesBreaksInLaterPiece()
        {
            // modules often print one line with several outprf() calls; the last space is in an
            // earlier send, so it cannot become the break and the word is not cut either
            testSession.WordWrapWidth = 80;
            testSession.SendToClient(new string('x', 40) + " " + new string('y', 30));
            testSession.SendToClient(new string('z', 20) + " end\r\n");
            testSession.GetLine(TimeSpan.FromMilliseconds(100)).Should().Be(new string('x', 40) + " " + new string('y', 30) + new string('z', 20));
            testSession.GetLine(TimeSpan.FromMilliseconds(100)).Should().Be("end");
        }

        [Fact]
        public void lineSentInPiecesDoesNotBreakInsideLaterPiece()
        {
            // the space was already sent and the later piece has none, so the line runs on
            testSession.WordWrapWidth = 80;
            testSession.SendToClient("aaaa bbbb");
            testSession.SendToClient(new string('c', 80) + "\r\n");
            testSession.GetLine(TimeSpan.FromMilliseconds(100)).Should().Be("aaaa bbbb" + new string('c', 80));
        }

        [Fact]
        public void cursorMovedRightDoesNotBreakAtSpaceFromEarlierLine()
        {
            // ESC[70C skips 70 columns without sending them; the spaces of the previous line must not be used
            testSession.WordWrapWidth = 80;
            testSession.SendToClient("one two three four\r\n");
            testSession.GetLine(TimeSpan.FromMilliseconds(100)).Should().Be("one two three four");
            testSession.SendToClient("\x1B[70C" + "abcdefghijklmno\r\n");
            testSession.GetLine(TimeSpan.FromMilliseconds(100)).Should().Be("\x1B[70C" + "abcdefghijklmno");
        }

        [Fact]
        public void cursorPositionWithOnlyRowMovesToFirstColumn()
        {
            // Trade Wars 2002 starts each line of its framed text with ESC[<row>H
            testSession.WordWrapWidth = 80;
            testSession.SendToClient(new string('x', 60) + "\x1B[5H" + new string('y', 40) + "\r\n");
            testSession.GetLine(TimeSpan.FromMilliseconds(100)).Should().Be(new string('x', 60) + "\x1B[5H" + new string('y', 40));
        }

        [Fact]
        public void cursorPositionWithEmptyRowUsesColumn()
        {
            testSession.WordWrapWidth = 80;
            testSession.SendToClient(new string('x', 60) + "\x1B[;42H" + new string('y', 30) + " " + new string('z', 14) + "\r\n");
            testSession.GetLine(TimeSpan.FromMilliseconds(100)).Should().Be(new string('x', 60) + "\x1B[;42H" + new string('y', 30));
            testSession.GetLine(TimeSpan.FromMilliseconds(100)).Should().Be(new string('z', 14));
        }

        [Fact]
        public void cursorRightWithoutCountMovesOneColumn()
        {
            // Trade Wars 2002 sends the spaces between words as ESC[C
            testSession.WordWrapWidth = 80;
            // 70 + 3 * 2 columns, then " ccc" reaches column 80; without the ESC[C columns it would not
            testSession.SendToClient(new string('a', 70) + string.Concat(System.Linq.Enumerable.Repeat("\x1B[Cb", 3)) + " ccc\r\n");
            testSession.GetLine(TimeSpan.FromMilliseconds(100)).Should().Be(new string('a', 70) + string.Concat(System.Linq.Enumerable.Repeat("\x1B[Cb", 3)));
            testSession.GetLine(TimeSpan.FromMilliseconds(100)).Should().Be("ccc");
        }

        [Fact]
        public void restoredCursorKeepsItsColumn()
        {
            // ESC[s CR LF ESC[u is how Trade Wars 2002 keeps lines short without moving the cursor
            testSession.WordWrapWidth = 80;
            testSession.SendToClient(new string('a', 60) + "\x1B[s\r\n\x1B[u" + "1234567890 1234567890 12\r\n");
            testSession.GetLine(TimeSpan.FromMilliseconds(100)).Should().Be(new string('a', 60) + "\x1B[s");
            testSession.GetLine(TimeSpan.FromMilliseconds(100)).Should().Be("\x1B[u" + "1234567890");
            testSession.GetLine(TimeSpan.FromMilliseconds(100)).Should().Be("1234567890 12");
        }

        [Fact]
        public void letterRightAfterBracketEndsSequence()
        {
            // ESC[H must not swallow the H of "Hello", or the CR after ESC[s
            testSession.WordWrapWidth = 10;
            testSession.SendToClient("\x1B[HHello there\r\n");
            testSession.GetLine(TimeSpan.FromMilliseconds(100)).Should().Be("\x1B[HHello");
            testSession.GetLine(TimeSpan.FromMilliseconds(100)).Should().Be("there");
        }

        [Theory]
        [InlineData("\x1B[1;200H")]
        [InlineData("\x1B[1;0H")]
        [InlineData("\x1B[0G")]
        [InlineData("\x1B[600C")]
        public void cursorPositionOutsideLineIsClamped(string ansi)
        {
            testSession.WordWrapWidth = 80;
            testSession.SendToClient(ansi + "abc def\r\n");
            testSession.GetLine(TimeSpan.FromMilliseconds(100)).Should().NotBeNull();
        }

        /// <summary>
        ///     How MajorBBS 6.25's GALGSBL.DLL wrapped each text given to btuxmt() after
        ///     btutsw(width): where it broke the lines, byte for byte. (It also follows each CR
        ///     of the text with a LF, its line-feed setting, which is left out here.)
        ///     {c*N} is the character c N times.
        /// </summary>
        [Theory]
        [InlineData(79, "{a*70} bcdefgh next words\r", "{a*70} bcdefgh\r\nnext words\r")]
        [InlineData(79, "{x*72} yyyyyy z\r", "{x*72}\r\nyyyyyy z\r")]
        [InlineData(79, "{x*72} yyyyyy\r", "{x*72}\r\nyyyyyy\r")]
        [InlineData(79, "{x*72} yyyyyy", "{x*72}\r\nyyyyyy")]
        [InlineData(79, "{x*72} yyyyyy tail\r", "{x*72}\r\nyyyyyy tail\r")]
        [InlineData(79, "{q*85} r\r", "{q*85}\r\nr\r")]
        [InlineData(79, "{y*60} {z*30}\r", "{y*60}\r\n{z*30}\r")]
        [InlineData(79, "ab {c*76}\u0007 d\r", "ab\r\n{c*76}\u0007 d\r")]
        [InlineData(79, "ab {c*76}\u001b[1;31m d\u001b[0m\r", "ab\r\n{c*76}\u001b[1;31m d\u001b[0m\r")]
        [InlineData(79, "You are carrying padded pants (Legs), padded gloves (Hands), club (Weapon Hand), padded boots, padded helm, padded vest\r",
            "You are carrying padded pants (Legs), padded gloves (Hands), club (Weapon\r\nHand), padded boots, padded helm, padded vest\r")]
        [InlineData(10, "abcd efghi", "abcd\r\nefghi")]
        [InlineData(10, "jk lm\r", "jk lm\r")]
        [InlineData(10, "abcde fghij\r", "abcde\r\nfghij\r")]
        [InlineData(10, "abcde fghijk\r", "abcde\r\nfghijk\r")]
        [InlineData(10, "abcde fghij klm nop\r", "abcde\r\nfghij klm\r\nnop\r")]
        [InlineData(10, "a bcdefghijklmnopq rs\r", "a\r\nbcdefghijklmnopq\r\nrs\r")]
        [InlineData(10, "abc def gh i\r", "abc def\r\ngh i\r")]
        [InlineData(4, "test one two three\r", "test\r\none\r\ntwo\r\nthree\r")]
        public void wrapsAsGsbl(int width, string text, string sent)
        {
            static string expand(string s) =>
                Regex.Replace(s, @"\{(.)\*(\d+)\}", m => new string(m.Groups[1].Value[0], int.Parse(m.Groups[2].Value)));

            var session = new TestSession(null, null) { WordWrapWidth = width };
            session.SendToClient(expand(text));
            Encoding.ASCII.GetString(session.DrainSentData()).Should().Be(expand(sent));
        }

        /// <summary>
        ///     GSBL breaks a line only at a space: TAB and the CP437 letters 0x85 and 0xA0 are one
        ///     column like any other byte and are never replaced. The review's vectors for #769;
        ///     MajorBBS 6.25's GALGSBL.DLL sends each one unchanged after btutsw(6).
        /// </summary>
        [Theory]
        [InlineData(new byte[] { 0x61, 0x62, 0xA0, 0x63, 0x64, 0x65, 0x66 })]
        [InlineData(new byte[] { 0x61, 0x62, 0x85, 0x63, 0x64, 0x65, 0x66 })]
        [InlineData(new byte[] { 0x61, 0x62, 0x09, 0x63, 0x64, 0x65, 0x66 })]
        public void wrapsOnlyAtSpaceAsGsbl(byte[] text)
        {
            var session = new TestSession(null, null) { WordWrapWidth = 6 };
            session.SendToClient(text);
            session.DrainSentData().Should().Equal(text);
        }

        [Fact]
        public void flushingTest()
        {
            int lines = 0;
            StringBuilder builder = new StringBuilder();

            // build up a string of 128k characters, much larger than our buffer size
            while (builder.Length < (128 * 1024))
            {
                builder.Append("This is a long line of text that we repeat\r\n");
                ++lines;
            }

            testSession.WordWrapWidth = 80;
            testSession.SendToClient(builder.ToString()); // send all at once

            while (lines-- > 0)
            {
                testSession.GetLine(TimeSpan.FromMilliseconds(100)).Should().Be("This is a long line of text that we repeat");
            }

            // should be no lines left to read
            Assert.Throws<TimeoutException>(() => testSession.GetLine(TimeSpan.FromMilliseconds(100)));

        }

        [Fact]
        public void ResetForLogin_ClearsPriorSessionState()
        {
            var session = new TestSession(null, null)
            {
                Password = "password",
                Email = "sysop@example.com",
                DataToProcess = true,
                InputLockout = true,
                EchoSecureEnabled = true,
                BinaryOutputMode = true
            };

            session.Status.Enqueue(EnumUserStatus.CR_TERMINATED_STRING_AVAILABLE);
            session.DataFromClient.Add((byte)'x');
            session.DataToClient.Add(Encoding.ASCII.GetBytes("pending"));
            session.InputBuffer.WriteByte((byte)'x');
            session.EchoBuffer.WriteByte((byte)'x');

            session.ResetForLogin();

            Assert.Equal(EnumSessionState.Unauthenticated, session.SessionState);
            Assert.Empty(session.Username);
            Assert.Empty(session.Password);
            Assert.Empty(session.Email);
            Assert.Empty(session.Status);
            Assert.Empty(session.DataFromClient);
            Assert.Empty(session.DataToClient);
            Assert.Equal(0, session.InputBuffer.Length);
            Assert.Equal(0, session.EchoBuffer.Length);
            Assert.False(session.DataToProcess);
            Assert.False(session.InputLockout);
            Assert.False(session.EchoSecureEnabled);
            Assert.False(session.BinaryOutputMode);
        }
    }
}
