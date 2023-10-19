using MBBSEmu.Logging.Targets;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;

namespace MBBSEmu.Logging
{
    public abstract class LoggerBase
    {
        protected readonly List<ILoggingTarget> LOGGING_TARGETS = new();

        protected LogLevel ConfiguredLogLevel = LogLevel.Information;

        public void RemoveTarget<T>()
        {
            LOGGING_TARGETS.RemoveAll(x => x.GetType() == typeof(T));
        }

        public void AddTarget(ILoggingTarget target)
        {
            LOGGING_TARGETS.Add(target);
        }

        public void SetLogLevel(LogLevel logLevel)
        {
            ConfiguredLogLevel = logLevel;
        }
    }
}
