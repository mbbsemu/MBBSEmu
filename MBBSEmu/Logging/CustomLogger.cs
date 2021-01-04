using NLog;
using NLog.Conditions;
using NLog.Layouts;
using NLog.Targets;
using System.IO;

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
            config.AddRuleForAllLevels(consoleLogger);
            LogManager.Configuration = config;
            LogManager.Configuration.Variables["mbbsdir"] = Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar;
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
