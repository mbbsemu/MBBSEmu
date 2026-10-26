using Terminal.Gui.App;
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
                Height = Dim.Percent(70)
            };
            dialog.SetScheme(ConsoleTheme.Base);

            var body = new TextView
            {
                Text = text,
                ReadOnly = true,
                WordWrap = true,
                Width = Dim.Fill(),
                Height = Dim.Fill(1)
            };
            body.SetScheme(ConsoleTheme.Base);
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
        ///     Yes/No question that defaults to No; Esc or closing the dialog also counts as No
        /// </summary>
        private bool AskYesNo(string title, string message) =>
            MessageBox.Query(_app, title, message, AdminConsoleText.NoButton, AdminConsoleText.YesButton) == 1;
    }
}
