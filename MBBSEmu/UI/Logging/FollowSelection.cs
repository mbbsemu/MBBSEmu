using System;

namespace MBBSEmu.UI.Logging
{
    /// <summary>
    ///     Selected row after new entries arrive, and whether the view is following the newest entry
    /// </summary>
    public sealed record FollowResult(int SelectedRow, bool Following);

    /// <summary>
    ///     Follow-mode rules for the Admin Console logs: a log follows new entries while the sysop is at the
    ///     bottom, and stays on the entry they selected once they scroll up
    /// </summary>
    public static class FollowSelection
    {
        /// <param name="selectedRow">Selected row before the append, or -1 for none</param>
        /// <param name="lastRowVisible">True if the last row was on screen before the append</param>
        /// <param name="previousCount">Entry count before the append</param>
        /// <param name="dropped">Oldest entries dropped by the append</param>
        /// <param name="newCount">Entry count after the append</param>
        public static FollowResult Adjust(int selectedRow, bool lastRowVisible, int previousCount, int dropped, int newCount)
        {
            var following = previousCount == 0 ||
                            ((selectedRow < 0 || selectedRow >= previousCount - 1) && lastRowVisible);

            if (following)
                return new FollowResult(newCount - 1, true);

            //Rows shift up by the number of entries dropped from the top; stay on the same entry
            return new FollowResult(selectedRow < 0 ? -1 : Math.Max(0, selectedRow - dropped), false);
        }
    }
}
