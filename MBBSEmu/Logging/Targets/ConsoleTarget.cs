using Microsoft.Extensions.Logging;
using System;

namespace MBBSEmu.Logging.Targets
{
    /// <summary>
    ///     Target for Logging Messages to the Console
    /// </summary>
    public class ConsoleTarget : ILoggingTarget
    {
        public ConsoleTarget() { }

        /// <summary>
        ///     Writes the log entry to the Console
        /// </summary>
        /// <param name="logEntry">params object[] consisting of (logLevel, logMessage)</param>
        public void Write(params object[] logEntry)
        {
            var logLevel =  logEntry[0] as string ?? ((LogLevel)logEntry[0]).ToString();
            var logMessage = (string)logEntry[1];

            //Use reflection to get the name of the class calling this method
            //TODO: We should move this reflection up the stack to the log factory in GetLogger<T>() and pass it into the logger so we don't have to do this every time
            var callingType = new System.Diagnostics.StackTrace().GetFrame(3)?.GetMethod()?.DeclaringType;

            Console.WriteLine($"[{DateTime.Now:O}] {callingType} [{logLevel}] {logMessage}");
        }
    }
}
