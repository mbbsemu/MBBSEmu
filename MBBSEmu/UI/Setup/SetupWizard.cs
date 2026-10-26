using System;
using System.IO;
using Terminal.Gui.App;

namespace MBBSEmu.UI.Setup
{
    /// <summary>
    ///     Runs the Setup Wizard full screen and restores the terminal when it ends
    /// </summary>
    public static class SetupWizard
    {
        public static SetupWizardResult Run()
        {
            var app = Application.Create();
            app.Init(null);
            ConsoleTheme.ApplyGlobalDefaults();
            try
            {
                var view = new SetupWizardView(new SetupWizardModel(new Random()), new TerminalGuiSetupWizardDialogs(app),
                    File.WriteAllText);

                app.ScreenChanged += (_, e) => view.ApplyScreenSize(e.Value.Width, e.Value.Height);
                view.ApplyScreenSize(app.Screen.Width, app.Screen.Height);

                app.Run(view);

                return view.Outcome ?? new SetupWizardResult(false, null);
            }
            finally
            {
                app.Dispose();
            }
        }
    }
}
