using FluentAssertions;
using Microsoft.Extensions.FileProviders;
using System.IO;
using Xunit;

namespace MBBSEmu.Tests
{
    public class TestEnvironment_Tests
    {
        /// <summary>
        ///     Every test's AppSettingsManager watches appsettings.json for changes. On macOS each file watcher flushes
        ///     the whole disk (sync) as it starts, which across the suite blocks so many thread pool threads that tests
        ///     relying on the pool (the admin console's Terminal.Gui loop) time out. Polling avoids the flush.
        /// </summary>
        [Fact]
        public void FileProviders_PollInsteadOfWatching()
        {
            using var provider = new PhysicalFileProvider(Path.GetTempPath());

            provider.UsePollingFileWatcher.Should().BeTrue();
        }
    }
}
