using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MBBSEmu.UI
{
    /// <summary>
    ///     Where MBBSEmu keeps its files when started with no arguments, and whether it has been set up yet
    /// </summary>
    public static class GuiStartup
    {
        private const string SettingsFileName = "appsettings.json";
        private const string DefaultDatabaseFileName = "mbbsemu.db";

        /// <summary>
        ///     The folder holding the MBBSEmu executable: appsettings.json, modules.json and the BBS databases live
        ///     here, whichever folder MBBSEmu was started from
        /// </summary>
        public static string HomeFolder => AppContext.BaseDirectory;

        /// <summary>
        ///     True if there are no settings, or no user database (Database.File, mbbsemu.db by default) beside them.
        ///     Settings that aren't valid JSON don't count: the normal startup reports them rather than the wizard
        ///     writing over them.
        /// </summary>
        public static bool IsFirstRun(string homeFolder)
        {
            var settingsFile = Path.Combine(homeFolder, SettingsFileName);
            if (!File.Exists(settingsFile))
                return true;

            JsonNode settings;
            try
            {
                settings = JsonNode.Parse(File.ReadAllText(settingsFile));
            }
            catch (JsonException)
            {
                return false;
            }

            //Only the file name is used, as AppSettingsManager does
            var databaseFile = settings?["Database.File"] is JsonValue value ? Path.GetFileName(value.ToString()) : null;
            if (string.IsNullOrEmpty(databaseFile))
                databaseFile = DefaultDatabaseFileName;

            return !File.Exists(Path.Combine(homeFolder, databaseFile));
        }
    }
}
