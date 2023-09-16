using System;
using MBBSEmu.Logging;
using MBBSEmu.Logging.Targets;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Terminal.Gui;

namespace MBBSEmu.UI.Main
{
    [UIMetadata(name: "Host Window", description: "MBBEmu Main Host Window")]
    public class MainView : UIBase
    {
        private readonly IQueueTarget _queueTarget = new QueueTarget();
        private readonly DataTable _logTable;
        private TableView _tableView;

        public MainView() : base()
        {
            //Overwrite the Logger to use the QueueTarget
            new LogFactory().AddLogger(new MessageLogger((ILoggingTarget)_queueTarget));

            _logTable = new DataTable();
            _logTable.Columns.Add("Time", typeof(string));
            _logTable.Columns.Add("Class", typeof(string));
            _logTable.Columns.Add("Level", typeof(string));
            _logTable.Columns.Add("Message", typeof(string));
        }

        public override void Setup()
        {
            _tableView = new TableView
            {
                X = 0,
                Y = Pos.Center(),
                Width = Dim.Fill(),
                Height = Dim.Fill(),
                Table = _logTable,
                PreserveTrailingSpaces = false,
                
            };
            MainWindow.Add(_tableView);

            var menu = new MenuBar(new MenuBarItem[] {
                new MenuBarItem ("_File", new MenuItem [] {
                    new MenuItem ("_Quit", "", null),
                }),
            });

            MainWindow.Add(menu);
        }

        public override void Run()
        {
            isRunning = true;

            //Start a new Task to dequeue the QueueTarget and add it to the DataTable
            Task.Run(() =>
            {
                while (true)
                {
                    var logEntry = _queueTarget.DequeueAll();

                    if (logEntry != null)
                    {
                        foreach (var entry in logEntry)
                        {
                            //Split the log entry into its parts (Time, Class, Level, Message
                            var logEntryParts = entry.Split('\t');

                            _logTable.Rows.Add(logEntryParts[0], logEntryParts[1], logEntryParts[2],
                                string.Join(" ", logEntryParts.Skip(3)));
                        }

                        //Check to see if the MainWindow is running, if not, break out of the loop
                        if (!isRunning)
                            break;

                        //Only Force Updates when new log entries are added
                        if (logEntry.Count > 0)
                        {
                            _tableView?.Update();
                            _tableView?.ChangeSelectionToEndOfTable(false);
                            _tableView?.ChangeSelectionToStartOfRow(false);
                            _tableView?.Redraw(_tableView.Bounds);
                        }
                    }
                }
            });

            base.Run();
        }
    }
}
