using System;
using MBBSEmu.Logging.Targets;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

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

        public void Log(LogLevel logLevel, string message, Exception exception = null)
        {
            foreach (var target in LOGGING_TARGETS)
            {
                //Use reflection to get the name of the class calling this method
                var callingClass = new System.Diagnostics.StackTrace().GetFrame(2)?.GetMethod()?.DeclaringType.ToString();

                target.Write(DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss.fff"), callingClass, logLevel, message);
            }
        }

        public void Debug(string message) => Log(LogLevel.Debug, message);

        public void Info(string message) => Log(LogLevel.Information, message);

        public void Warn(string message) => Log(LogLevel.Warning, message);

        public void Warn(Exception exception, string message) => Log(LogLevel.Warning, $"{message} - {exception.Message}");

        public void Error(string message) => Log(LogLevel.Error, message);

        public void Error(Exception exception, string message) => Log(LogLevel.Error, $"{message} - {exception.Message}");

        public void Error(Exception exception) => Log(LogLevel.Error, exception.Message);
    }
}
