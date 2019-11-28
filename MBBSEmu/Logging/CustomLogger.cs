using NLog;
using NLog.Conditions;
using NLog.Layouts;
using NLog.Targets;

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
            var logconsole = new ColoredConsoleTarget("logconsole")
            {
                Layout = Layout.FromString("${shortdate} ${time} ${level} ${callsite} ${message}"),
                UseDefaultRowHighlightingRules = true
            };

            logconsole.RowHighlightingRules.Add(new ConsoleRowHighlightingRule()
            {
                Condition = ConditionParser.ParseExpression("level == LogLevel.Debug"),
                ForegroundColor = ConsoleOutputColor.Gray
            });

            logconsole.RowHighlightingRules.Add(new ConsoleRowHighlightingRule()
            {
                Condition = ConditionParser.ParseExpression("level == LogLevel.Info"),
                ForegroundColor = ConsoleOutputColor.White
            });

            logconsole.RowHighlightingRules.Add(new ConsoleRowHighlightingRule()
            {
                Condition = ConditionParser.ParseExpression("level == LogLevel.Warn"),
                ForegroundColor = ConsoleOutputColor.DarkYellow
            });

            logconsole.RowHighlightingRules.Add(new ConsoleRowHighlightingRule()
            {
                Condition = ConditionParser.ParseExpression("level == LogLevel.Error"),
                ForegroundColor = ConsoleOutputColor.Red
            });

            config.AddTarget(logconsole);

            var logfile = new FileTarget("logfile")
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
