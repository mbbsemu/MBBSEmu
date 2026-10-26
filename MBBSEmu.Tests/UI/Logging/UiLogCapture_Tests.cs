using FluentAssertions;
using MBBSEmu.Logging;
using MBBSEmu.Logging.Targets;
using MBBSEmu.UI.Logging;
using Microsoft.Extensions.Logging;
using Xunit;

namespace MBBSEmu.Tests.UI.Logging
{
    public class UiLogCapture_Tests
    {
        [Fact]
        public void Constructor_RemovesConsoleTargetAndCaptures()
        {
            var messageLogger = new MessageLogger(new ConsoleTarget());
            var auditLogger = new AuditLogger(new ConsoleTarget());

            using var capture = new UiLogCapture(messageLogger, auditLogger);

            messageLogger.HasTarget<ConsoleTarget>().Should().BeFalse();
            auditLogger.HasTarget<ConsoleTarget>().Should().BeFalse();

            messageLogger.Info("hello");

            var entries = capture.DrainApplicationLog();
            entries.Should().ContainSingle();
            entries[0].Message.Should().Be("hello");
            entries[0].Level.Should().Be(LogLevel.Information);
            capture.DrainApplicationLog().Should().BeEmpty();
        }

        [Fact]
        public void AuditLog_IsCaptured()
        {
            var messageLogger = new MessageLogger(new ConsoleTarget());
            var auditLogger = new AuditLogger(new ConsoleTarget());
            using var capture = new UiLogCapture(messageLogger, auditLogger);

            auditLogger.Log("LOGON", "SYSOP");

            capture.DrainAuditLog().Should().Equal(new AuditLogEntry("LOGON", "SYSOP"));
        }

        [Fact]
        public void Dispose_RestoresConsoleTargetOnlyWhereItExisted()
        {
            var messageLogger = new MessageLogger(new ConsoleTarget());
            var auditLogger = new AuditLogger();

            new UiLogCapture(messageLogger, auditLogger).Dispose();

            messageLogger.HasTarget<ConsoleTarget>().Should().BeTrue();
            auditLogger.HasTarget<ConsoleTarget>().Should().BeFalse();
            messageLogger.HasTarget<QueueTarget>().Should().BeFalse();
            auditLogger.HasTarget<QueueTarget>().Should().BeFalse();
        }

        [Fact]
        public void Dispose_LeavesOtherQueueTargetsInPlace()
        {
            var otherQueue = new QueueTarget();
            var messageLogger = new MessageLogger(otherQueue);
            var auditLogger = new AuditLogger();

            new UiLogCapture(messageLogger, auditLogger).Dispose();
            messageLogger.Info("after");

            otherQueue.DequeueAll().Should().ContainSingle();
        }
    }
}
