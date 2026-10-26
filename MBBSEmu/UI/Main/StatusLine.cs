using System.Collections.Generic;

namespace MBBSEmu.UI.Main
{
    /// <summary>
    ///     A keyboard shortcut hint on the status bar (e.g. "^Q Shutdown")
    /// </summary>
    public sealed record StatusHint(string Key, string Label);

    /// <summary>
    ///     Status bar contents for the Admin Console: shortcut hints on the left, service state and build on the right
    /// </summary>
    public static class StatusLine
    {
        public static readonly IReadOnlyList<StatusHint> AdminConsoleHints = new[]
        {
            new StatusHint("^Q", "Shutdown"),
            new StatusHint("Enter", "View entry"),
            new StatusHint("Tab", "Next panel")
        };

        public static string RightText(bool telnetEnabled, bool rloginEnabled, string build) =>
            $"Telnet: {(telnetEnabled ? "ON" : "OFF")}  Rlogin: {(rloginEnabled ? "ON" : "OFF")}  Build {build}";

        /// <summary>
        ///     Hints that fit to the left of rightText, in order. The right text never gives way: hints are dropped
        ///     from the end until the rest fit with a gap of at least two columns.
        /// </summary>
        public static IReadOnlyList<StatusHint> FitHints(int screenWidth, IReadOnlyList<StatusHint> hints, string rightText)
        {
            var fitted = new List<StatusHint>();
            var limit = screenWidth - rightText.Length - 3;
            var x = 1;

            foreach (var hint in hints)
            {
                var width = hint.Key.Length + 1 + hint.Label.Length;
                if (x + width > limit)
                    break;

                fitted.Add(hint);
                x += width + 2;
            }

            return fitted;
        }
    }
}
