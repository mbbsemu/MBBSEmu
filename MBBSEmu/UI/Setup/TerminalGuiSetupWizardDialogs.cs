using Terminal.Gui.App;
using Terminal.Gui.Views;

namespace MBBSEmu.UI.Setup
{
    /// <summary>
    ///     Setup Wizard dialogs drawn with Terminal.Gui
    /// </summary>
    public sealed class TerminalGuiSetupWizardDialogs : ISetupWizardDialogs
    {
        private readonly IApplication _app;

        public TerminalGuiSetupWizardDialogs(IApplication app)
        {
            _app = app;
        }

        public void ShowError(string title, string message) =>
            MessageBox.ErrorQuery(_app, title, message, SetupWizardText.OkButton);

        /// <summary>
        ///     MessageBox makes its last button the default, so No goes last
        /// </summary>
        public bool ConfirmWrite() =>
            MessageBox.Query(_app, SetupWizardText.ConfirmTitle, SetupWizardText.ConfirmMessage,
                SetupWizardText.YesButton, SetupWizardText.NoButton) == 0;

        public void ShowSaved(string path) =>
            MessageBox.Query(_app, SetupWizardText.SuccessTitle, SetupWizardText.SuccessMessage(path), SetupWizardText.OkButton);
    }
}
