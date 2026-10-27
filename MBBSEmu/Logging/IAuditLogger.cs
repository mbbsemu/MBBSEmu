using MBBSEmu.Logging.Targets;

namespace MBBSEmu.Logging
{
    public interface IAuditLogger
    {
        void Log(string summary, string detail);
        bool RemoveTarget<T>();
        void AddTarget(ILoggingTarget target);
    }
}
