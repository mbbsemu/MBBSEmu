using System;

namespace MBBSEmu.Logging.Targets
{
    /// <summary>
    ///     Target for Logging Messages to the Console
    /// </summary>
    public class ConsoleTarget : ILoggingTarget
    {
        public ConsoleTarget() { }

        public void Write(params object[] logEntry)
        {
            if(logEntry.Length != 0)
                return;

            var logMessage = (string)logEntry[0];
            var logLevel = ((EnumLogLevel)logEntry[1]).ToString();

            //Use reflection to get the name of the class calling this method
            var callingType = new System.Diagnostics.StackTrace().GetFrame(3)?.GetMethod()?.DeclaringType;

            Console.WriteLine($"[{DateTime.Now:O}] {callingType} [{logLevel}] {logMessage}");
        }
    }
}
