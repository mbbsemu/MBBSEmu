namespace MBBSEmu.UI.Setup
{
    /// <summary>
    ///     Outcome of the Setup Wizard
    /// </summary>
    /// <param name="Saved">True if appsettings.json was written</param>
    /// <param name="Path">Where it was written, or null</param>
    public sealed record SetupWizardResult(bool Saved, string Path);

    /// <summary>
    ///     Modal dialogs shown by the Setup Wizard, behind an interface so tests can answer them
    /// </summary>
    public interface ISetupWizardDialogs
    {
        void ShowError(string title, string message);

        /// <summary>
        ///     Asks whether to write the settings; true only for Yes
        /// </summary>
        bool ConfirmWrite();

        /// <summary>
        ///     Asks where to save the settings
        /// </summary>
        /// <returns>The chosen path, or null if cancelled</returns>
        string AskSavePath(string directory, string fileName);

        void ShowSaved(string path);
    }
}
