using NLog;
using NLog.Layouts;

namespace MBBSEmu.Logging
{
    /// <summary>
    ///     NLog Custom Logger
    ///
    ///     Handy for debugging within the application. Really only implemented
    ///     where I need it while working on a specific portion of XamariNES.
    /// </summary>
    public class CustomLogger : Logger
    {

        static CustomLogger()
        {
            var config = new NLog.Config.LoggingConfiguration();

            //Setup Console Logging
            var logconsole = new NLog.Targets.ConsoleTarget("logconsole")
            {
                Layout = Layout.FromString("${shortdate} ${time} ${level} ${callsite} ${message}")
            };
            config.AddTarget(logconsole);

            var logfile = new NLog.Targets.FileTarget("logfile")
            {
                FileName = @"c:\dos\log\log.txt",
                Layout = Layout.FromString("${shortdate} ${time} ${level} ${callsite} ${message}"),
                DeleteOldFileOnStartup = true
            };
            config.AddTarget(logfile);
            config.AddRuleForAllLevels(logconsole);
            config.AddRuleForAllLevels(logfile);
            LogManager.Configuration = config;
        }
    }
}
