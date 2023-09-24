using System;

namespace MBBSEmu.Logging
{
    public interface IMessageLogger
    {
        void Log(EnumLogLevel logLevel, string message, Exception exception = null);
        void Debug(string message);
        void Info(string message);
        void Warn(string message);
        void Warn(Exception exception, string message);
        void Error(string message);
        void Error(Exception exception, string message);
        void Error(Exception exception);
    }
}
