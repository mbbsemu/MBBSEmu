using NLog;
using NLog.Conditions;
using NLog.Layouts;
using NLog.Targets;

namespace MBBSEmu.Logging
{
    /// <summary>
    ///     NLog Custom Logger
    /// </summary>
    public class CustomLogger : Logger
    {

        static CustomLogger()
        {
            var config = new NLog.Config.LoggingConfiguration();

            //Setup Console Logging
            var consoleLogger = new ColoredConsoleTarget("consoleLogger")
            {
                Layout = Layout.FromString("${shortdate} ${time} ${level} ${callsite} ${message}"),
                UseDefaultRowHighlightingRules = true
            };

            consoleLogger.RowHighlightingRules.Add(new ConsoleRowHighlightingRule()
            {
                Condition = ConditionParser.ParseExpression("level == LogLevel.Debug"),
                ForegroundColor = ConsoleOutputColor.Gray
            });

            consoleLogger.RowHighlightingRules.Add(new ConsoleRowHighlightingRule()
            {
                Condition = ConditionParser.ParseExpression("level == LogLevel.Info"),
                ForegroundColor = ConsoleOutputColor.White
            });

            consoleLogger.RowHighlightingRules.Add(new ConsoleRowHighlightingRule()
            {
                Condition = ConditionParser.ParseExpression("level == LogLevel.Warn"),
                ForegroundColor = ConsoleOutputColor.DarkYellow
            });

            consoleLogger.RowHighlightingRules.Add(new ConsoleRowHighlightingRule()
            {
                Condition = ConditionParser.ParseExpression("level == LogLevel.Error"),
                ForegroundColor = ConsoleOutputColor.Red
            });

            config.AddTarget(consoleLogger);

            var fileLogger = new FileTarget("fileLogger")
            {
                FileName = @"c:\dos\log\log.txt",
                Layout = Layout.FromString("${shortdate} ${time} ${level} ${callsite} ${message}"),
                DeleteOldFileOnStartup = true
            };
            config.AddTarget(fileLogger);
            config.AddRuleForAllLevels(consoleLogger);
            config.AddRuleForAllLevels(fileLogger);
            LogManager.Configuration = config;
        }
    }
}
