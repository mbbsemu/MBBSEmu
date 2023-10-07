using MBBSEmu.Logging.Targets;

namespace MBBSEmu.Logging
{
    /// <summary>
    ///     Logger to handle messages to be sent to the Audit Log
    /// </summary>
    public class AuditLogger : LoggerBase, IAuditLogger
    {
        public AuditLogger() { }

        public AuditLogger(ILoggingTarget target)
        {
            AddTarget(target);
        }

        /// <summary>
        ///     Send message to the Audit Log
        /// </summary>
        /// <param name="summary"></param>
        /// <param name="detail"></param>
        public void Log(string summary, string detail)
        {
            foreach (var target in LOGGING_TARGETS)
            {
                target.Write(summary, detail);
            }
        }
    }
}
