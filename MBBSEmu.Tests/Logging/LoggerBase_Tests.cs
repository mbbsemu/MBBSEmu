using FluentAssertions;
using MBBSEmu.Logging;
using MBBSEmu.Logging.Targets;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace MBBSEmu.Tests.Logging
{
    public class LoggerBase_Tests
    {
        /// <summary>
        ///     Target that only counts writes, so tests can log heavily without console output
        /// </summary>
        private class CountingTarget : ILoggingTarget
        {
            public int Count;

            public void Write(params object[] logEntry) => Interlocked.Increment(ref Count);
        }

        [Fact]
        public void RemoveTargetOfType_ReturnsTrueOnlyWhenPresent()
        {
            var logger = new MessageLogger(new ConsoleTarget());

            logger.RemoveTarget<ConsoleTarget>().Should().BeTrue();
            logger.RemoveTarget<ConsoleTarget>().Should().BeFalse();
        }

        [Fact]
        public void RemoveTargetInstance_RemovesOnlyThatInstance()
        {
            var first = new QueueTarget();
            var second = new QueueTarget();
            var logger = new MessageLogger(first);
            logger.AddTarget(second);

            logger.RemoveTarget(first).Should().BeTrue();

            logger.HasTarget<QueueTarget>().Should().BeTrue();
            logger.RemoveTarget(first).Should().BeFalse();
        }

        [Fact]
        public void RemoveTargetInstance_StopsWritesToThatInstanceOnly()
        {
            var removed = new CountingTarget();
            var kept = new CountingTarget();
            var logger = new MessageLogger(removed);
            logger.AddTarget(kept);

            logger.RemoveTarget(removed);
            logger.Info("hello");

            removed.Count.Should().Be(0);
            kept.Count.Should().Be(1);
        }

        [Fact]
        public void HasTarget_ReflectsAddAndRemove()
        {
            var logger = new MessageLogger(new CountingTarget());

            logger.HasTarget<QueueTarget>().Should().BeFalse();

            logger.AddTarget(new QueueTarget());
            logger.HasTarget<QueueTarget>().Should().BeTrue();

            logger.RemoveTarget<QueueTarget>();
            logger.HasTarget<QueueTarget>().Should().BeFalse();
        }

        [Fact]
        public void ConcurrentAddRemoveWhileLogging_DoesNotThrow()
        {
            var logger = new MessageLogger(new CountingTarget());

            var act = () => Parallel.Invoke(
                () =>
                {
                    for (var i = 0; i < 20_000; i++)
                        logger.Info("x");
                },
                () =>
                {
                    for (var i = 0; i < 2_000; i++)
                    {
                        var target = new CountingTarget();
                        logger.AddTarget(target);
                        logger.RemoveTarget(target);
                    }
                });

            act.Should().NotThrow();
        }
    }
}
