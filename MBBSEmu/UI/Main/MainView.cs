﻿using MBBSEmu.DependencyInjection;
using MBBSEmu.HostProcess;
using MBBSEmu.Logging;
using MBBSEmu.Logging.Targets;
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
            var logFactory = new LogFactory();
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
            var menu = new MenuBar(new MenuBarItem[] {
                new MenuBarItem ("_Server", new MenuItem [] {
                    new MenuItem ("_Shutdown", string.Empty, ShutdownServer ),
                }),
            });
            MainWindow.Add(menu);

            //Audit Log Window
            var auditLogContainer = new Window("Audit Log")
            {
                X = 0,
                Y = 1,
                Width = Dim.Percent(75),
                Height = Dim.Percent(50) - 1
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
            auditLogContainer.Add(_auditLogTableView);
            MainWindow.Add(auditLogContainer);

            //Channel List Window
            var channelListContainer = new Window("Channels")
            {
                X = Pos.Right(auditLogContainer),
                Y = 1,
                Width = Dim.Fill(),
                Height = Dim.Percent(50) - 1
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
            var windowLogContainer = new Window("MBBSEmu Log")
            {
                X = 0,
                Y = Pos.Percent(50),
                Width = Dim.Fill(),
                Height = Dim.Fill()
            };

            _tableView = new TableView
            {
                X = 0,
                Y = 0,
                Width = Dim.Fill(),
                Height = Dim.Fill(),
                Table = _logTable,
                PreserveTrailingSpaces = false,
                Style = new TableView.TableStyle { AlwaysShowHeaders = true }
                
            };
            var _logMessageColumnStyle = new TableView.ColumnStyle()
            {
                MaxWidth = 58
            };
            _tableView.Style.ColumnStyles.Add(_tableView.Table.Columns["Message"], _logMessageColumnStyle);
            windowLogContainer.Add(_tableView);

            //When the user double clicks on a cell, show a message box with the full log entry
            _tableView.CellActivated += (e) =>
            {
                var logEntry = _logTable.Rows[e.Row].ItemArray;
                MessageBox.Query("Log Entry", $"Time: {logEntry[0]}\nClass: {logEntry[1]}\nLevel: {logEntry[2]}\nMessage: {logEntry[3]}", "OK");
            };
            MainWindow.Add(windowLogContainer);
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

        private void UpdateAuditLog()
        {

            var auditEntry = _auditQueueTarget.DequeueAll();
            if (auditEntry != null)
            {
                foreach (var entry in auditEntry)
                {
                    //Split the log entry into its parts (Time, Class, Level, Message
                    _auditLogTable.Rows.Add((string)entry[0], (string)entry[1]);
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
                    var logDate = ((string)entry[0]).Split(' ')[1];
                    var logClass = ((string)entry[1])[8..];
                    var logLevel = ((EnumLogLevel)entry[2]).ToString();
                    var logMessage = (string)entry[3];

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