using FluentAssertions;
using MBBSEmu.Module;
using MBBSEmu.UI.Modules;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Xunit;

namespace MBBSEmu.Tests.UI.Modules
{
    public class ModuleLoader_Tests : IDisposable
    {
        private const string Hint = "HINT";

        private readonly string _folder = Path.Combine(Path.GetTempPath(), $"mbbsemu-moduleloader-{Guid.NewGuid():N}");
        private readonly string _modulesFile;
        private readonly string _moduleFolder;
        private readonly string _pickedMdf;
        private readonly List<ModuleConfiguration> _hostRequests = new();
        private Func<ModuleConfiguration, ModuleLoadResult> _hostAnswer;

        public ModuleLoader_Tests()
        {
            Directory.CreateDirectory(_folder);
            _modulesFile = Path.Combine(_folder, ModuleListFile.DefaultFileName);
            _moduleFolder = Path.Combine(_folder, "MJWMUT");
            _pickedMdf = Path.Combine(_moduleFolder, "MJWMUT.MDF");
            _hostAnswer = _ => Result(EnumModuleLoadStatus.Loaded);
        }

        public void Dispose() => Directory.Delete(_folder, recursive: true);

        private static ModuleLoadResult Result(EnumModuleLoadStatus status, string detail = null) =>
            new(status, "MJWMUT", "Mutants!", "A", detail);

        private ModuleLoader Loader(ModuleSaving saving = null) => new(
            config =>
            {
                _hostRequests.Add(config);
                return Task.FromResult(_hostAnswer(config));
            },
            saving ?? ModuleSaving.To(_modulesFile),
            _folder,
            Hint);

        [Fact]
        public async Task LoadAsync_Loaded_SavesAndExplainsHowToPlay()
        {
            var loader = Loader();

            var report = await loader.LoadAsync(_pickedMdf);

            var request = _hostRequests.Should().ContainSingle().Subject;
            request.ModuleIdentifier.Should().Be("MJWMUT");
            request.ModulePath.Should().Be(_moduleFolder);
            request.MenuOptionKey.Should().BeNull();
            request.ModuleEnabled.Should().BeTrue();
            report.Should().Be(new ModuleLoadReport(false, ModuleLoadText.LoadedTitle,
                ModuleLoadText.Loaded("Mutants!", "A") + "\n\n" + Hint));
            var saved = JsonNode.Parse(File.ReadAllText(_modulesFile))!["Modules"]!.AsArray().Should().ContainSingle().Subject!;
            saved["Identifier"]!.GetValue<string>().Should().Be("MJWMUT");
            saved["Path"]!.GetValue<string>().Should().Be(_moduleFolder);
            saved["MenuOptionKey"]!.GetValue<string>().Should().Be("A");
            loader.LoadedCount.Should().Be(1);
            loader.StartFolder.Should().Be(_moduleFolder);
        }

        [Fact]
        public async Task LoadAsync_SessionOnly_WritesNothing()
        {
            var loader = Loader(ModuleSaving.SessionOnly(ModuleLoadText.StartedWithConfiguration));

            var report = await loader.LoadAsync(_pickedMdf);

            report.Should().Be(new ModuleLoadReport(false, ModuleLoadText.LoadedTitle,
                ModuleLoadText.LoadedForSession("Mutants!", "A", ModuleLoadText.StartedWithConfiguration) + "\n\n" + Hint));
            Directory.GetFileSystemEntries(_folder).Should().BeEmpty();
        }

        [Fact]
        public async Task LoadAsync_SaveFails_ReportsLoadedButNotSaved()
        {
            File.WriteAllText(_modulesFile, "{ not json");
            var loader = Loader();

            var report = await loader.LoadAsync(_pickedMdf);

            report.IsError.Should().BeFalse("the module is loaded and playable");
            report.Title.Should().Be(ModuleLoadText.LoadedTitle);
            report.Message.Should().StartWith("Mutants! is loaded, but it couldn't be saved").And.EndWith("\n\n" + Hint);
            File.ReadAllText(_modulesFile).Should().Be("{ not json");
            loader.LoadedCount.Should().Be(1);
        }

        [Fact]
        public async Task LoadAsync_NotAnMdf_DoesNotAskTheHost()
        {
            var report = await Loader().LoadAsync(Path.Combine(_moduleFolder, "MJWMUT.DLL"));

            report.Should().Be(new ModuleLoadReport(true, ModuleLoadText.FailedTitle, ModuleLoadText.NotAnMdf("MJWMUT.DLL")));
            _hostRequests.Should().BeEmpty();
        }

        [Fact]
        public async Task LoadAsync_LowercaseMdf_IsAccepted()
        {
            await Loader().LoadAsync(Path.Combine(_moduleFolder, "mjwmut.mdf"));

            _hostRequests.Should().ContainSingle().Which.ModuleIdentifier.Should().Be("mjwmut");
        }

        [Fact]
        public async Task LoadAsync_Failed_ReportsErrorAndSavesNothing()
        {
            _hostAnswer = _ => Result(EnumModuleLoadStatus.Failed, "bad dll");
            var loader = Loader();

            var report = await loader.LoadAsync(_pickedMdf);

            report.Should().Be(new ModuleLoadReport(true, ModuleLoadText.FailedTitle, ModuleLoadText.Failed("MJWMUT", "bad dll")));
            File.Exists(_modulesFile).Should().BeFalse();
            loader.LoadedCount.Should().Be(0);
        }

        [Fact]
        public async Task LoadAsync_AlreadyLoaded_SavesNothing()
        {
            _hostAnswer = _ => Result(EnumModuleLoadStatus.AlreadyLoaded);

            var report = await Loader().LoadAsync(_pickedMdf);

            report.Should().Be(new ModuleLoadReport(false, ModuleLoadText.AlreadyLoadedTitle, ModuleLoadText.AlreadyLoaded("Mutants!")));
            File.Exists(_modulesFile).Should().BeFalse();
        }

        [Fact]
        public async Task LoadAsync_HostThrows_ReportsError()
        {
            _hostAnswer = _ => throw new InvalidOperationException("boom");

            var report = await Loader().LoadAsync(_pickedMdf);

            report.Should().Be(new ModuleLoadReport(true, ModuleLoadText.FailedTitle, ModuleLoadText.Failed("MJWMUT", "boom")));
        }

        [Fact]
        public async Task LoadSavedAsync_Loaded_ReturnsNullAndCounts()
        {
            var loader = Loader();
            var saved = new ModuleConfiguration { ModuleIdentifier = "MJWMUT", ModulePath = _moduleFolder, MenuOptionKey = "M" };

            var skipped = await loader.LoadSavedAsync(saved);

            skipped.Should().BeNull();
            _hostRequests.Should().ContainSingle().Which.Should().BeSameAs(saved);
            loader.LoadedCount.Should().Be(1);
            File.Exists(_modulesFile).Should().BeFalse("it's already in the saved list");
        }

        /// <summary>
        ///     Modules from -C, -M or -P are loaded by the host at startup, not through the loader, but still count:
        ///     otherwise the console would say no modules are loaded while they're running
        /// </summary>
        [Fact]
        public async Task AddStartupModules_CountsModulesLoadedWithoutTheLoader()
        {
            var loader = Loader(ModuleSaving.SessionOnly(ModuleLoadText.StartedWithConfiguration));

            loader.AddStartupModules(2);
            await loader.LoadAsync(_pickedMdf);

            loader.LoadedCount.Should().Be(3);
        }

        [Fact]
        public async Task LoadSavedAsync_NotFound_ReturnsSkipLine()
        {
            _hostAnswer = _ => Result(EnumModuleLoadStatus.NotFound, "/gone/MJWMUT");
            var loader = Loader();

            var skipped = await loader.LoadSavedAsync(new ModuleConfiguration { ModuleIdentifier = "MJWMUT", ModulePath = "/gone/MJWMUT" });

            skipped.Should().Be(ModuleLoadText.SkippedNotFound("MJWMUT", "/gone/MJWMUT"));
            loader.LoadedCount.Should().Be(0);
            File.Exists(_modulesFile).Should().BeFalse();
        }
    }
}
