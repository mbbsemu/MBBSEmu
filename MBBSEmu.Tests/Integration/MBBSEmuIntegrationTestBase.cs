using MBBSEmu.Database.Repositories.Account;
using MBBSEmu.Database.Repositories.AccountKey;
using MBBSEmu.Database.Session;
using MBBSEmu.Date;
using MBBSEmu.DependencyInjection;
using MBBSEmu.HostProcess;
using MBBSEmu.Logging;
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

        public MBBSEmuIntegrationTestBase() : this(null)
        {
        }

        /// <summary>
        ///     Pass a clock (e.g. a FakeClock pinned to a golden capture's date) so
        ///     date-dependent module output stays stable across runs. Null keeps the
        ///     default SystemClock.
        /// </summary>
        private protected MBBSEmuIntegrationTestBase(IClock clock)
        {
            _modulePath = GetModulePath();
            var _logFactoryForTest = new LogFactory();
            _logFactoryForTest.AddLogger(new MessageLogger());
            _logFactoryForTest.AddLogger(new AuditLogger());

            var overrides = new List<object> { SessionBuilder.ForTest($"MBBSDb_{RANDOM.Next()}"), _logFactoryForTest };
            if (clock != null)
                overrides.Add(clock);

            _serviceResolver = new ServiceResolver(overrides.ToArray());

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
            CopyModuleToTempPath(ResourceManager.GetTestResourceManager());

            RunHost("MBBSEMU", testLogic);
        }

        /// <summary>
        ///     Runs testLogic against a real (non-embedded) module. The module's files
        ///     are copied from moduleSourcePath into the per-test temp directory so the
        ///     source install is never mutated (Btrieve .DAT files convert to .DB and
        ///     get written to during play).
        /// </summary>
        protected void ExecuteTest(string moduleIdentifier, string moduleSourcePath, TestLogic testLogic)
        {
            //Recursive: modules keep runtime data in subdirectories (e.g. RTSLORD/RTSLORD/)
            foreach (var file in Directory.GetFiles(moduleSourcePath, "*", SearchOption.AllDirectories))
            {
                //Skip SQLite caches from a prior run; they regenerate from .DAT
                if (file.EndsWith(".DB", StringComparison.OrdinalIgnoreCase))
                    continue;

                var destination = Path.Combine(_modulePath, Path.GetRelativePath(moduleSourcePath, file));
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                File.Copy(file, destination);
            }

            //Real modules may import BBS-wide Btrieve globals (GENBB/USRBB…), which
            //BtrieveSetupGlobalPointer opens from the process's current directory —
            //in production that's the BBS system dir. Seed them there for the test.
            var resourceManager = _serviceResolver.GetService<IResourceManager>();
            foreach (var systemFile in new[] { "BBSGEN.DB", "BBSUSR.DB" })
            {
                var systemFilePath = Path.Combine(Directory.GetCurrentDirectory(), systemFile);
                if (!File.Exists(systemFilePath))
                    File.WriteAllBytes(systemFilePath, resourceManager.GetResource($"MBBSEmu.Assets.{systemFile}").ToArray());
            }

            RunHost(moduleIdentifier, testLogic);
        }

        private void RunHost(string moduleIdentifier, TestLogic testLogic)
        {
            //Setup Generic Database
            var resourceManager = _serviceResolver.GetService<IResourceManager>();
            File.WriteAllBytes(Path.Combine(_modulePath, "BBSGEN.DB"), resourceManager.GetResource("MBBSEmu.Assets.BBSGEN.DB").ToArray());
            File.WriteAllBytes(Path.Combine(_modulePath, "BBSUSR.DB"), resourceManager.GetResource("MBBSEmu.Assets.BBSUSR.DB").ToArray());

            //Setup and Run Host with only the specified module
            var host = _serviceResolver.GetService<IMbbsHost>();
            var textVariableService = _serviceResolver.GetService<ITextVariableService>();
            var moduleConfigurations = new List<ModuleConfiguration>
            {
                new ModuleConfiguration {ModuleIdentifier = moduleIdentifier, ModulePath = _modulePath, MenuOptionKey = "A", ModuleEnabled = true}
            };

            host.Start(moduleConfigurations);

            _session = new TestSession(host, textVariableService, moduleIdentifier);
            host.AddSession(_session);

            testLogic(_session, host);

            host.Stop();

            host.WaitForShutdown();
        }
    }
}
