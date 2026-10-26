using MBBSEmu.UI.Logging;
using Terminal.Gui.Views;

namespace MBBSEmu.UI.Main
{
    /// <summary>
    ///     Application Log rows for a TableView: Time, Source, Level, Message
    /// </summary>
    public sealed class ApplicationLogTableSource : ITableSource
    {
        public const int TimeColumn = 0;
        public const int SourceColumn = 1;
        public const int LevelColumn = 2;
        public const int MessageColumn = 3;

        private static readonly string[] Names = { "Time", "Source", "Level", "Message" };

        private readonly BoundedLog<ApplicationLogEntry> _log;

        public ApplicationLogTableSource(BoundedLog<ApplicationLogEntry> log)
        {
            _log = log;
        }

        /// <summary>
        ///     Whether the Time column includes milliseconds
        /// </summary>
        public bool ShowMilliseconds { get; set; }

        public object this[int row, int col]
        {
            get
            {
                var entry = _log[row];
                return col switch
                {
                    TimeColumn => LogEntryFormatter.FormatTime(entry.Timestamp, ShowMilliseconds),
                    SourceColumn => LogEntryFormatter.ShortSource(entry.SourceType),
                    LevelColumn => LogEntryFormatter.LevelLabel(entry.Level),
                    _ => LogEntryFormatter.SingleLine(entry.Message)
                };
            }
        }

        public string[] ColumnNames => Names;

        public int Columns => Names.Length;

        public int Rows => _log.Count;
    }

    /// <summary>
    ///     Audit Log rows for a TableView: Summary, Detail
    /// </summary>
    public sealed class AuditLogTableSource : ITableSource
    {
        public const int SummaryColumn = 0;
        public const int DetailColumn = 1;

        private static readonly string[] Names = { "Summary", "Detail" };

        private readonly BoundedLog<AuditLogEntry> _log;

        public AuditLogTableSource(BoundedLog<AuditLogEntry> log)
        {
            _log = log;
        }

        public object this[int row, int col]
        {
            get
            {
                var entry = _log[row];
                return LogEntryFormatter.SingleLine(col == SummaryColumn ? entry.Summary : entry.Detail);
            }
        }

        public string[] ColumnNames => Names;

        public int Columns => Names.Length;

        public int Rows => _log.Count;
    }
}
