using MBBSEmu.DependencyInjection;
using MBBSEmu.HostProcess;
using MBBSEmu.Logging;
using MBBSEmu.Logging.Targets;
using MBBSEmu.Resources;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Terminal.Gui;

namespace MBBSEmu.UI.Main
{
    [UIMetadata(name: "The MajorBBS Emulation Project", description: "MBBEmu Main Host Window")]
    public class MainView : UIBase
    {
        private readonly IQueueTarget _logQueueTarget = new QueueTarget();
        private readonly IQueueTarget _auditQueueTarget = new QueueTarget();
        private readonly DataTable _logTable;
        private readonly DataTable _auditLogTable;

        private TableView _tableView;
        private TableView _auditLogTableView;
        private ListView _channelListView;

        private readonly List<string> _channelList = new();

        private readonly ServiceResolver _serviceResolver;
        private readonly AppSettingsManager _appSettingsManager;

        public MainView(ServiceResolver serviceResolver) : base()
        {
            //Overwrite the Logger to use the QueueTarget
            var logFactory = serviceResolver.GetService<LogFactory>();
            logFactory.AddLogger(new MessageLogger((ILoggingTarget)_logQueueTarget));
            logFactory.AddLogger(new AuditLogger((ILoggingTarget)_auditQueueTarget));

            _logTable = new DataTable();
            _logTable.Columns.Add("Time", typeof(string));
            _logTable.Columns.Add("Class", typeof(string));
            _logTable.Columns.Add("Level", typeof(string));
            _logTable.Columns.Add("Message", typeof(string));

            _auditLogTable = new DataTable();
            _auditLogTable.Columns.Add("Summary", typeof(string));
            _auditLogTable.Columns.Add("Detail", typeof(string));

            _serviceResolver = serviceResolver;
            _appSettingsManager = serviceResolver.GetService<AppSettingsManager>();
        }

        /// <summary>
        ///     Sets up the UI Elements to be used by this View
        /// </summary>
        public override void Setup()
        {
            //Menu Bar
            var menu = new MenuBar(new MenuBarItem[]
            {
                new MenuBarItem("_Server", new MenuItem[]
                {
                    new MenuItem("_Shutdown", string.Empty, ShutdownServer, null, null, Key.Q | Key.CtrlMask),
                }),
                new MenuBarItem( "_Help", new MenuItem[]
                {
                    new MenuItem("_About", string.Empty, () => MessageBox.Query("About MBBSEmu", $"The MajorBBS Emulation Project\n" +
                        $"Build #{new ResourceManager().GetString("MBBSEmu.Assets.version.txt")}\n\n" +
                        "Open Source & Community Driven!\n" +
                        "Distributed under MIT License\n\n" +
                        "https://github.com/mbbsemu/MBBSEmu\n" +
                        "https://www.mbbsemu.com", "OK")),
                })
            });
            menu.Text = "Test";
            menu.TextAlignment = TextAlignment.Right;
            MainWindow.Add(menu);

            //Audit Log Window
            var auditLogContainer = new Window
            {
                X = 0,
                Y = 1,
                Width = Dim.Percent(75),
                Height = Dim.Percent(50) - 1,
                Border = new Border
                {
                    BorderBrush = Color.BrightCyan,
                    BorderStyle = BorderStyle.Double,
                    Title = "Audit Log"
                }
            };
            _auditLogTableView = new TableView
            {
                X = 0,
                Y = 0,
                Width = Dim.Fill(),
                Height = Dim.Fill(),
                Table = _auditLogTable,
                PreserveTrailingSpaces = false,
                Style = new TableView.TableStyle { AlwaysShowHeaders = true }
            };

            //Use local Function to return new ColumnStyle with specified MinWidth and MaxWidth
            TableView.ColumnStyle GetAuditLogStyle(int minWidth, int maxWidth) => new()
            {
                MinWidth = minWidth,
                MaxWidth = maxWidth
            };

            _auditLogTableView.Style.ColumnStyles.Add(_auditLogTableView.Table.Columns["Summary"], GetAuditLogStyle(20, 20));
            _auditLogTableView.Style.ColumnStyles.Add(_auditLogTableView.Table.Columns["Detail"], GetAuditLogStyle(50, 50));

            auditLogContainer.Add(_auditLogTableView);
            MainWindow.Add(auditLogContainer);

            //Channel List Window
            var channelListContainer = new Window("")
            {
                X = Pos.Right(auditLogContainer),
                Y = 1,
                Width = Dim.Fill(),
                Height = Dim.Percent(50) - 1,
                Border = new Border
                {
                    BorderBrush = Color.BrightCyan,
                    BorderStyle = BorderStyle.Double,
                    Title = "Channels"
                }
            };
            _channelListView = new ListView(_channelList)
            {
                X = 0,
                Y = 0,
                Width = Dim.Fill(),
                Height = Dim.Fill()
            };
            channelListContainer.Add(_channelListView);
            MainWindow.Add(channelListContainer);

            //Application Log Window
            var windowLogContainer = new Window
            {
                X = 0,
                Y = Pos.Percent(50),
                Width = Dim.Fill(),
                Height = Dim.Fill(),
                Border = new Border
                {
                    BorderBrush = Color.BrightCyan,
                    BorderStyle = BorderStyle.Double,
                    Title = "MBBSEmu Application Log"
                }
            };

            _tableView = new TableView
            {
                X = 0,
                Y = 0,
                Width = Dim.Fill(),
                Height = Dim.Fill(),
                Table = _logTable,
                PreserveTrailingSpaces = false,
                Style = new TableView.TableStyle { AlwaysShowHeaders = true },
            };

            //Color Scheme used for ERROR level log entries
            var errorColorScheme = new ColorScheme()
            {
                Disabled = windowLogContainer.ColorScheme.Disabled,
                HotFocus = windowLogContainer.ColorScheme.HotFocus,
                Focus = windowLogContainer.ColorScheme.Focus,
                Normal = Application.Driver.MakeAttribute(Color.Red, windowLogContainer.ColorScheme.Normal.Background)
            };

            //Use local Function to return new ColumnStyle with specified MinWidth and MaxWidth
            TableView.ColumnStyle GetApplicationLogStyle(int minWidth, int maxWidth) => new()
            {
                ColorGetter = (a) => a.Table.Rows[a.RowIndex].ItemArray[2]?.ToString() == LogLevel.Error.ToString() ? errorColorScheme : windowLogContainer.ColorScheme,
                MinWidth = minWidth, 
                MaxWidth = maxWidth
            };

            _tableView.Style.ColumnStyles.Add(_tableView.Table.Columns["Time"], GetApplicationLogStyle(12, 12)); 
            _tableView.Style.ColumnStyles.Add(_tableView.Table.Columns["Class"], GetApplicationLogStyle(36, 36)); 
            _tableView.Style.ColumnStyles.Add(_tableView.Table.Columns["Level"], GetApplicationLogStyle(5, 5));
            _tableView.Style.ColumnStyles.Add(_tableView.Table.Columns["Message"], GetApplicationLogStyle(60, 60));
            windowLogContainer.Add(_tableView);

            //When the user double clicks on a cell, show a message box with the full log entry
            _tableView.CellActivated += (e) =>
            {
                var logEntry = _logTable.Rows[e.Row].ItemArray;
                MessageBox.Query("Log Entry", $"Time: {logEntry[0]}\nClass: {logEntry[1]}\nLevel: {logEntry[2]}\nMessage: {logEntry[3]}", "OK");
            };
            MainWindow.Add(windowLogContainer);

            //Status Bar
            var telnetStatus = new StatusItem(Key.CharMask, $"~Telnet:~ {(_appSettingsManager.TelnetEnabled ? "ENABLED" : "DISABLED")}", null);
            var rloginStatus = new StatusItem(Key.CharMask, $"~Rlogin:~ {(_appSettingsManager.RloginEnabled ? "ENABLED" : "DISABLED")}", null);
            var statusBarName = new StatusItem(Key.CharMask, $"The MajorBBS Emulation Project (Build #{new ResourceManager().GetString("MBBSEmu.Assets.version.txt")})", null);
            var statusBar = new StatusBar(new[] { telnetStatus, rloginStatus, statusBarName })
            {
                Visible = true
            };

            MainWindow.Add(statusBar);
        }

        public override void Run()
        {
            isRunning = true;

            //Start a new Task to dequeue the QueueTarget and add it to the DataTable
            Task.Run(() =>
            {
                while (true)
                {
                    //Check to see if the MainWindow is running, if not, break out of the loop
                    if (!isRunning)
                        break;

                    UpdateAuditLog();

                    UpdateChannelList();

                    UpdateApplicationLog();

                    Application.DoEvents();

                    Thread.Sleep(250);
                }
            });

            base.Run();
        }

        /// <summary>
        ///    Updates the UI Audit Log View with the latest Audit Log Entries
        /// </summary>
        private void UpdateAuditLog()
        {

            var auditEntry = _auditQueueTarget.DequeueAll();
            if (auditEntry != null)
            {
                foreach (var entry in auditEntry)
                {
                    //Split the log entry into its parts (Time, Class, Level, Message
                    _auditLogTable.Rows.Add((string)entry[2], (string)entry[3]);
                }

                //Only Force Updates when new log entries are added
                if (auditEntry.Count > 0)
                {
                    _auditLogTableView?.Update();
                    _auditLogTableView?.ChangeSelectionToEndOfTable(false);
                    _auditLogTableView?.ChangeSelectionToStartOfRow(false);
                }
            }

        }

        /// <summary>
        ///     Updates the UI Log View with the latest Log Entries
        /// </summary>
        private void UpdateApplicationLog()
        {
            var logEntry = _logQueueTarget.DequeueAll();
            if (logEntry != null)
            {
                foreach (var entry in logEntry)
                {
                    var logDate = ((DateTime)entry[0]).ToString("HH:mm:ss.fff");
                    var logClass = (entry[1].ToString())?[8..];
                    var logLevel = string.Empty;
                    var logMessage = (string)entry[3];

                    //Evaluate message LogLevel and set string to be the level name up to 5 characters
                    logLevel = (LogLevel)entry[2] switch
                    {
                        LogLevel.Trace => "TRACE",
                        LogLevel.Debug => "DEBUG",
                        LogLevel.Information => "INFO",
                        LogLevel.Warning => "WARN",
                        LogLevel.Error => "ERROR",
                        LogLevel.Critical => "CRIT",
                        LogLevel.None => "NONE",
                        _ => logLevel
                    };

                    _logTable.Rows.Add(
                        logDate, logClass, logLevel, logMessage);
                }

                //Only Force Updates when new log entries are added
                if (logEntry.Count > 0)
                {
                    _tableView?.Update();
                    _tableView?.ChangeSelectionToEndOfTable(false);
                    _tableView?.ChangeSelectionToStartOfRow(false);
                }
            }
        }

        /// <summary>
        ///     Updates the UI ListView with the List of Channels and their current status
        /// </summary>
        private void UpdateChannelList()
        {
            _channelList.Clear();
            var userSessions = _serviceResolver.GetService<IMbbsHost>().GetUserSessions();
            for (var i = 0; i < _appSettingsManager.BBSChannels; i++)
            {
                var channel = userSessions.FirstOrDefault(x => x.Channel == i);

                if (channel != null)
                {
                    _channelList.Add(!string.IsNullOrEmpty(channel.Username)
                        ? $"{i}: {channel.Username.ToUpper()}"
                        : $"{i}: CONNECTING");
                }
                else
                {
                    _channelList.Add($"{i}: DISCONNECTED");
                }
            }
            _channelListView.SetNeedsDisplay();
        }

        /// <summary>
        ///     Gracefully stops MBBSEmu and exits the application
        /// </summary>
        private void ShutdownServer()
        {
            var _mbbsHost = _serviceResolver.GetService<IMbbsHost>();
            _mbbsHost.Stop();
            _mbbsHost.WaitForShutdown();

            Application.RequestStop();
        }
    }
}