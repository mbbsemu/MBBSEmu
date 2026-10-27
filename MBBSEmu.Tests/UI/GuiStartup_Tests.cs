using FluentAssertions;
using MBBSEmu.UI;
using System;
using System.IO;
using Xunit;

namespace MBBSEmu.Tests.UI
{
    public class GuiStartup_Tests : IDisposable
    {
        private readonly string _home = Path.Combine(Path.GetTempPath(), $"mbbsemu-home-{Guid.NewGuid():N}");

        public GuiStartup_Tests() => Directory.CreateDirectory(_home);

        public void Dispose() => Directory.Delete(_home, recursive: true);

        private void Write(string fileName, string contents) => File.WriteAllText(Path.Combine(_home, fileName), contents);

        [Fact]
        public void IsFirstRun_NoSettings_True()
        {
            GuiStartup.IsFirstRun(_home).Should().BeTrue();
        }

        [Fact]
        public void IsFirstRun_SettingsWithoutDatabase_True()
        {
            Write("appsettings.json", "{}");

            GuiStartup.IsFirstRun(_home).Should().BeTrue();
        }

        [Fact]
        public void IsFirstRun_SettingsAndDefaultDatabase_False()
        {
            Write("appsettings.json", "{}");
            Write("mbbsemu.db", "");

            GuiStartup.IsFirstRun(_home).Should().BeFalse();
        }

        [Fact]
        public void IsFirstRun_UsesDatabaseFileSetting()
        {
            Write("appsettings.json", """{"Database.File":"data/custom.db"}""");
            Write("custom.db", "");

            GuiStartup.IsFirstRun(_home).Should().BeFalse("only the file name is used, as AppSettingsManager does");

            File.Delete(Path.Combine(_home, "custom.db"));

            GuiStartup.IsFirstRun(_home).Should().BeTrue();
        }

        /// <summary>
        ///     A broken settings file is left for the normal startup to report rather than overwritten by the wizard
        /// </summary>
        [Fact]
        public void IsFirstRun_InvalidSettingsJson_False()
        {
            Write("appsettings.json", "{ not json");

            GuiStartup.IsFirstRun(_home).Should().BeFalse();
        }

        [Fact]
        public void HomeFolder_IsTheExecutablesFolder()
        {
            GuiStartup.HomeFolder.Should().Be(AppContext.BaseDirectory);
        }
    }
}
