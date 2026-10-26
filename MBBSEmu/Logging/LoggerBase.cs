using MBBSEmu.Logging.Targets;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace MBBSEmu.Logging
{
    public abstract class LoggerBase
    {
        private readonly object _targetsLock = new();

        /// <summary>
        ///     Current Logging Targets
        ///
        ///     Replaced (never modified) on every change, so a logger can be writing on one thread while
        ///     targets are added or removed on another
        /// </summary>
        private ILoggingTarget[] _targets = Array.Empty<ILoggingTarget>();

        /// <summary>
        ///     Snapshot of the Logging Targets at the time of the call
        /// </summary>
        protected IReadOnlyList<ILoggingTarget> LOGGING_TARGETS => Volatile.Read(ref _targets);

        protected LogLevel ConfiguredLogLevel = LogLevel.Information;

        /// <summary>
        ///     Removes every Logging Target of type T
        /// </summary>
        /// <returns>True if any target was removed</returns>
        public bool RemoveTarget<T>()
        {
            lock (_targetsLock)
            {
                var remaining = _targets.Where(x => x.GetType() != typeof(T)).ToArray();
                if (remaining.Length == _targets.Length)
                    return false;

                Volatile.Write(ref _targets, remaining);
                return true;
            }
        }

        /// <summary>
        ///     Removes the specified Logging Target instance
        /// </summary>
        /// <returns>True if the target was removed</returns>
        public bool RemoveTarget(ILoggingTarget target)
        {
            lock (_targetsLock)
            {
                var remaining = _targets.Where(x => !ReferenceEquals(x, target)).ToArray();
                if (remaining.Length == _targets.Length)
                    return false;

                Volatile.Write(ref _targets, remaining);
                return true;
            }
        }

        public bool HasTarget<T>() => Volatile.Read(ref _targets).Any(x => x.GetType() == typeof(T));

        public void AddTarget(ILoggingTarget target)
        {
            lock (_targetsLock)
            {
                Volatile.Write(ref _targets, _targets.Append(target).ToArray());
            }
        }

        public void SetLogLevel(LogLevel logLevel)
        {
            ConfiguredLogLevel = logLevel;
        }
    }
}
