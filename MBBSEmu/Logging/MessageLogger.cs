using System;
using MBBSEmu.Logging.Targets;
using System.Collections.Generic;

namespace MBBSEmu.Logging
{
    /// <summary>
    ///     Logger used for logging Messages along with Log Levels (DEBUG, INFO, WARN, ERROR)
    ///
    ///     Messages are in turn written to the specified Logging Targets
    /// </summary>
    public class MessageLogger : LoggerBase, IMessageLogger
    {
        public MessageLogger() { }

        public MessageLogger(ILoggingTarget target)
        {
            AddTarget(target);
        }

        public void Log(EnumLogLevel logLevel, string message, Exception exception = null)
        {
            foreach (var target in LOGGING_TARGETS)
            {
                target.Write(message, logLevel);
            }
        }

        public void Debug(string message) => Log(EnumLogLevel.Debug, message);

        public void Info(string message) => Log(EnumLogLevel.Info, message);

        public void Warn(string message) => Log(EnumLogLevel.Warn, message);

        public void Warn(Exception exception, string message) => Log(EnumLogLevel.Warn, $"{message} - {exception.Message}");

        public void Error(string message) => Log(EnumLogLevel.Error, message);

        public void Error(Exception exception, string message) => Log(EnumLogLevel.Error, $"{message} - {exception.Message}");

        public void Error(Exception exception) => Log(EnumLogLevel.Error, exception.Message);
    }
}
