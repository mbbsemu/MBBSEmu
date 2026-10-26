namespace MBBSEmu.UI.Main
{
    /// <summary>
    ///     User-visible text for the Admin Console
    /// </summary>
    public static class AdminConsoleText
    {
        public const string AboutTitle = "About MBBSEmu";
        public const string OkButton = "OK";
        public const string YesButton = "Yes";
        public const string NoButton = "No";

        public const string AuditLogTitle = "Audit Log";
        public const string ApplicationLogTitle = "Application Log";

        public const string ShutdownTitle = "Shutdown";
        public const string ShutdownMessage = "Shut down MBBSEmu? All connected users will be disconnected.";
        public const string ForceQuitTitle = "Force Quit";
        public const string ForceQuitMessage = "Force quit now? Shutdown has not finished.";
        public const string ShuttingDown = "Shutting down…";
        public const string TooSmallQuitHint = "Ctrl+Q still shuts down MBBSEmu.";

        public const string ApplicationEntryTitle = "Log Entry";
        public const string AuditEntryTitle = "Audit Entry";

        public static string AboutMessage(string build) => "The MajorBBS Emulation Project\n" +
                                                           $"Build #{build}\n\n" +
                                                           "Open Source & Community Driven!\n" +
                                                           "Distributed under MIT License\n\n" +
                                                           "https://github.com/mbbsemu/MBBSEmu\n" +
                                                           "https://www.mbbsemu.com";
    }
}
