using FluentAssertions;
using MBBSEmu.Session;
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
        public void flushingTest()
        {
            int lines = 0;
            StringBuilder builder = new StringBuilder();

            // build up a string of 128k characters, much larger than our buffer size
            while (builder.Length < (128 * 1024)) {
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
    }
}
