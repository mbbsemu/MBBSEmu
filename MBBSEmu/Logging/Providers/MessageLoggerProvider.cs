using Microsoft.Extensions.Logging;

namespace MBBSEmu.Logging.Providers
{
    /// <summary>
    ///    Provides a MessageLogger for Microsoft.Extensions.Logging
    /// </summary>
    public class MessageLoggerProvider : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName)
        {
            return new MessageLogger();
        }

        public void Dispose()
        {
            // Dispose any resources if needed
        }
    }
}
