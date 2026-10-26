using Microsoft.Extensions.Logging;
using System;

namespace MBBSEmu.UI.Logging
{
    /// <summary>
    ///     An Application Log entry as shown in the Admin Console
    /// </summary>
    /// <param name="Timestamp">Local time the entry was logged</param>
    /// <param name="SourceType">Full name of the type that logged the entry, or an empty string</param>
    /// <param name="Level">Log Level of the entry</param>
    /// <param name="Message">Message text, unmodified</param>
    public sealed record ApplicationLogEntry(DateTime Timestamp, string SourceType, LogLevel Level, string Message);

    /// <summary>
    ///     An Audit Log entry (from a module's shocst() call) as shown in the Admin Console
    /// </summary>
    public sealed record AuditLogEntry(string Summary, string Detail);
}
