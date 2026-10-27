using FluentAssertions;
using MBBSEmu.HostProcess;
using MBBSEmu.Module;
using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace MBBSEmu.Tests.Integration
{
    /// <summary>
    ///     Loading a module into a host that is already running, as Modules > Load Module does
    /// </summary>
    public class RuntimeModuleLoad_Tests : MBBSEmuIntegrationTestBase
    {
        private static readonly TimeSpan LoadTimeout = TimeSpan.FromSeconds(30);

        private ModuleConfiguration Config(string identifier, string path = null, string menuOptionKey = null) => new()
        {
            ModuleIdentifier = identifier,
            ModulePath = path ?? _modulePath,
            MenuOptionKey = menuOptionKey,
            ModuleEnabled = true
        };

        private static ModuleLoadResult Load(IMbbsHost host, ModuleConfiguration config)
        {
            var load = host.LoadModule(config);
            load.Wait(LoadTimeout).Should().BeTrue("the load should finish");
            return load.Result;
        }

        private void WithRunningHost(Action<IMbbsHost> test)
        {
            var host = StartHostWithoutModules();
            try
            {
                test(host);
            }
            finally
            {
                host.Stop();
                host.WaitForShutdown();
            }
        }

        [Fact]
        public void LoadModule_AfterStart_LoadsWithFirstFreeKey()
        {
            WithRunningHost(host =>
            {
                var result = Load(host, Config("MBBSEMU"));

                result.Status.Should().Be(EnumModuleLoadStatus.Loaded);
                result.ModuleIdentifier.Should().Be("MBBSEMU");
                result.MenuOptionKey.Should().Be("A");
                result.ModuleName.Should().Be("MBBSEmu");
                result.Detail.Should().BeNull();
                host.GetModule("MBBSEMU").ModuleConfig.MenuOptionKey.Should().Be("A");
            });
        }

        [Fact]
        public void LoadModule_SameModuleDifferentCase_ReportsAlreadyLoaded()
        {
            WithRunningHost(host =>
            {
                Load(host, Config("MBBSEMU")).Status.Should().Be(EnumModuleLoadStatus.Loaded);

                var result = Load(host, Config("mbbsemu"));

                result.Status.Should().Be(EnumModuleLoadStatus.AlreadyLoaded);
                result.MenuOptionKey.Should().Be("A");
                result.ModuleName.Should().Be("MBBSEmu");
            });
        }

        [Fact]
        public void LoadModule_MissingFolder_ReportsNotFound()
        {
            WithRunningHost(host =>
            {
                var missing = Path.Combine(_modulePath, "missing");

                var result = Load(host, Config("MBBSEMU", missing));

                result.Status.Should().Be(EnumModuleLoadStatus.NotFound);
                result.Detail.Should().Be(missing);
            });
        }

        [Fact]
        public void LoadModule_MissingMdf_ReportsNotFound()
        {
            WithRunningHost(host =>
            {
                var result = Load(host, Config("NOPE"));

                result.Status.Should().Be(EnumModuleLoadStatus.NotFound);
                result.Detail.Should().EndWith("NOPE.MDF");
            });
        }

        [Fact]
        public void LoadModule_BrokenModule_ReportsFailedAndLeavesNoTrace()
        {
            WithRunningHost(host =>
            {
                File.WriteAllText(Path.Combine(_modulePath, "BROKEN.MDF"),
                    File.ReadAllText(Path.Combine(_modulePath, "MBBSEMU.MDF")).Replace("DLLs: MBBSEMU", "DLLs: BROKEN"));
                File.WriteAllBytes(Path.Combine(_modulePath, "BROKEN.DLL"), new byte[64]);

                var broken = Load(host, Config("BROKEN"));
                var good = Load(host, Config("MBBSEMU"));

                broken.Status.Should().Be(EnumModuleLoadStatus.Failed);
                broken.Detail.Should().NotBeNullOrEmpty();
                good.Status.Should().Be(EnumModuleLoadStatus.Loaded);
                good.MenuOptionKey.Should().Be("A");
                host.GetModule("MBBSEMU").MainModuleDll.StateCode.Should().Be(10);
            });
        }

        [Fact]
        public void LoadModule_TakenKey_IsReplacedWithNextFreeKey()
        {
            WithRunningHost(host =>
            {
                var result = Load(host, Config("MBBSEMU", menuOptionKey: "X"));

                result.Status.Should().Be(EnumModuleLoadStatus.Loaded);
                result.MenuOptionKey.Should().Be("A");
            });
        }

        [Fact]
        public void LoadModule_BeforeStart_FailsImmediately()
        {
            var host = _serviceResolver.GetService<IMbbsHost>();

            var result = Load(host, Config("MBBSEMU"));

            result.Status.Should().Be(EnumModuleLoadStatus.Failed);
            result.Detail.Should().Be("MBBSEmu isn't running");
        }

        [Fact]
        public void LoadModule_AfterStop_FailsImmediately()
        {
            var host = StartHostWithoutModules();
            host.Stop();
            host.WaitForShutdown();

            var load = host.LoadModule(Config("MBBSEMU"));

            load.Wait(TimeSpan.FromSeconds(5)).Should().BeTrue("a stopped host can't take the request");
            load.Result.Status.Should().Be(EnumModuleLoadStatus.Failed);
        }
    }
}
