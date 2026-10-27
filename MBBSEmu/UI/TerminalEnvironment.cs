using System;

namespace MBBSEmu.UI
{
    /// <summary>
    ///     Facts about the terminal MBBSEmu was started from
    /// </summary>
    public static class TerminalEnvironment
    {
        public const string NoTerminalMessage = "No interactive terminal detected; running in CLI mode";

        /// <summary>
        ///     The UI needs a real terminal on both ends: input from a keyboard and output to a screen
        /// </summary>
        public static bool IsInteractive(bool inputRedirected, bool outputRedirected) => !inputRedirected && !outputRedirected;

        public static bool IsInteractive() => IsInteractive(Console.IsInputRedirected, Console.IsOutputRedirected);

        /// <summary>
        ///     Whether the locale tells the terminal (and tmux) to expect UTF-8, which the UI's box-drawing
        ///     characters need. Follows POSIX precedence: LC_ALL, then LC_CTYPE, then LANG.
        /// </summary>
        public static bool IsUtf8Locale(bool isWindows, Func<string, string> getEnvironmentVariable)
        {
            if (isWindows)
                return true;

            foreach (var name in new[] { "LC_ALL", "LC_CTYPE", "LANG" })
            {
                var value = getEnvironmentVariable(name);
                if (string.IsNullOrEmpty(value))
                    continue;

                return value.Contains("UTF-8", StringComparison.OrdinalIgnoreCase) ||
                       value.Contains("UTF8", StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }

        public static string Utf8Warning(bool insideTmux) =>
            "Warning: the terminal locale is not UTF-8, so the admin console may not draw its borders correctly. Set LANG=C.UTF-8 before starting MBBSEmu." +
            (insideTmux ? " Inside tmux, also start tmux with -u." : string.Empty);
    }
}
