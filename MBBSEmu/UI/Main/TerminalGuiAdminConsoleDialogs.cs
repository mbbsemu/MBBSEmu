using MBBSEmu.UI.Modules;
using System;
using System.Collections.Generic;
using System.IO;
using Terminal.Gui.App;
using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace MBBSEmu.UI.Main
{
    /// <summary>
    ///     Admin Console dialogs drawn with Terminal.Gui
    /// </summary>
    public sealed class TerminalGuiAdminConsoleDialogs : IAdminConsoleDialogs
    {
        private readonly IApplication _app;
        private readonly string _build;

        public TerminalGuiAdminConsoleDialogs(IApplication app, string build)
        {
            _app = app;
            _build = build;
        }

        public bool ConfirmShutdown() => AskYesNo(AdminConsoleText.ShutdownTitle, AdminConsoleText.ShutdownMessage);

        public bool ConfirmForceQuit() => AskYesNo(AdminConsoleText.ForceQuitTitle, AdminConsoleText.ForceQuitMessage);

        /// <summary>
        ///     Scrollable, word-wrapped view of the entry that always fits the screen
        /// </summary>
        public void ShowEntry(string title, string text)
        {
            using var dialog = new Dialog
            {
                Title = title,
                Width = Dim.Percent(80),
                Height = Dim.Percent(70),
                BorderStyle = LineStyle.Double
            };
            dialog.SetScheme(ConsoleTheme.DialogScheme);

            //TextView is marked obsolete in favor of a separate editor package, but it is still the built-in
            //read-only, word-wrapping, scrollable text view, which is all this dialog needs
#pragma warning disable CS0618
            var body = new TextView
            {
                Text = text,
                ReadOnly = true,
                WordWrap = true,
                Width = Dim.Fill(),
                Height = Dim.Fill(1)
            };
#pragma warning restore CS0618
            body.SetScheme(ConsoleTheme.DialogScheme);
            dialog.Add(body);

            var ok = new Button { Text = AdminConsoleText.OkButton, IsDefault = true };
            ok.Accepting += (_, e) =>
            {
                e.Handled = true;
                _app.RequestStop(dialog);
            };
            dialog.AddButton(ok);

            _app.Run(dialog);
        }

        public void ShowAbout() =>
            MessageBox.Query(_app, AdminConsoleText.AboutTitle, AdminConsoleText.AboutMessage(_build), AdminConsoleText.OkButton);

        /// <summary>
        ///     File picker showing only module definitions; opens in the home folder if startFolder is gone
        /// </summary>
        public string PickModuleDefinition(string startFolder)
        {
            var folder = !string.IsNullOrEmpty(startFolder) && Directory.Exists(startFolder)
                ? startFolder
                : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            using var dialog = new OpenDialog
            {
                Title = ModuleLoadText.PickerTitle,
                OpenMode = OpenMode.File,
                MustExist = true,
                AllowedTypes = new List<IAllowedType> { new AllowedType(ModuleLoadText.MdfFiles, ".MDF", ".mdf") },
                //A trailing separator opens the folder itself rather than selecting it in its parent
                Path = Path.EndsInDirectorySeparator(folder) ? folder : folder + Path.DirectorySeparatorChar
            };

            _app.Run(dialog);

            return dialog.Canceled || string.IsNullOrEmpty(dialog.Path) ? null : dialog.Path;
        }

        public void ShowMessage(string title, string message) =>
            MessageBox.Query(_app, title, message, AdminConsoleText.OkButton);

        public void ShowError(string title, string message) =>
            MessageBox.ErrorQuery(_app, title, message, AdminConsoleText.OkButton);

        /// <summary>
        ///     Yes/No question that defaults to No; Esc or closing the dialog also counts as No
        ///
        ///     MessageBox makes its last button the default (what Enter presses), so No goes last.
        /// </summary>
        private bool AskYesNo(string title, string message) =>
            MessageBox.Query(_app, title, message, AdminConsoleText.YesButton, AdminConsoleText.NoButton) == 0;
    }
}
