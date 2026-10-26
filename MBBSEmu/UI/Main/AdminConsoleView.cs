using MBBSEmu.UI.Logging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace MBBSEmu.UI.Main
{
    /// <summary>
    ///     The Admin Console screen: Audit Log above the Application Log on the left, Channels in a full-height
    ///     sidebar on the right, menu bar on top and status bar at the bottom
    ///
    ///     Display only. The AdminConsole controller feeds it new entries, sizes it when the terminal changes size,
    ///     and handles shutdown when ShutdownRequested is raised.
    /// </summary>
    public sealed class AdminConsoleView : Window
    {
        public const int ApplicationLogCapacity = 5_000;
        public const int AuditLogCapacity = 1_000;

        /// <summary>
        ///     Rows a table uses for its header (titles and underline)
        /// </summary>
        private const int TableHeaderRows = 2;

        private const int LevelColumnWidth = 5;

        private readonly IAdminConsoleDialogs _dialogs;
        private readonly string _statusRightText;

        private readonly BoundedLog<ApplicationLogEntry> _applicationLog = new(ApplicationLogCapacity);
        private readonly BoundedLog<AuditLogEntry> _auditLog = new(AuditLogCapacity);
        private readonly ApplicationLogTableSource _applicationSource;

        private readonly MenuBar _menuBar;
        private readonly FrameView _auditFrame;
        private readonly FrameView _applicationFrame;
        private readonly FrameView _channelsFrame;
        private readonly Label _auditTab;
        private readonly Label _applicationTab;
        private readonly Label _channelsTab;
        private readonly View _statusBar;
        private readonly TooSmallView _tooSmall;

        private readonly ObservableCollection<string> _channelItems = new();
        private IReadOnlyList<ChannelLine> _channelLines = Array.Empty<ChannelLine>();
        private int _longestChannelLine;

        private int _width;
        private int _height;
        private AdminConsoleLayout _layout;
        private bool _shuttingDown;

        /// <summary>
        ///     Raised when the sysop picks Server → Shutdown
        /// </summary>
        public event EventHandler ShutdownRequested;

        internal TableView ApplicationLogTable { get; }
        internal TableView AuditLogTable { get; }
        internal ListView ChannelListView { get; }
        internal MenuItem ShutdownMenuItem { get; }
        internal MenuItem AboutMenuItem { get; }

        public int ApplicationLogCount => _applicationLog.Count;
        public int AuditLogCount => _auditLog.Count;

        /// <param name="dialogs">Dialogs for log entries and About</param>
        /// <param name="statusRightText">Service state and build, from StatusLine.RightText</param>
        public AdminConsoleView(IAdminConsoleDialogs dialogs, string statusRightText)
        {
            _dialogs = dialogs;
            _statusRightText = statusRightText;

            BorderStyle = LineStyle.None;
            SetScheme(ConsoleTheme.Base);

            ShutdownMenuItem = new MenuItem("_Shutdown", "Ctrl+Q", () => ShutdownRequested?.Invoke(this, EventArgs.Empty), Key.Empty);
            AboutMenuItem = new MenuItem("_About", string.Empty, () => _dialogs.ShowAbout(), Key.Empty);
            _menuBar = new MenuBar(new[]
            {
                new MenuBarItem("_Server", new View[] { ShutdownMenuItem }),
                new MenuBarItem("_Help", new View[] { AboutMenuItem })
            })
            {
                X = 0,
                Y = 0,
                Width = Dim.Fill()
            };
            _menuBar.SetScheme(ConsoleTheme.Menu);

            AuditLogTable = CreateTable(new AuditLogTableSource(_auditLog));
            AuditLogTable.Accepting += (_, e) =>
            {
                e.Handled = true;
                var row = SelectedRow(AuditLogTable);
                if (row >= 0 && row < _auditLog.Count)
                    _dialogs.ShowEntry(AdminConsoleText.AuditEntryTitle, LogEntryFormatter.AuditDetail(_auditLog[row]));
            };
            AuditLogTable.Style.RowColorGetter = _ => ConsoleTheme.AuditRow;
            _auditFrame = CreateFrame(AuditLogTable);
            _auditTab = CreateTab(_auditFrame, AdminConsoleText.AuditLogTitle);

            _applicationSource = new ApplicationLogTableSource(_applicationLog);
            ApplicationLogTable = CreateTable(_applicationSource);
            ApplicationLogTable.Style.RowColorGetter = args => _applicationLog[args.RowIndex].Level switch
            {
                Microsoft.Extensions.Logging.LogLevel.Error or Microsoft.Extensions.Logging.LogLevel.Critical => ConsoleTheme.ErrorRow,
                Microsoft.Extensions.Logging.LogLevel.Warning => ConsoleTheme.WarningRow,
                Microsoft.Extensions.Logging.LogLevel.Information => ConsoleTheme.InfoRow,
                Microsoft.Extensions.Logging.LogLevel.Debug or Microsoft.Extensions.Logging.LogLevel.Trace => ConsoleTheme.DebugRow,
                _ => null
            };
            ApplicationLogTable.Accepting += (_, e) =>
            {
                e.Handled = true;
                var row = SelectedRow(ApplicationLogTable);
                if (row >= 0 && row < _applicationLog.Count)
                    _dialogs.ShowEntry(AdminConsoleText.ApplicationEntryTitle, LogEntryFormatter.ApplicationDetail(_applicationLog[row]));
            };
            _applicationFrame = CreateFrame(ApplicationLogTable);
            _applicationTab = CreateTab(_applicationFrame, AdminConsoleText.ApplicationLogTitle);

            ChannelListView = new ListView
            {
                X = 0,
                Y = 0,
                Width = Dim.Fill(),
                Height = Dim.Fill()
            };
            ChannelListView.SetScheme(ConsoleTheme.Base);
            ChannelListView.SetSource(_channelItems);
            ChannelListView.VerticalScrollBar.VisibilityMode = ScrollBarVisibilityMode.Auto;
            ChannelListView.RowRender += (_, e) =>
            {
                if (e.Row < 0 || e.Row >= _channelLines.Count)
                    return;

                //Leave the selected row's highlight alone while the list has focus
                if (ChannelListView.HasFocus && e.Row == ChannelListView.SelectedItem)
                    return;

                var line = _channelLines[e.Row];
                e.RowAttribute = line.IsDisconnected ? ConsoleTheme.DimChannel
                    : line.IsConnecting ? ConsoleTheme.ConnectingChannel
                    : ConsoleTheme.OnlineChannel;
            };
            _channelsFrame = CreateFrame(ChannelListView);
            _channelsTab = CreateTab(_channelsFrame, ChannelList.Title(0, 0));

            _statusBar = new View { X = 0, Height = 1, CanFocus = false };
            _statusBar.SetScheme(ConsoleTheme.Menu);

            _tooSmall = new TooSmallView(AdminConsoleText.TooSmallQuitHint);

            //Tabs after the frames, so they draw over the frames' top borders
            Add(_menuBar, _auditFrame, _applicationFrame, _channelsFrame, _auditTab, _applicationTab, _channelsTab, _statusBar,
                _tooSmall);
        }

        /// <summary>
        ///     Lays the screen out for the given terminal size, or shows the "too small" notice below 80×24
        /// </summary>
        public void ApplyScreenSize(int width, int height)
        {
            _width = width;
            _height = height;

            var tooSmall = ScreenSize.IsBelowMinimum(width, height);
            foreach (var view in new View[] { _menuBar, _auditFrame, _applicationFrame, _channelsFrame, _auditTab, _applicationTab, _channelsTab, _statusBar })
                view.Visible = !tooSmall;

            if (tooSmall)
            {
                _layout = null;
                _tooSmall.Show(width, height);
                SetNeedsLayout();
                return;
            }

            _tooSmall.Hide();
            _layout = AdminConsoleLayout.Calculate(width, height, _longestChannelLine);

            Place(_auditFrame, 0, 1, _layout.LeftWidth, _layout.AuditHeight);
            Place(_applicationFrame, 0, 1 + _layout.AuditHeight, _layout.LeftWidth, _layout.ApplicationLogHeight);
            Place(_channelsFrame, _layout.LeftWidth, 1, _layout.SidebarWidth, height - 2);
            Place(_statusBar, 0, height - 1, width, 1);
            PlaceTab(_auditTab, _auditFrame);
            PlaceTab(_applicationTab, _applicationFrame);
            PlaceTab(_channelsTab, _channelsFrame);

            SizeAuditColumns();
            SizeApplicationColumns();
            RebuildChannelItems();
            RebuildStatusBar();

            SetNeedsLayout();
        }

        /// <summary>
        ///     Adds new log entries and refreshes the channel list
        /// </summary>
        public void Update(IReadOnlyList<ApplicationLogEntry> newApplicationEntries, IReadOnlyList<AuditLogEntry> newAuditEntries,
            IReadOnlyList<ChannelLine> channels, int onlineCount)
        {
            AppendAndFollow(ApplicationLogTable, _applicationLog, newApplicationEntries);
            AppendAndFollow(AuditLogTable, _auditLog, newAuditEntries);

            _channelLines = channels;
            _channelsTab.Text = $" {ChannelList.Title(onlineCount, channels.Count)} ";

            var longest = channels.Count == 0 ? 0 : channels.Max(x => x.Text.Length);
            if (longest != _longestChannelLine)
            {
                //The sidebar sizes itself to the longest channel line
                _longestChannelLine = longest;
                if (_width > 0)
                    ApplyScreenSize(_width, _height);
            }
            else
            {
                RebuildChannelItems();
            }

            SetNeedsDraw();
        }

        /// <summary>
        ///     Replaces the status bar hints with "Shutting down…"
        /// </summary>
        public void SetShuttingDown()
        {
            _shuttingDown = true;
            RebuildStatusBar();
        }

        private static TableView CreateTable(ITableSource source)
        {
            var table = new TableView
            {
                X = 0,
                Y = 0,
                Width = Dim.Fill(),
                Height = Dim.Fill(),
                FullRowSelect = true,
                Table = source
            };
            table.SetScheme(ConsoleTheme.Base);
            table.Style.HeaderScheme = ConsoleTheme.Heading;

            //Sit flush inside the frame: no line above the headers or down the outer edges
            table.Style.AlwaysShowHeaders = true;
            table.Style.ShowHorizontalHeaderOverline = false;
            table.Style.ShowVerticalCellLineForFirstColumn = false;
            table.Style.ShowVerticalCellLineForLastColumn = false;
            table.Style.ShowHorizontalBottomLine = false;

            //Columns get exact widths, so the last one truncates with "…" instead of stretching
            table.Style.ExpandLastColumn = false;
            for (var column = 0; column < source.Columns; column++)
                table.Style.GetOrCreateColumnStyle(column);

            return table;
        }

        private static FrameView CreateFrame(View content)
        {
            var frame = new FrameView { BorderStyle = LineStyle.Double };
            frame.SetScheme(ConsoleTheme.Frame);
            frame.Add(content);
            return frame;
        }

        /// <summary>
        ///     A MajorBBS-style title tab for a panel (drawn over its top border), highlighted while the panel has focus
        /// </summary>
        private static Label CreateTab(FrameView frame, string title)
        {
            var tab = new Label { Text = $" {title} " };
            tab.SetScheme(ConsoleTheme.PanelTab);
            frame.HasFocusChanged += (_, e) => tab.SetScheme(e.NewValue ? ConsoleTheme.FocusedPanelTab : ConsoleTheme.PanelTab);
            return tab;
        }

        private static void PlaceTab(Label tab, View frame)
        {
            tab.X = Pos.Left(frame) + 2;
            tab.Y = Pos.Top(frame);
        }

        private static void Place(View view, int x, int y, int width, int height)
        {
            view.X = x;
            view.Y = y;
            view.Width = width;
            view.Height = height;
        }

        private static void SetColumnWidth(TableView table, int column, int width)
        {
            var style = table.Style.GetOrCreateColumnStyle(column);
            style.MinWidth = width;
            style.MaxWidth = width;
        }

        private static int SelectedRow(TableView table) => table.Value?.SelectedCell.Y ?? -1;

        private void SizeAuditColumns()
        {
            var interior = _layout.LeftWidth - 2;
            SetColumnWidth(AuditLogTable, AuditLogTableSource.SummaryColumn, _layout.AuditSummaryWidth);
            SetColumnWidth(AuditLogTable, AuditLogTableSource.DetailColumn, Math.Max(1, interior - _layout.AuditSummaryWidth - 1));
            AuditLogTable.Update();
        }

        private void SizeApplicationColumns()
        {
            var interior = _layout.LeftWidth - 2;
            var timeWidth = _layout.ShowMilliseconds ? 12 : 8;
            var sourceWidth = _layout.ShowSource ? _layout.SourceWidth : 0;
            var separators = _layout.ShowSource ? 3 : 2;

            _applicationSource.ShowMilliseconds = _layout.ShowMilliseconds;
            SetColumnWidth(ApplicationLogTable, ApplicationLogTableSource.TimeColumn, timeWidth);
            SetColumnWidth(ApplicationLogTable, ApplicationLogTableSource.LevelColumn, LevelColumnWidth);
            SetColumnWidth(ApplicationLogTable, ApplicationLogTableSource.MessageColumn,
                Math.Max(1, interior - timeWidth - sourceWidth - LevelColumnWidth - separators));

            var source = ApplicationLogTable.Style.GetOrCreateColumnStyle(ApplicationLogTableSource.SourceColumn);
            source.Visible = _layout.ShowSource;
            if (_layout.ShowSource)
                SetColumnWidth(ApplicationLogTable, ApplicationLogTableSource.SourceColumn, sourceWidth);

            ApplicationLogTable.Update();
        }

        /// <summary>
        ///     Appends entries to a log, then keeps the table on the newest entry (when following) or on the entry
        ///     the sysop selected
        /// </summary>
        private static void AppendAndFollow<T>(TableView table, BoundedLog<T> log, IReadOnlyList<T> entries)
        {
            if (entries.Count == 0)
                return;

            var previousCount = log.Count;
            var selectedRow = previousCount == 0 ? -1 : SelectedRow(table);
            var visibleRows = Math.Max(1, table.Viewport.Height - TableHeaderRows);
            var lastRowVisible = table.RowOffset + visibleRows >= previousCount;

            var dropped = log.Append(entries);
            var result = FollowSelection.Adjust(selectedRow, lastRowVisible, previousCount, dropped, log.Count);

            table.Update();
            if (result.Following)
            {
                table.SetSelection(0, result.SelectedRow, false, null);
                table.EnsureCursorIsVisible();
                return;
            }

            table.RowOffset = Math.Max(0, table.RowOffset - dropped);
            if (result.SelectedRow >= 0)
                table.SetSelection(0, result.SelectedRow, false, null);
        }

        private void RebuildChannelItems()
        {
            var interior = _layout == null ? int.MaxValue : _layout.SidebarWidth - 2;
            var texts = _channelLines.Select(x => x.Text.Length <= interior ? x.Text : x.Text[..Math.Max(0, interior - 1)] + "…").ToList();

            if (texts.Count != _channelItems.Count)
            {
                _channelItems.Clear();
                foreach (var text in texts)
                    _channelItems.Add(text);
                return;
            }

            //Same channel count: replace only what changed so the selection and scroll position stay put
            for (var i = 0; i < texts.Count; i++)
            {
                if (_channelItems[i] != texts[i])
                    _channelItems[i] = texts[i];
            }
        }

        private void RebuildStatusBar()
        {
            foreach (var label in _statusBar.SubViews.ToList())
            {
                _statusBar.Remove(label);
                label.Dispose();
            }

            if (_width == 0)
                return;

            if (_shuttingDown)
            {
                AddStatusLabel(1, AdminConsoleText.ShuttingDown, ConsoleTheme.Menu);
            }
            else
            {
                var x = 1;
                foreach (var hint in StatusLine.FitHints(_width, StatusLine.AdminConsoleHints, _statusRightText))
                {
                    AddStatusLabel(x, hint.Key, ConsoleTheme.MenuHotkey);
                    AddStatusLabel(x + hint.Key.Length + 1, hint.Label, ConsoleTheme.Menu);
                    x += hint.Key.Length + 1 + hint.Label.Length + 2;
                }
            }

            AddStatusLabel(Math.Max(0, _width - _statusRightText.Length - 1), _statusRightText, ConsoleTheme.Menu);
        }

        private void AddStatusLabel(int x, string text, Scheme scheme)
        {
            var label = new Label { X = x, Y = 0, Text = text };
            label.SetScheme(scheme);
            _statusBar.Add(label);
        }
    }
}
