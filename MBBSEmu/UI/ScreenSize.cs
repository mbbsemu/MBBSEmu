using System.Collections.Generic;

namespace MBBSEmu.UI
{
    /// <summary>
    ///     Minimum terminal size for the Admin Console and Setup Wizard
    /// </summary>
    public static class ScreenSize
    {
        public const int MinimumWidth = 80;
        public const int MinimumHeight = 24;

        public static bool IsBelowMinimum(int width, int height) => width < MinimumWidth || height < MinimumHeight;

        /// <summary>
        ///     Lines shown in place of the UI while the terminal is below the minimum size
        /// </summary>
        /// <param name="quitHint">Last line, telling the user how to leave without resizing</param>
        public static IReadOnlyList<string> TooSmallMessage(int width, int height, string quitHint) => new[]
        {
            "Terminal too small",
            "",
            $"Currently {width}×{height}, need at least {MinimumWidth}×{MinimumHeight}",
            "",
            "Resize the window to continue.",
            quitHint
        };
    }
}
