using MBBSEmu.Logging.Targets;
using Microsoft.Extensions.Logging;
using System;

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

        /// <summary>
        ///     Logs the specific message, exception, or both
        /// </summary>
        /// <param name="logLevel">LogLevel corresponding to Microsoft.Extensions.Logging</param>
        /// <param name="message">Message to be Logged</param>
        /// <param name="exception">Exception to be Logged</param>
        public void Log(LogLevel logLevel, string message, Exception exception = null)
        {
            //Ignore Message if it's below the configured log level
            if((int)logLevel < (int)ConfiguredLogLevel)
                return;

            //Write to all Logging Targets
            foreach (var target in LOGGING_TARGETS)
            {
                if(!string.IsNullOrEmpty(message))
                    target.Write(logLevel, message);

                if (exception != null)
                {
                    target.Write(logLevel, exception.Message);
                    target.Write(logLevel, exception.StackTrace);
                }
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
