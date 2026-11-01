using System;
using System.IO;
using MBBSEmu.IO;
using MBBSEmu.Logging;
using MBBSEmu.Module;
using Xunit;

namespace MBBSEmu.Tests.Module
{
    public class ModuleLoadPlan_Tests : IDisposable
    {
        private readonly string _directory;
        private readonly IFileUtility _files = FileUtility.CreateForTest();
        private readonly IMessageLogger _logger = new MessageLogger();

        public ModuleLoadPlan_Tests()
        {
            _directory = Path.Combine(Path.GetTempPath(), "mbbsemu-plan-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
            File.WriteAllText(Path.Combine(_directory, "WCCMMUD.MDF"), """
                Module Name: MajorMUD
                Developer: Metropolis Inc.
                Unconditional
                DLLs: WCCMMUD
                MSGs: WCCMMUD
                I Need: GALEML BBSFSD
                Needs Me: WCCMMPLS
                """);
            File.WriteAllText(Path.Combine(_directory, "WCCMMPLS.MDF"), """
                Module Name: MajorMUD Plus
                Developer: Metropolis, Inc.
                Unconditional
                Internal
                Requires: WCCMMUD
                DLLs: WCCMMPLS
                MSGs: WCCMMPLS
                I Need: WCCMMUD
                Needs Me: WCCMMUD
                """);
        }

        public void Dispose()
        {
            if (Directory.Exists(_directory))
                Directory.Delete(_directory, recursive: true);
        }

        [Fact]
        public void Mdf_ParsesInternalAndNeeds()
        {
            var plus = new MdfFile(Path.Combine(_directory, "WCCMMPLS.MDF"));
            Assert.True(plus.IsInternal);
            Assert.Contains("WCCMMUD", plus.INeed);
            Assert.Contains("WCCMMUD", plus.Requires);
            Assert.Contains("WCCMMUD", plus.NeedsMe);

            var mud = new MdfFile(Path.Combine(_directory, "WCCMMUD.MDF"));
            Assert.False(mud.IsInternal);
            Assert.Contains("WCCMMPLS", mud.NeedsMe);
            Assert.Contains("GALEML", mud.INeed);
        }

        [Fact]
        public void BothConfigured_AttachesPlusToTheLoadedMudModule()
        {
            var plan = ModuleLoadPlan.Build(new[]
            {
                Config("WCCMMUD"),
                Config("WCCMMPLS")
            }, _files, _logger);

            var host = Assert.Single(plan.HostModules);
            Assert.Equal("WCCMMUD", host.ModuleIdentifier);

            var companion = Assert.Single(plan.Companions);
            Assert.Equal("WCCMMPLS", companion.CompanionIdentifier);
            Assert.Equal("WCCMMUD", companion.ParentIdentifier);
        }

        [Fact]
        public void MudOnly_DoesNotLoadAnInternalModuleTheOperatorLeftOut()
        {
            var plan = ModuleLoadPlan.Build(new[] { Config("WCCMMUD") }, _files, _logger);

            Assert.Equal("WCCMMUD", Assert.Single(plan.HostModules).ModuleIdentifier);
            Assert.Empty(plan.Companions);
        }

        [Fact]
        public void DisabledInternalModule_IsNotAttached()
        {
            var plus = Config("WCCMMPLS");
            plus.ModuleEnabled = false;

            var plan = ModuleLoadPlan.Build(new[] { Config("WCCMMUD"), plus }, _files, _logger);

            Assert.Equal("WCCMMUD", Assert.Single(plan.HostModules).ModuleIdentifier);
            Assert.Empty(plan.Companions);
        }

        [Fact]
        public void NeedsMeDoesNotAbsorbANormalModule()
        {
            File.WriteAllText(Path.Combine(_directory, "ADDON.MDF"), """
                Module Name: Addon
                DLLs: ADDON
                Needs Me: WCCMMUD
                """);

            var plan = ModuleLoadPlan.Build(new[]
            {
                Config("WCCMMUD"),
                Config("ADDON")
            }, _files, _logger);

            Assert.Equal(2, plan.HostModules.Count);
            Assert.Equal("WCCMMUD", plan.HostModules[0].ModuleIdentifier);
            Assert.Equal("ADDON", plan.HostModules[1].ModuleIdentifier);
            Assert.Empty(plan.Companions);
        }

        [Fact]
        public void PlusOnly_LoadsTheRequiredMudModuleFirst()
        {
            var plus = Config("WCCMMPLS");
            plus.MenuOptionKey = "M";

            var plan = ModuleLoadPlan.Build(new[] { plus }, _files, _logger);

            var host = Assert.Single(plan.HostModules);
            Assert.Equal("WCCMMUD", host.ModuleIdentifier);
            Assert.Equal("M", host.MenuOptionKey);
            var companion = Assert.Single(plan.Companions);
            Assert.Equal("WCCMMPLS", companion.CompanionIdentifier);
            Assert.Equal("WCCMMUD", companion.ParentIdentifier);
        }

        [Fact]
        public void TwoHostModules_StaySeparate()
        {
            File.WriteAllText(Path.Combine(_directory, "OTHER.MDF"), """
                Module Name: Other
                DLLs: OTHER
                """);

            var plan = ModuleLoadPlan.Build(new[]
            {
                Config("WCCMMUD"),
                Config("OTHER")
            }, _files, _logger);

            Assert.Equal(2, plan.HostModules.Count);
            Assert.Equal("WCCMMUD", plan.HostModules[0].ModuleIdentifier);
            Assert.Equal("OTHER", plan.HostModules[1].ModuleIdentifier);
            Assert.Empty(plan.Companions);
        }

        private ModuleConfiguration Config(string identifier) => new()
        {
            ModuleIdentifier = identifier,
            ModulePath = _directory,
            ModuleEnabled = true
        };
    }
}
