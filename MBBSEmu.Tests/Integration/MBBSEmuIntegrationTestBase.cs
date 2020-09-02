using MBBSEmu.DependencyInjection;
using MBBSEmu.HostProcess;
using MBBSEmu.IO;
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
        private readonly string[] _moduleFiles = {"MBBSEMU.DLL", "MBBSEMU.MCV", "MBBSEMU.MDF", "MBBSEMU.MSG"};

        protected readonly string _modulePath = Path.Join(Path.GetTempPath(), "mbbsemu");
        protected TestSession _session;

        public MBBSEmuIntegrationTestBase()
        {
            Directory.CreateDirectory(_modulePath);
        }

        public void Dispose()
        {
            Directory.Delete(_modulePath, /* recursive= */ true);
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
        /// </summary>
        protected string WaitUntil(char endingCharacter, string message)
        {
            string line;
            while (true)
            {
                line = _session.GetLine(endingCharacter, TimeSpan.FromSeconds(2));
                if (line.Contains(message))
                {
                    return line;
                }
            }
        }

        protected delegate void TestLogic(TestSession testSession);

        protected void ExecuteTest(TestLogic testLogic)
        {
            var list = new List<KeyValuePair<string, string>>() {
                new KeyValuePair<string, string>("BBS.Title", "Test"),
                new KeyValuePair<string, string>("GSBL.Activation", "123456789"),
                new KeyValuePair<string, string>("Telnet.Enabled", "False"),
                new KeyValuePair<string, string>("Rlogin.Enabled", "False"),
                new KeyValuePair<string, string>("Database.File", "mbbsemu.db")
            };

            ServiceResolver.Create(list);

            //Setup Generic Database
            var resourceManager = ServiceResolver.GetService<IResourceManager>();
            File.WriteAllBytes($"BBSGEN.DAT", resourceManager.GetResource("MBBSEmu.Assets.BBSGEN.VIR").ToArray());

            CopyModuleToTempPath(ResourceManager.GetTestResourceManager());

            var modules = new List<MbbsModule>();
            modules.Add(new MbbsModule(ServiceResolver.GetService<IFileUtility>(), "MBBSEMU", _modulePath));

            //Setup and Run Host
            var host = ServiceResolver.GetService<IMbbsHost>();
            foreach (var m in modules)
                host.AddModule(m);

            host.Start();

            _session = new TestSession(host);
            host.AddSession(_session);

            testLogic(_session);

            host.Stop();

            host.WaitForShutdown();
        }
    }
}
