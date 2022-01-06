using MBBSEmu.Database.Repositories.Account;
using MBBSEmu.Database.Repositories.AccountKey;
using MBBSEmu.Database.Session;
using MBBSEmu.DependencyInjection;
using MBBSEmu.HostProcess;
using MBBSEmu.Module;
using MBBSEmu.Resources;
using MBBSEmu.Session;
using MBBSEmu.TextVariables;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.IO;

namespace MBBSEmu.Tests.Integration
{
    public class MBBSEmuIntegrationTestBase : TestBase, IDisposable
    {
        private readonly string[] _moduleFiles = { "MBBSEMU.DAT", "MBBSEMU.DLL", "MBBSEMU.MCV", "MBBSEMU.MDF", "MBBSEMU.MSG" };

        protected readonly string _modulePath;
        protected TestSession _session;

        private protected readonly ServiceResolver _serviceResolver;

        public MBBSEmuIntegrationTestBase()
        {
            _modulePath = GetModulePath();

            _serviceResolver = new ServiceResolver(SessionBuilder.ForTest($"MBBSDb_{RANDOM.Next()}"));

            _serviceResolver.GetService<IAccountRepository>().Reset("sysop");
            _serviceResolver.GetService<IAccountKeyRepository>().Reset();
            Directory.CreateDirectory(_modulePath);
        }

        public void Dispose()
        {
            _serviceResolver.Dispose();

            SqliteConnection.ClearAllPools();

            Directory.Delete(_modulePath, recursive: true);
        }

        private void CopyModuleToTempPath(IResourceManager resourceManager)
        {
            foreach (var file in _moduleFiles)
            {
                File.WriteAllBytes(Path.Combine(_modulePath, file), resourceManager.GetResource($"MBBSEmu.Tests.Assets.{file}").ToArray());
            }
        }

        /// <summary>
        ///     Reads data from MBBSEMU until endingCharacter is received, and also verifies the
        ///     last data read contains message.
        /// <returns>All the lines delineated by endingCharacter until message is found</returns>
        /// </summary>
        protected List<string> WaitUntil(char endingCharacter, string message)
        {
            var lines = new List<string>();
            while (true)
            {
                var line = _session.GetLine(endingCharacter, TimeSpan.FromSeconds(2));
                lines.Add(line);

                if (line.Contains(message))
                {
                    return lines;
                }
            }
        }

        protected delegate void TestLogic(TestSession testSession, IMbbsHost host);

        protected void ExecuteTest(TestLogic testLogic)
        {
            //Setup Generic Database
            var resourceManager = _serviceResolver.GetService<IResourceManager>();
            File.WriteAllBytes(Path.Combine(_modulePath, "BBSGEN.DB"), resourceManager.GetResource("MBBSEmu.Assets.BBSGEN.DB").ToArray());
            File.WriteAllBytes(Path.Combine(_modulePath, "BBSUSR.DB"), resourceManager.GetResource("MBBSEmu.Assets.BBSUSR.DB").ToArray());

            CopyModuleToTempPath(ResourceManager.GetTestResourceManager());

            //Setup and Run Host with only the MBBSEMU module
            var host = _serviceResolver.GetService<IMbbsHost>();
            var textVariableService = _serviceResolver.GetService<ITextVariableService>();
            var moduleConfigurations = new List<ModuleConfiguration>
            {
                new ModuleConfiguration {ModuleIdentifier = "MBBSEMU", ModulePath = _modulePath, MenuOptionKey = "A", ModuleEnabled = true}
            };

            host.Start(moduleConfigurations);

            _session = new TestSession(host, textVariableService);
            host.AddSession(_session);

            testLogic(_session, host);

            host.Stop();

            host.WaitForShutdown();
        }
    }
}
