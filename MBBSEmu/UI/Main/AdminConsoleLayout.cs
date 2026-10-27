using System;

namespace MBBSEmu.UI.Main
{
    /// <summary>
    ///     Panel and column sizes for the Admin Console at a given terminal size
    ///
    ///     The Channels sidebar runs full height on the right; the Audit Log sits above the Application Log on the
    ///     left. Rows 0 and (height - 1) are the menu bar and status bar.
    /// </summary>
    /// <param name="SidebarWidth">Width of the Channels sidebar, including its border</param>
    /// <param name="LeftWidth">Width of the Audit Log and Application Log panels, including borders</param>
    /// <param name="AuditHeight">Height of the Audit Log panel, including borders</param>
    /// <param name="ApplicationLogHeight">Height of the Application Log panel, including borders</param>
    /// <param name="AuditSummaryWidth">Width of the Audit Log Summary column</param>
    /// <param name="ShowSource">Whether the Application Log shows its Source column</param>
    /// <param name="SourceWidth">Width of the Source column when shown</param>
    /// <param name="ShowMilliseconds">Whether Application Log times include milliseconds</param>
    public sealed record AdminConsoleLayout(
        int SidebarWidth,
        int LeftWidth,
        int AuditHeight,
        int ApplicationLogHeight,
        int AuditSummaryWidth,
        bool ShowSource,
        int SourceWidth,
        bool ShowMilliseconds)
    {
        public const int MinimumSidebarWidth = 20;

        /// <summary>
        ///     Minimum Audit Log height (borders, header, separator and four rows)
        /// </summary>
        private const int MinimumAuditHeight = 8;

        /// <summary>
        ///     Share of the body height given to the Audit Log
        /// </summary>
        private const double AuditHeightShare = 0.35;

        /// <summary>
        ///     Audit Log Summary width on the narrowest terminals
        /// </summary>
        private const int MinimumAuditSummaryWidth = 10;

        /// <summary>
        ///     The longest audit trail summary a module can write (AUDBRIEFSIZ in the Worldgroup SDK's AUDAPI.H, less
        ///     its nul)
        /// </summary>
        private const int MaximumAuditSummaryWidth = 32;

        /// <summary>
        ///     Detail characters kept before the Summary column widens: the 63-character detail of a classic MajorBBS
        ///     audit trail record (AUDOLDDETSIZ in AUDAPI.H, less its two nuls)
        /// </summary>
        private const int AuditDetailWidthBeforeSummaryWidens = 63;

        /// <summary>
        ///     Application Log interior width at which the Source column and milliseconds appear
        /// </summary>
        private const int SourceColumnMinimumInterior = 70;

        /// <summary>
        ///     Application Log interior width at which the Source column widens
        /// </summary>
        private const int WideSourceMinimumInterior = 100;

        /// <param name="screenWidth">Terminal width in columns, at least ScreenSize.MinimumWidth</param>
        /// <param name="screenHeight">Terminal height in rows, at least ScreenSize.MinimumHeight</param>
        /// <param name="longestChannelLine">Length of the longest channel line (e.g. "4: SYSOP")</param>
        public static AdminConsoleLayout Calculate(int screenWidth, int screenHeight, int longestChannelLine)
        {
            var bodyHeight = screenHeight - 2;

            var sidebarWidth = Math.Clamp(longestChannelLine + 2, MinimumSidebarWidth,
                Math.Max(MinimumSidebarWidth, screenWidth / 4));
            var leftWidth = screenWidth - sidebarWidth;

            var auditHeight = Math.Max(MinimumAuditHeight,
                (int)Math.Round(bodyHeight * AuditHeightShare, MidpointRounding.AwayFromZero));

            var logInterior = leftWidth - 2;
            var showSource = logInterior >= SourceColumnMinimumInterior;

            //Detail gets its classic 63 characters first, then Summary widens up to its 32, then Detail takes the rest
            var auditSummaryWidth = Math.Clamp(logInterior - 1 - AuditDetailWidthBeforeSummaryWidens,
                MinimumAuditSummaryWidth, MaximumAuditSummaryWidth);

            return new AdminConsoleLayout(
                SidebarWidth: sidebarWidth,
                LeftWidth: leftWidth,
                AuditHeight: auditHeight,
                ApplicationLogHeight: bodyHeight - auditHeight,
                AuditSummaryWidth: auditSummaryWidth,
                ShowSource: showSource,
                SourceWidth: logInterior >= WideSourceMinimumInterior ? 20 : 14,
                ShowMilliseconds: showSource);
        }
    }
}
