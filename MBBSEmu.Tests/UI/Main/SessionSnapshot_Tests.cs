using FluentAssertions;
using MBBSEmu.UI.Main;
using System;
using System.Collections.Generic;
using Xunit;

namespace MBBSEmu.Tests.UI.Main
{
    public class SessionSnapshot_Tests
    {
        private static readonly IReadOnlyList<ChannelSession> Previous = new[] { new ChannelSession(0, "PREVIOUS") };

        /// <summary>
        ///     The host worker adds and removes sessions while the UI reads them; reading a plain Dictionary at the
        ///     wrong moment throws, and that must not count as a UI failure that shuts the BBS down
        /// </summary>
        [Theory]
        [InlineData(typeof(InvalidOperationException))]
        [InlineData(typeof(ArgumentException))]
        [InlineData(typeof(NullReferenceException))]
        public void Read_RetriesConcurrentModification(Type exceptionType)
        {
            var attempts = 0;

            var sessions = SessionSnapshot.Read(() =>
            {
                if (++attempts == 1)
                    throw (Exception)Activator.CreateInstance(exceptionType)!;

                return new[] { new ChannelSession(1, "SYSOP") };
            }, Previous);

            sessions.Should().Equal(new ChannelSession(1, "SYSOP"));
        }

        [Fact]
        public void Read_KeepsPreviousSnapshotWhenEveryAttemptFails()
        {
            SessionSnapshot.Read(() => throw new InvalidOperationException(), Previous).Should().BeSameAs(Previous);
        }

        [Fact]
        public void Read_DropsNullEntries()
        {
            SessionSnapshot.Read(() => new[] { new ChannelSession(0, "A"), null }, Previous)
                .Should().Equal(new ChannelSession(0, "A"));
        }

        [Fact]
        public void Read_OtherExceptionsPropagate()
        {
            var act = () => SessionSnapshot.Read(() => throw new FormatException(), Previous);

            act.Should().Throw<FormatException>();
        }
    }
}
