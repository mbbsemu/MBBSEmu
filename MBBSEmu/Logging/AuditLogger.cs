using MBBSEmu.Logging.Targets;

namespace MBBSEmu.Logging
{
    public class AuditLogger : LoggerBase, IAuditLogger
    {
        public AuditLogger() { }

        public AuditLogger(ILoggingTarget target)
        {
            AddTarget(target);
        }

        public void Log(string summary, string detail)
        {
            foreach (var target in LOGGING_TARGETS)
            {
                target.Write(summary, detail);
            }
        }
    }
}
