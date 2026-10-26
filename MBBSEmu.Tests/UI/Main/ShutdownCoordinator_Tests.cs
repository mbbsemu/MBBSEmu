using FluentAssertions;
using MBBSEmu.UI.Main;
using System;
using System.Threading;
using Xunit;

namespace MBBSEmu.Tests.UI.Main
{
    public class ShutdownCoordinator_Tests
    {
        private static readonly DateTime Start = new(2026, 10, 26, 12, 0, 0, DateTimeKind.Utc);

        private DateTime _now = Start;

        private ShutdownCoordinator Create(Action stopServices) => new(stopServices, () => _now);

        private static bool WaitForStopped(ShutdownCoordinator coordinator) =>
            SpinWait.SpinUntil(() => coordinator.Phase == EnumShutdownPhase.Stopped, TimeSpan.FromSeconds(5));

        [Fact]
        public void Begin_RunsStopOnceInBackground()
        {
            using var release = new ManualResetEventSlim();
            var calls = 0;
            var coordinator = Create(() =>
            {
                Interlocked.Increment(ref calls);
                release.Wait();
            });

            coordinator.Phase.Should().Be(EnumShutdownPhase.Running);
            coordinator.Begin(unattended: false).Should().BeTrue();
            coordinator.Phase.Should().Be(EnumShutdownPhase.ShuttingDown);
            coordinator.Begin(unattended: false).Should().BeFalse();

            release.Set();

            WaitForStopped(coordinator).Should().BeTrue();
            calls.Should().Be(1);
            coordinator.StopException.Should().BeNull();
        }

        [Fact]
        public void Begin_StopThrows_RecordsExceptionAndStops()
        {
            var failure = new InvalidOperationException("boom");
            var coordinator = Create(() => throw failure);

            coordinator.Begin(unattended: false);

            WaitForStopped(coordinator).Should().BeTrue();
            coordinator.StopException.Should().BeSameAs(failure);
        }

        [Fact]
        public void DeadlineExpired_UserInitiated_NeverExpires()
        {
            using var release = new ManualResetEventSlim();
            var coordinator = Create(() => release.Wait());

            coordinator.Begin(unattended: false);
            _now = Start.AddHours(1);

            coordinator.DeadlineExpired.Should().BeFalse();
            release.Set();
        }

        [Fact]
        public void DeadlineExpired_Unattended_AfterFifteenSeconds()
        {
            using var release = new ManualResetEventSlim();
            var coordinator = Create(() => release.Wait());

            coordinator.Begin(unattended: true);

            _now = Start.AddSeconds(14.9);
            coordinator.DeadlineExpired.Should().BeFalse();

            _now = Start.AddSeconds(15);
            coordinator.DeadlineExpired.Should().BeTrue();
            release.Set();
        }

        [Fact]
        public void Begin_UnattendedAfterUserInitiated_AddsDeadline()
        {
            using var release = new ManualResetEventSlim();
            var coordinator = Create(() => release.Wait());

            coordinator.Begin(unattended: false);
            coordinator.Begin(unattended: true).Should().BeFalse();

            _now = Start.AddSeconds(15);
            coordinator.DeadlineExpired.Should().BeTrue();
            release.Set();
        }

        [Fact]
        public void DeadlineExpired_FalseOnceStopped()
        {
            var coordinator = Create(() => { });

            coordinator.Begin(unattended: true);
            WaitForStopped(coordinator).Should().BeTrue();

            _now = Start.AddMinutes(1);
            coordinator.DeadlineExpired.Should().BeFalse();
        }
    }
}
