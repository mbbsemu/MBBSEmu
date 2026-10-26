using System.Collections.Generic;
using System.IO;
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

        public string AskSavePath(string directory, string fileName)
        {
            using var dialog = new SaveDialog
            {
                Title = SetupWizardText.SaveTitle,
                Path = Path.Combine(directory, fileName),
                AllowedTypes = new List<IAllowedType> { new AllowedType(SetupWizardText.JsonFiles, SetupWizardText.SaveExtension) }
            };

            _app.Run(dialog);

            return dialog.Canceled || string.IsNullOrEmpty(dialog.Path) ? null : dialog.Path;
        }

        public void ShowSaved(string path) =>
            MessageBox.Query(_app, SetupWizardText.SuccessTitle, SetupWizardText.SuccessMessage(path), SetupWizardText.OkButton);
    }
}
