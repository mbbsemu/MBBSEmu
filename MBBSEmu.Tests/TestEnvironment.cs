using System;
using System.Runtime.CompilerServices;

namespace MBBSEmu.Tests
{
    /// <summary>
    ///     Process-wide settings for the test run, applied as the test assembly loads, before any test
    /// </summary>
    internal static class TestEnvironment
    {
        /// <summary>
        ///     Every test's AppSettingsManager watches appsettings.json for changes. On macOS each file watcher flushes
        ///     the whole disk (sync) as it starts; thousands of them tie up the thread pool for over an hour and starve
        ///     the admin console tests. Polling for changes instead avoids the flush. MBBSEmu itself is unaffected: it
        ///     only ever has one watcher.
        /// </summary>
        [ModuleInitializer]
        internal static void PollForFileChanges() =>
            Environment.SetEnvironmentVariable("DOTNET_USE_POLLING_FILE_WATCHER", "1");
    }
}
