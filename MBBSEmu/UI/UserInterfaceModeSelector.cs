namespace MBBSEmu.UI
{
    public enum EnumUserInterfaceMode
    {
        /// <summary>
        ///     Full-screen Admin Console
        /// </summary>
        AdminConsole,

        /// <summary>
        ///     Log lines written straight to the terminal
        /// </summary>
        Cli
    }

    /// <summary>
    ///     Decides whether MBBSEmu shows the Admin Console or runs as -CLI
    /// </summary>
    public static class UserInterfaceModeSelector
    {
        /// <summary>
        ///     The Admin Console only runs for a normal server run with a terminal attached
        /// </summary>
        public static EnumUserInterfaceMode Select(bool cliRequested, bool consoleSessionRequested, bool apiReportRequested,
            bool interactiveTerminal) =>
            !cliRequested && !consoleSessionRequested && !apiReportRequested && interactiveTerminal
                ? EnumUserInterfaceMode.AdminConsole
                : EnumUserInterfaceMode.Cli;

        /// <summary>
        ///     Started with no arguments in a terminal, e.g. by double-clicking it: MBBSEmu runs from its own folder,
        ///     sets itself up on first run and opens the Admin Console with the modules loaded last time
        /// </summary>
        public static bool IsGuiMode(int argumentCount, bool interactiveTerminal) => argumentCount == 0 && interactiveTerminal;

        /// <summary>
        ///     True when the Admin Console would have run but there is no terminal, e.g. under systemd
        /// </summary>
        public static bool IsHeadlessFallback(bool cliRequested, bool consoleSessionRequested, bool apiReportRequested,
            bool interactiveTerminal) =>
            !cliRequested && !consoleSessionRequested && !apiReportRequested && !interactiveTerminal;
    }
}
