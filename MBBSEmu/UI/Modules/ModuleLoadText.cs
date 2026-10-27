namespace MBBSEmu.UI.Modules
{
    /// <summary>
    ///     User-visible text for loading modules from the admin console
    /// </summary>
    public static class ModuleLoadText
    {
        public const string LoadedTitle = "Module Loaded";
        public const string AlreadyLoadedTitle = "Already Loaded";
        public const string FailedTitle = "Couldn't Load Module";
        public const string SkippedTitle = "Some Modules Were Skipped";
        public const string PickerTitle = "Load Module";
        public const string MdfFiles = "Module definition (.MDF)";

        /// <summary>
        ///     Shown on the menu bar until a module is loaded
        /// </summary>
        public const string NoModulesHint = "No modules loaded yet — Modules → Load Module (Ctrl+O)";

        public const string StartedWithConfiguration = "MBBSEmu was started with a module configuration";
        public const string UnreadableModulesFileReason = "modules.json couldn't be read";

        public static string Loaded(string name, string key) => $"{name} is loaded. It's on the main menu as {key}.";

        public static string LoadedForSession(string name, string key, string reason) =>
            $"{name} is loaded for this session only ({reason}). It's on the main menu as {key}.";

        public static string LoadedNotSaved(string name, string key, string reason) =>
            $"{name} is loaded, but it couldn't be saved to modules.json ({reason}), so you'll need to load it again next time. It's on the main menu as {key}.";

        public static string AlreadyLoaded(string name) => $"{name} is already loaded.";

        public static string NotFound(string id, string path) => $"Couldn't find {id} at {path}.";

        public static string Failed(string id, string reason) =>
            $"{id} couldn't be loaded:\n{reason}\n\nNothing was saved, and MBBSEmu is still running.";

        public static string NotAnMdf(string fileName) => $"{fileName} isn't a module definition. Choose the module's .MDF file.";

        /// <summary>
        ///     Names the identifier only: the module's name is in its MDF, which is what's missing
        /// </summary>
        public static string SkippedNotFound(string id, string path) => $"Skipped {id}: not found at {path}";

        public static string SkippedFailed(string id, string reason) => $"Skipped {id}: {reason}";

        public static string ModulesFileUnreadable(string path, string reason) =>
            $"{path} couldn't be read, so no saved modules were loaded and it won't be changed:\n{reason}";

        /// <summary>
        ///     How to connect and play, from which services are turned on
        /// </summary>
        public static string PlayHint(bool telnetEnabled, int telnetPort, bool rloginEnabled, int rloginPort)
        {
            if (telnetEnabled)
                return $"Connect a telnet client to localhost, port {telnetPort}, to play.";

            if (rloginEnabled)
                return $"Telnet is turned off, so connect with Rlogin to localhost, port {rloginPort}, to play.";

            return "Telnet and Rlogin are both turned off, so nobody can connect to play yet.";
        }
    }
}
