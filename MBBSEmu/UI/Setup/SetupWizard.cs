using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using Terminal.Gui.App;

namespace MBBSEmu.UI.Setup
{
    /// <summary>
    ///     Runs the Setup Wizard full screen and restores the terminal when it ends
    /// </summary>
    public static class SetupWizard
    {
        /// <param name="settingsPath">
        ///     The appsettings.json to save to. If it already exists, the wizard starts from its values and keeps the
        ///     settings it doesn't edit.
        /// </param>
        public static SetupWizardResult Run(string settingsPath)
        {
            var app = Application.Create();
            app.Init(null);
            ConsoleTheme.ApplyGlobalDefaults();
            ConsoleTheme.StyleDialogsOf(app);
            try
            {
                var model = new SetupWizardModel(new Random());
                if (ReadSettings(settingsPath) is { } existing)
                    model.LoadFrom(existing);

                var view = new SetupWizardView(model, new TerminalGuiSetupWizardDialogs(app), settingsPath, File.WriteAllText);

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

        /// <returns>The existing settings, or null if there are none or they aren't a JSON object</returns>
        private static JsonObject ReadSettings(string settingsPath)
        {
            if (!File.Exists(settingsPath))
                return null;

            try
            {
                return JsonNode.Parse(File.ReadAllText(settingsPath)) as JsonObject;
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }
}
