namespace MBBSEmu.UI.Main
{
    /// <summary>
    ///     Modal dialogs shown by the Admin Console
    ///
    ///     Kept behind an interface because Terminal.Gui dialogs block until answered, which tests replace with
    ///     scripted answers
    /// </summary>
    public interface IAdminConsoleDialogs
    {
        /// <summary>
        ///     Asks whether to shut down MBBSEmu; true only for Yes
        /// </summary>
        bool ConfirmShutdown();

        /// <summary>
        ///     Asks whether to force quit while a shutdown is still running; true only for Yes
        /// </summary>
        bool ConfirmForceQuit();

        /// <summary>
        ///     Shows the full text of a log entry
        /// </summary>
        void ShowEntry(string title, string text);

        void ShowAbout();
    }
}
