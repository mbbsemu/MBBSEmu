using FluentAssertions;
using MBBSEmu.UI.Logging;
using MBBSEmu.UI.Main;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Terminal.Gui.Input;
using Xunit;

namespace MBBSEmu.Tests.UI.Main
{
    [Collection("TerminalGui")]
    public class AdminConsoleView_Tests : IDisposable
    {
        private sealed class FakeDialogs : IAdminConsoleDialogs
        {
            public readonly List<(string Title, string Text)> Entries = new();
            public int AboutCalls;

            public bool ConfirmShutdown() => true;
            public bool ConfirmForceQuit() => true;
            public void ShowEntry(string title, string text) => Entries.Add((title, text));
            public void ShowAbout() => AboutCalls++;
        }

        private static ApplicationLogEntry Entry(string time, string source, LogLevel level, string message) =>
            new(DateTime.ParseExact("2026-10-26 " + time, "yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture), source, level, message);

        private static readonly ApplicationLogEntry[] SampleApplicationLog =
        {
            Entry("21:04:11.902", "MBBSEmu.Program", LogLevel.Information, "Telnet listening on IP 0.0.0.0 port 23"),
            Entry("21:04:12.118", "MBBSEmu.HostProcess.MbbsHost", LogLevel.Information, "Loading GWWARROW"),
            Entry("21:07:03.551", "MBBSEmu.HostProcess.ExportedModules.Majorbbs", LogLevel.Warning, "Unsupported ordinal 1092 called by GWWARROW, returning 0"),
            Entry("21:07:09.204", "MBBSEmu.Btrieve.BtrieveFileProcessor", LogLevel.Error, "Unable to open TWDATA.DAT: file is locked by another process"),
            Entry("21:08:15.620", "MBBSEmu.Server.SocketServer", LogLevel.Information, "Accepted connection from 10.0.0.12:40118")
        };

        private const int ErrorRow = 3;

        private static readonly AuditLogEntry[] SampleAuditLog =
        {
            new("LOGON", "SYSOP logged on from 127.0.0.1"),
            new("NEW USER", "BOBSMITH created a new account")
        };

        private static readonly IReadOnlyList<ChannelLine> SampleChannels = ChannelList.Build(8, new[] { new ChannelSession(0, "SYSOP") });

        private readonly FakeDialogs _dialogs = new();
        private readonly AdminConsoleView _view;
        private TerminalGuiHarness _harness;

        public AdminConsoleView_Tests()
        {
            _view = new AdminConsoleView(_dialogs, StatusLine.RightText(true, false, "2026.10.26.1"));
        }

        public void Dispose() => _harness?.Dispose();

        /// <summary>
        ///     Shows the view at the given size with the sample logs and channels
        /// </summary>
        private string[] Show(int width, int height, IReadOnlyList<ChannelLine> channels = null)
        {
            _harness = new TerminalGuiHarness(width, height);
            _harness.Show(_view);
            _view.ApplyScreenSize(width, height);
            _view.Update(SampleApplicationLog, SampleAuditLog, channels ?? SampleChannels, 1);
            return _harness.Screen();
        }

        private string[] Resize(int width, int height)
        {
            _harness.Resize(width, height);
            _view.ApplyScreenSize(width, height);
            return _harness.Screen();
        }

        private void AppendApplicationLog(IEnumerable<string> messages) =>
            _view.Update(messages.Select(x => Entry("22:00:00.000", "MBBSEmu.Program", LogLevel.Information, x)).ToList(),
                Array.Empty<AuditLogEntry>(), SampleChannels, 1);

        [Fact]
        public void At80x24_LaysOutSidebarAndLogs()
        {
            var screen = Show(80, 24);

            screen[1].Should().Contain("Audit Log").And.Contain("Channels (1/8)");
            screen[1][60].Should().Be('╔');

            var header = screen.First(x => x.Contains("Level") && x.Contains("Message"));
            header.Should().Contain("Time").And.NotContain("Source");

            screen[23].Should().Contain("^Q").And.Contain("Shutdown")
                .And.Contain("Telnet: ON  Rlogin: OFF  Build 2026.10.26.1")
                .And.NotContain("Next panel");
        }

        [Fact]
        public void At132x40_ShowsSourceAndMilliseconds()
        {
            var screen = Show(132, 40);

            screen.First(x => x.Contains("Level") && x.Contains("Message")).Should().Contain("Source");
            screen.Should().Contain(x => x.Contains("21:07:09.204") && x.Contains("BtrieveFileProcessor"));
            screen[39].Should().Contain("Next panel");
        }

        [Theory]
        [InlineData(79, 24)]
        [InlineData(80, 23)]
        public void Below80x24_ShowsTooSmallScreen(int width, int height)
        {
            var screen = Show(width, height);

            screen.Should().Contain(x => x.Contains("Terminal too small"));
            screen.Should().Contain(x => x.Contains($"Currently {width}×{height}, need at least 80×24"));
            screen.Should().NotContain(x => x.Contains("Audit Log"));
        }

        [Fact]
        public void ResizeAcrossFloor_RestoresLayout()
        {
            Show(80, 24);
            Resize(70, 20).Should().Contain(x => x.Contains("Terminal too small"));

            var screen = Resize(80, 24);

            screen.Should().Contain(x => x.Contains("Audit Log"));
            screen.Should().NotContain(x => x.Contains("Terminal too small"));
        }

        [Fact]
        public void TinySizes_DoNotThrow()
        {
            Show(80, 24);

            var act = () =>
            {
                Resize(10, 3);
                Resize(1, 1);
            };

            act.Should().NotThrow();
            Resize(80, 24).Should().Contain(x => x.Contains("Audit Log"));
        }

        [Fact]
        public void LongMessage_TruncatesWithEllipsis()
        {
            Show(80, 24);
            AppendApplicationLog(new[] { "LONGMESSAGE-" + new string('x', 188) });

            var screen = _harness.Screen();

            screen.First(x => x.Contains("LONGMESSAGE-")).Should().Contain("…");
            screen.Should().OnlyContain(x => x.Length <= 80);
        }

        [Fact]
        public void LongUsername_TruncatesInSidebar()
        {
            var channels = ChannelList.Build(8, new[]
            {
                new ChannelSession(0, "SYSOP"),
                new ChannelSession(1, "ABCDEFGHIJKLMNOPQRSTUVWXYZABC")
            });

            var screen = Show(80, 24, channels);

            screen.Should().Contain(x => x.Contains("1: ABCDEFGHIJKLMN…"));
            screen[1][60].Should().Be('╔');
        }

        [Fact]
        public void Update_FollowsWhenAtBottom()
        {
            Show(80, 24);
            AppendApplicationLog(Enumerable.Range(0, 50).Select(i => $"Entry {i:000}"));
            _harness.Screen().Should().Contain(x => x.Contains("Entry 049"));

            _view.ApplicationLogTable.SetSelection(0, 0, false, null);
            _view.ApplicationLogTable.EnsureCursorIsVisible();
            AppendApplicationLog(Enumerable.Range(50, 5).Select(i => $"Entry {i:000}"));

            _view.ApplicationLogTable.Value!.SelectedCell.Y.Should().Be(0);
            _harness.Screen().Should().NotContain(x => x.Contains("Entry 054"));
        }

        [Fact]
        public void Update_RetainsCapacity_UnderBurst()
        {
            Show(80, 24);

            _view.Update(
                Enumerable.Range(0, 20_000).Select(i => Entry("22:00:00.000", "MBBSEmu.Program", LogLevel.Information, $"Burst {i}")).ToList(),
                Enumerable.Range(0, 1_500).Select(i => new AuditLogEntry("BURST", $"Audit {i}")).ToList(),
                SampleChannels, 1);

            _view.ApplicationLogCount.Should().Be(5_000);
            _view.AuditLogCount.Should().Be(1_000);
            _harness.Screen().Should().Contain(x => x.Contains("Burst 19999"));
        }

        [Fact]
        public void EnterOnApplicationRow_ShowsEntry()
        {
            Show(80, 24);
            _view.ApplicationLogTable.SetFocus();
            _view.ApplicationLogTable.SetSelection(0, ErrorRow, false, null);

            _harness.Key(Key.Enter);

            _dialogs.Entries.Should().ContainSingle();
            _dialogs.Entries[0].Title.Should().Be("Log Entry");
            _dialogs.Entries[0].Text.Should().Contain("Class: MBBSEmu.Btrieve.BtrieveFileProcessor");
        }

        [Fact]
        public void EnterOnAuditRow_ShowsEntry()
        {
            Show(80, 24);
            _view.AuditLogTable.SetFocus();
            _view.AuditLogTable.SetSelection(0, 0, false, null);

            _harness.Key(Key.Enter);

            _dialogs.Entries.Should().Equal(("Audit Entry", "Summary: LOGON\nDetail: SYSOP logged on from 127.0.0.1"));
        }

        [Fact]
        public void MenuShutdown_RaisesShutdownRequested()
        {
            Show(80, 24);
            var requests = 0;
            _view.ShutdownRequested += (_, _) => requests++;

            _view.ShutdownMenuItem.Action!();

            requests.Should().Be(1);
        }

        [Fact]
        public void MenuAbout_ShowsAbout()
        {
            Show(80, 24);

            _view.AboutMenuItem.Action!();

            _dialogs.AboutCalls.Should().Be(1);
        }

        [Fact]
        public void SetShuttingDown_ReplacesHints()
        {
            Show(80, 24);

            _view.SetShuttingDown();

            _harness.Screen()[23].Should().Contain("Shutting down…").And.NotContain("^Q");
        }
    }
}
