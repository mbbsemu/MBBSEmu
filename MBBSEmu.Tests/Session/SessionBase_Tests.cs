using FluentAssertions;
using MBBSEmu.Session;
using System;
using Xunit;

namespace MBBSEmu.Tests.Session
{
    public class SessionBase_Tests
    {
        private const string RED = "\x1B[31;48;4m";

        private readonly TestSession testSession = new TestSession(null, null);

        [Fact]
        public void normalBreaks()
        {
            testSession.SendToClient("Testing one two three four five six seven eight nine ten eleven twelve thirteen fourteen fifteen sixteen\r\n");
            testSession.GetLine(TimeSpan.FromMilliseconds(100)).Should().Be("Testing one two three four five six seven eight nine ten eleven twelve thirteen");
            testSession.GetLine(TimeSpan.FromMilliseconds(100)).Should().Be("fourteen fifteen sixteen");
        }

        [Fact]
        public void normalBreaksWithAnsi()
        {
            testSession.SendToClient($"{RED}Testing {RED}one{RED} {RED}two three four five six seven eight nine ten eleven twelve{RED} {RED}thirteen{RED} {RED}fourteen fifteen sixteen\r\n");
            testSession.GetLine(TimeSpan.FromMilliseconds(100)).Should().Be($"{RED}Testing {RED}one{RED} {RED}two three four five six seven eight nine ten eleven twelve{RED} {RED}thirteen{RED}");
            testSession.GetLine(TimeSpan.FromMilliseconds(100)).Should().Be($"{RED}fourteen fifteen sixteen");
        }

        [Fact]
        public void longBreakWithWhitespaceAfterwards()
        {
            testSession.SendToClient("01234567890123456789012345678901234567890123456789012345678901234567890123456789 testing\r\n");
            testSession.GetLine(TimeSpan.FromMilliseconds(100)).Should().Be("01234567890123456789012345678901234567890123456789012345678901234567890123456789");
            testSession.GetLine(TimeSpan.FromMilliseconds(100)).Should().Be("testing");
        }

        [Fact]
        public void longBreakWithNoWhitespaceAfterwards()
        {
            testSession.SendToClient("012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789 testing\r\n");
            testSession.GetLine(TimeSpan.FromMilliseconds(100)).Should().Be("01234567890123456789012345678901234567890123456789012345678901234567890123456789");
            testSession.GetLine(TimeSpan.FromMilliseconds(100)).Should().Be("0123456789 testing");
        }

        [Fact]
        public void longBreakWithNoWhitespaceAfterwardsMultiline()
        {
            testSession.SendToClient("012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789 testing champion agreement platitude advancement antidisestablishmentarianism\r\n");
            testSession.GetLine(TimeSpan.FromMilliseconds(100)).Should().Be("01234567890123456789012345678901234567890123456789012345678901234567890123456789");
            testSession.GetLine(TimeSpan.FromMilliseconds(100)).Should().Be("0123456789 testing champion agreement platitude advancement");
            testSession.GetLine(TimeSpan.FromMilliseconds(100)).Should().Be("antidisestablishmentarianism");
        }

        [Fact]
        public void manyLongBreaks()
        {
            testSession.SendToClient(
                "01234567890123456789012345678901234567890123456789012345678901234567890123456789 " +
                "01234567890123456789012345678901234567890123456789012345678901234567890123456789 " +
                "01234567890123456789012345678901234567890123456789012345678901234567890123456789 testing\n");

            testSession.GetLine(TimeSpan.FromMilliseconds(100)).Should().Be("01234567890123456789012345678901234567890123456789012345678901234567890123456789");
            testSession.GetLine(TimeSpan.FromMilliseconds(100)).Should().Be("01234567890123456789012345678901234567890123456789012345678901234567890123456789");
            testSession.GetLine(TimeSpan.FromMilliseconds(100)).Should().Be("01234567890123456789012345678901234567890123456789012345678901234567890123456789");
            testSession.GetLine(TimeSpan.FromMilliseconds(100)).Should().Be("testing");
        }
    }
}
