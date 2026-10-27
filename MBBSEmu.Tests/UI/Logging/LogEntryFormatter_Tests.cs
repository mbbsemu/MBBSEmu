using FluentAssertions;
using MBBSEmu.UI.Logging;
using Microsoft.Extensions.Logging;
using System;
using Xunit;

namespace MBBSEmu.Tests.UI.Logging
{
    public class LogEntryFormatter_Tests
    {
        private static readonly DateTime SampleTime = new(2026, 10, 26, 21, 7, 9, 204);

        [Theory]
        [InlineData(LogLevel.Trace, "TRACE")]
        [InlineData(LogLevel.Debug, "DEBUG")]
        [InlineData(LogLevel.Information, "INFO")]
        [InlineData(LogLevel.Warning, "WARN")]
        [InlineData(LogLevel.Error, "ERROR")]
        [InlineData(LogLevel.Critical, "CRIT")]
        [InlineData(LogLevel.None, "NONE")]
        public void LevelLabel_MatchesLegacyLabels(LogLevel level, string expected)
        {
            LogEntryFormatter.LevelLabel(level).Should().Be(expected);
        }

        [Theory]
        [InlineData("MBBSEmu.HostProcess.MbbsHost", "MbbsHost")]
        [InlineData("MBBSEmu.Program", "Program")]
        [InlineData("MBBSEmu.Foo+Nested", "Nested")]
        [InlineData("MBBSEmu.Util.LRUCache`2[System.String,System.Int32]", "LRUCache")]
        [InlineData("", "")]
        [InlineData(null, "")]
        public void ShortSource_ReturnsLastSegment(string fullTypeName, string expected)
        {
            LogEntryFormatter.ShortSource(fullTypeName).Should().Be(expected);
        }

        [Fact]
        public void SingleLine_ReplacesLineBreaksTabsAndControlCharacters()
        {
            LogEntryFormatter.SingleLine("a\r\nb\tc\u001b[31md").Should().Be("a b c?[31md");
        }

        [Fact]
        public void SingleLine_LoneCarriageReturnAndNewlineBecomeSpaces()
        {
            LogEntryFormatter.SingleLine("a\rb\nc").Should().Be("a b c");
        }

        [Fact]
        public void FormatTime_WithAndWithoutMilliseconds()
        {
            LogEntryFormatter.FormatTime(SampleTime, withMilliseconds: false).Should().Be("21:07:09");
            LogEntryFormatter.FormatTime(SampleTime, withMilliseconds: true).Should().Be("21:07:09.204");
        }

        [Fact]
        public void ApplicationDetail_ContainsFullFields()
        {
            var entry = new ApplicationLogEntry(SampleTime, "MBBSEmu.Btrieve.BtrieveFileProcessor", LogLevel.Error, "line1\nline2");

            LogEntryFormatter.ApplicationDetail(entry).Should().Be(
                "Time: 2026-10-26 21:07:09.204\nClass: MBBSEmu.Btrieve.BtrieveFileProcessor\nLevel: ERROR\nMessage: line1\nline2");
        }

        [Fact]
        public void AuditDetail_ContainsSummaryAndDetail()
        {
            LogEntryFormatter.AuditDetail(new AuditLogEntry("LOGON", "SYSOP logged on"))
                .Should().Be("Summary: LOGON\nDetail: SYSOP logged on");
        }

        [Fact]
        public void ToApplicationEntry_ReadsQueueTargetLayout()
        {
            var entry = LogEntryFormatter.ToApplicationEntry(
                new object[] { SampleTime, typeof(MBBSEmu.HostProcess.MbbsHost), LogLevel.Warning, "msg" });

            entry.Should().Be(new ApplicationLogEntry(SampleTime, "MBBSEmu.HostProcess.MbbsHost", LogLevel.Warning, "msg"));
        }

        [Fact]
        public void ToApplicationEntry_NullTypeAndUnknownLevel()
        {
            var entry = LogEntryFormatter.ToApplicationEntry(new object[] { SampleTime, null, "not a level", "msg" });

            entry.SourceType.Should().Be("");
            entry.Level.Should().Be(LogLevel.None);
        }

        [Fact]
        public void ToAuditEntry_ReadsQueueTargetLayout()
        {
            LogEntryFormatter.ToAuditEntry(new object[] { SampleTime, null, "LOGON", "SYSOP" })
                .Should().Be(new AuditLogEntry("LOGON", "SYSOP"));
        }
    }
}
