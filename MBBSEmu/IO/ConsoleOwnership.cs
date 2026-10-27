namespace MBBSEmu.IO
{
    /// <summary>
    ///     Tracks whether a full-screen UI currently owns the process console
    ///
    ///     While it does, nothing may write to Console directly: raw output lands on top of the UI and garbles it.
    /// </summary>
    public static class ConsoleOwnership
    {
        private static volatile bool _isUiActive;

        public static bool IsUiActive => _isUiActive;

        public static void SetUiActive(bool active) => _isUiActive = active;
    }
}
