using FluentAssertions;
using MBBSEmu.HostProcess.Enums;
using MBBSEmu.Session;
using MBBSEmu.Session.Enums;
using System;
using System.Text;
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
            testSession.WordWrapWidth = 80;
            testSession.SendToClient("012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789 testing\r\n");
            testSession.GetLine(TimeSpan.FromMilliseconds(100)).Should().Be("01234567890123456789012345678901234567890123456789012345678901234567890123456789");
            testSession.GetLine(TimeSpan.FromMilliseconds(100)).Should().Be("0123456789 testing");
        }

        [Fact]
        public void longBreakWithNoWhitespaceAfterwardsMultiline()
        {
            testSession.WordWrapWidth = 80;
            testSession.SendToClient("012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789 testing champion agreement platitude advancement antidisestablishmentarianism\r\n");
            testSession.GetLine(TimeSpan.FromMilliseconds(100)).Should().Be("01234567890123456789012345678901234567890123456789012345678901234567890123456789");
            testSession.GetLine(TimeSpan.FromMilliseconds(100)).Should().Be("0123456789 testing champion agreement platitude advancement");
            testSession.GetLine(TimeSpan.FromMilliseconds(100)).Should().Be("antidisestablishmentarianism");
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
        [InlineData(4, "test one two three\r\n", "test", "one", "two", "thre", "e")]
        [InlineData(0, "test one two three\r\n", "test one two three")]
        [InlineData(4, "test\r\none two three\r\n", "test", "one", "two", "thre", "e")]
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
            // modules often print one line with several outprf() calls; the last space is in an earlier send
            testSession.WordWrapWidth = 80;
            testSession.SendToClient(new string('x', 40) + " " + new string('y', 30));
            testSession.SendToClient(new string('z', 20) + "\r\n");
            testSession.GetLine(TimeSpan.FromMilliseconds(100)).Should().Be(new string('x', 40) + " " + new string('y', 30) + new string('z', 9));
            testSession.GetLine(TimeSpan.FromMilliseconds(100)).Should().Be(new string('z', 11));
        }

        [Fact]
        public void lineSentInPiecesDoesNotBreakInsideLaterPiece()
        {
            // the space was already sent, so the break can only go before the character that overflows
            testSession.WordWrapWidth = 80;
            testSession.SendToClient("aaaa bbbb");
            testSession.SendToClient(new string('c', 80) + "\r\n");
            testSession.GetLine(TimeSpan.FromMilliseconds(100)).Should().Be("aaaa bbbb" + new string('c', 71));
            testSession.GetLine(TimeSpan.FromMilliseconds(100)).Should().Be(new string('c', 9));
        }

        [Fact]
        public void cursorMovedRightDoesNotBreakAtSpaceFromEarlierLine()
        {
            // ESC[70C skips 70 columns without sending them; the spaces of the previous line must not be used
            testSession.WordWrapWidth = 80;
            testSession.SendToClient("one two three four\r\n");
            testSession.GetLine(TimeSpan.FromMilliseconds(100)).Should().Be("one two three four");
            testSession.SendToClient("\x1B[70C" + "abcdefghijklmno\r\n");
            testSession.GetLine(TimeSpan.FromMilliseconds(100)).Should().Be("\x1B[70C" + "abcdefghij");
            testSession.GetLine(TimeSpan.FromMilliseconds(100)).Should().Be("klmno");
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
            testSession.SendToClient(new string('a', 70) + string.Concat(System.Linq.Enumerable.Repeat("\x1B[Cb", 6)) + "\r\n");
            testSession.GetLine(TimeSpan.FromMilliseconds(100)).Should().Be(new string('a', 70) + string.Concat(System.Linq.Enumerable.Repeat("\x1B[Cb", 5)) + "\x1B[C");
            testSession.GetLine(TimeSpan.FromMilliseconds(100)).Should().Be("b");
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
