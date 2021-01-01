using NLog;
using NLog.Conditions;
using NLog.Layouts;
using NLog.Targets;
using SQLitePCL;

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

            var consoleLogger = CreateConsoleTarget();
            config.AddTarget(consoleLogger);

            var fileLogger = new FileTarget("fileLogger")
            {
                FileName = @"c:\dos\log\log.txt",
                Layout = Layout.FromString("${shortdate} ${time} ${level} ${callsite} ${message}"),
                DeleteOldFileOnStartup = true
            };
            //config.AddTarget(fileLogger);
            //config.AddRuleForAllLevels(fileLogger);
            LogManager.Configuration = config;
        }

        static ColoredConsoleTarget CreateConsoleTarget()
        {
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

            return consoleLogger;
        }

        /// <summary>
        ///     Disables the Console Logger for NLog
        /// </summary>
        public void DisableConsoleLogging()
        {
            LogManager.Configuration.RemoveTarget("consoleLogger");
        }

        /// <summary>
        ///     Enables the Console Logger for NLog
        /// </summary>
        public void EnableConsoleLogging()
        {
            LogManager.Configuration.AddTarget("consoleLogger", CreateConsoleTarget());
        }
    }
}
