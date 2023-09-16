using MBBSEmu.Logging.Targets;
using System.Collections.Generic;

namespace MBBSEmu.Logging
{
    public abstract class LoggerBase
    {
        protected static readonly List<ILoggingTarget> LOGGING_TARGETS = new();

        public void RemoveTarget<T>()
        {
            LOGGING_TARGETS.RemoveAll(x => x.GetType() == typeof(T));
        }

        public void AddTarget(ILoggingTarget target)
        {
            LOGGING_TARGETS.Add(target);
        }
    }
}
