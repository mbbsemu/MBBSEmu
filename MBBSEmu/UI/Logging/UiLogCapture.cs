using MBBSEmu.Logging;
using MBBSEmu.Logging.Targets;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MBBSEmu.UI.Logging
{
    /// <summary>
    ///     Redirects the shared loggers from the console to the Admin Console while it runs
    ///
    ///     Targets are swapped on the existing logger instances rather than replacing loggers in LogFactory, so
    ///     every reference to a logger, including ones fetched before the UI started, stops writing to the
    ///     console and feeds the UI instead.
    /// </summary>
    public sealed class UiLogCapture : IDisposable
    {
        /// <summary>
        ///     Entries held between UI refreshes before new ones are dropped
        /// </summary>
        public const int QueueCapacity = 10_000;

        private readonly LoggerBase _messageLogger;
        private readonly LoggerBase _auditLogger;
        private readonly QueueTarget _applicationQueue = new(QueueCapacity);
        private readonly QueueTarget _auditQueue = new(QueueCapacity);
        private readonly bool _messageLoggerHadConsole;
        private readonly bool _auditLoggerHadConsole;
        private bool _disposed;

        public UiLogCapture(LoggerBase messageLogger, LoggerBase auditLogger)
        {
            _messageLogger = messageLogger;
            _auditLogger = auditLogger;

            //Add the queue first so nothing logged during the swap is lost
            _messageLogger.AddTarget(_applicationQueue);
            _messageLoggerHadConsole = _messageLogger.RemoveTarget<ConsoleTarget>();

            _auditLogger.AddTarget(_auditQueue);
            _auditLoggerHadConsole = _auditLogger.RemoveTarget<ConsoleTarget>();
        }

        public IReadOnlyList<ApplicationLogEntry> DrainApplicationLog() =>
            _applicationQueue.DequeueAll().Select(LogEntryFormatter.ToApplicationEntry).ToList();

        public IReadOnlyList<AuditLogEntry> DrainAuditLog() =>
            _auditQueue.DequeueAll().Select(LogEntryFormatter.ToAuditEntry).ToList();

        /// <summary>
        ///     Restores console logging on each logger that had it
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            if (_messageLoggerHadConsole)
                _messageLogger.AddTarget(new ConsoleTarget());
            _messageLogger.RemoveTarget(_applicationQueue);

            if (_auditLoggerHadConsole)
                _auditLogger.AddTarget(new ConsoleTarget());
            _auditLogger.RemoveTarget(_auditQueue);
        }
    }
}
