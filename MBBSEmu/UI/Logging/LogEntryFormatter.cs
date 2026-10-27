using Microsoft.Extensions.Logging;
using System;
using System.Globalization;
using System.Text;

namespace MBBSEmu.UI.Logging
{
    /// <summary>
    ///     Formats log entries for display in the Admin Console
    /// </summary>
    public static class LogEntryFormatter
    {
        /// <summary>
        ///     Level name shown in the Level column, at most five characters
        /// </summary>
        public static string LevelLabel(LogLevel level) => level switch
        {
            LogLevel.Trace => "TRACE",
            LogLevel.Debug => "DEBUG",
            LogLevel.Information => "INFO",
            LogLevel.Warning => "WARN",
            LogLevel.Error => "ERROR",
            LogLevel.Critical => "CRIT",
            _ => "NONE"
        };

        /// <summary>
        ///     Short type name shown in the Source column (e.g. MBBSEmu.HostProcess.MbbsHost becomes MbbsHost)
        /// </summary>
        public static string ShortSource(string fullTypeName)
        {
            if (string.IsNullOrEmpty(fullTypeName))
                return string.Empty;

            //Drop generic arity and type arguments (LRUCache`2[System.String,...])
            var end = fullTypeName.IndexOfAny(new[] { '`', '[' });
            var name = end >= 0 ? fullTypeName[..end] : fullTypeName;

            return name[(name.LastIndexOfAny(new[] { '.', '+' }) + 1)..];
        }

        /// <summary>
        ///     Collapses a message onto one row: line breaks and tabs become spaces, other control
        ///     characters (such as ESC in ANSI sequences) become '?'
        /// </summary>
        public static string SingleLine(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            var result = new StringBuilder(text.Length);
            for (var i = 0; i < text.Length; i++)
            {
                var c = text[i];
                switch (c)
                {
                    case '\r' when i + 1 < text.Length && text[i + 1] == '\n':
                        continue;
                    case '\r':
                    case '\n':
                    case '\t':
                        result.Append(' ');
                        break;
                    default:
                        result.Append(char.IsControl(c) ? '?' : c);
                        break;
                }
            }

            return result.ToString();
        }

        public static string FormatTime(DateTime timestamp, bool withMilliseconds) =>
            timestamp.ToString(withMilliseconds ? "HH:mm:ss.fff" : "HH:mm:ss", CultureInfo.InvariantCulture);

        /// <summary>
        ///     Full text of an Application Log entry for the entry dialog
        /// </summary>
        public static string ApplicationDetail(ApplicationLogEntry entry) =>
            $"Time: {entry.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture)}\n" +
            $"Class: {entry.SourceType}\n" +
            $"Level: {LevelLabel(entry.Level)}\n" +
            $"Message: {entry.Message}";

        /// <summary>
        ///     Full text of an Audit Log entry for the entry dialog
        /// </summary>
        public static string AuditDetail(AuditLogEntry entry) => $"Summary: {entry.Summary}\nDetail: {entry.Detail}";

        /// <summary>
        ///     Converts a QueueTarget item written by MessageLogger: [DateTime, calling Type, LogLevel, message]
        /// </summary>
        public static ApplicationLogEntry ToApplicationEntry(object[] queueItem)
        {
            var sourceType = queueItem[1] switch
            {
                Type type => type.FullName ?? type.ToString(),
                null => string.Empty,
                var other => other.ToString()
            };

            return new ApplicationLogEntry(
                (DateTime)queueItem[0],
                sourceType,
                queueItem[2] is LogLevel level ? level : LogLevel.None,
                queueItem[3] as string ?? string.Empty);
        }

        /// <summary>
        ///     Converts a QueueTarget item written by AuditLogger: [DateTime, calling Type, summary, detail]
        /// </summary>
        public static AuditLogEntry ToAuditEntry(object[] queueItem) =>
            new(queueItem[2] as string ?? string.Empty, queueItem[3] as string ?? string.Empty);
    }
}
