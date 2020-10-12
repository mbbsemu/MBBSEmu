using MBBSEmu.DependencyInjection;
using MBBSEmu.HostProcess;
using MBBSEmu.Module;
using MBBSEmu.Resources;
using MBBSEmu.Session;
using System.Collections.Generic;
using System.IO;
using System;

namespace MBBSEmu.Tests.Integration
{
    public class MBBSEmuIntegrationTestBase : IDisposable
    {
        private static readonly Random RANDOM = new Random(Guid.NewGuid().GetHashCode());
        private readonly string[] _moduleFiles = {"MBBSEMU.DAT", "MBBSEMU.DLL", "MBBSEMU.MCV", "MBBSEMU.MDF", "MBBSEMU.MSG"};

        protected readonly string _modulePath = Path.Join(Path.GetTempPath(), $"mbbsemu{RANDOM.Next()}");
        protected TestSession _session;

        public MBBSEmuIntegrationTestBase()
        {
            Directory.CreateDirectory(_modulePath);
        }

        public void Dispose()
        {
            Directory.Delete(_modulePath,  recursive: true);
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
            List<string> lines = new List<string>();
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
            ServiceResolver serviceResolver = new ServiceResolver(ServiceResolver.GetTestDefaults());

            //Setup Generic Database
            var resourceManager = serviceResolver.GetService<IResourceManager>();
            File.WriteAllBytes($"BBSGEN.EMU", resourceManager.GetResource("MBBSEmu.Assets.BBSGEN.EMU").ToArray());
            File.WriteAllBytes($"BBSUSR.EMU", resourceManager.GetResource("MBBSEmu.Assets.BBSUSR.EMU").ToArray());

            CopyModuleToTempPath(ResourceManager.GetTestResourceManager());

            //Setup and Run Host with only the MBBSEMU module
            var host = serviceResolver.GetService<IMbbsHost>();
            var moduleConfigurations = new List<ModuleConfiguration>
            {
                new ModuleConfiguration {ModuleIdentifier = "MBBSEMU", ModulePath = _modulePath, MenuOptionKey = null}
            };

            host.Start(moduleConfigurations);

            _session = new TestSession(host);
            host.AddSession(_session);

            testLogic(_session, host);

            host.Stop();

            host.WaitForShutdown();
        }
    }
}
