using MBBSEmu.DependencyInjection;
using MBBSEmu.HostProcess;
using MBBSEmu.IO;
using MBBSEmu.Memory;
using MBBSEmu.Module;
using MBBSEmu.Resources;
using MBBSEmu.Session;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class Echo_Tests : IDisposable
    {
        private readonly string _modulePath = Path.Join(Path.GetTempPath(), "mbbsemu");
        private TestSession _session;

        public Echo_Tests()
        {
            Directory.CreateDirectory(_modulePath);
            Directory.SetCurrentDirectory(_modulePath);
        }

        public void Dispose()
        {
            Directory.Delete(_modulePath, /* recursive= */ true);
        }

        private string[] _moduleFiles = {"MBBSEMU.DLL", "MBBSEMU.MCV", "MBBSEMU.MDF", "MBBSEMU.MSG"};

        private void CopyModuleToTempPath(IResourceManager resourceManager)
        {
            foreach (var file in _moduleFiles)
            {
                File.WriteAllBytes(file, resourceManager.GetResource($"MBBSEmu.Assets.{file}").ToArray());
            }
        }

        private string WaitUntil(char endingCharacter, string message)
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

        [Theory]
        [InlineData("x\r\n", "Hahahah")]
        public void test(string clientToSend, string expected)
        {
            var list = new List<KeyValuePair<string, string>>();
            list.Add(new KeyValuePair<string, string>("BBS.Title", "Test"));
            list.Add(new KeyValuePair<string, string>("GSBL.Activation", "123456789"));
            list.Add(new KeyValuePair<string, string>("Telnet.Enabled", "False"));
            list.Add(new KeyValuePair<string, string>("Rlogin.Enabled", "False"));
            list.Add(new KeyValuePair<string, string>("Database.File", "mbbsemu.db"));

            ServiceResolver.Create(list);

            //Setup Generic Database
            var resourceManager = ServiceResolver.GetService<IResourceManager>();
            File.WriteAllBytes($"BBSGEN.DAT", resourceManager.GetResource("MBBSEmu.Assets.BBSGEN.VIR").ToArray());

            CopyModuleToTempPath(resourceManager);

            var modules = new List<MbbsModule>();
            modules.Add(new MbbsModule(ServiceResolver.GetService<IFileUtility>(), "MBBSEMU", _modulePath));

            //Setup and Run Host
            var host = ServiceResolver.GetService<IMbbsHost>();
            foreach (var m in modules)
                host.AddModule(m);

            host.Start();

            _session = new TestSession(host);
            host.AddSession(_session);

            WaitUntil(':', "Make your selection");
            _session.SendToModule(Encoding.ASCII.GetBytes("E\r\n"));
            WaitUntil(':', "Type something");
            _session.SendToModule(Encoding.ASCII.GetBytes("This is really cool!\r\n"));
            WaitUntil(':', "You entered");
            WaitUntil('\n', "This is really cool!");

            host.Stop();

            host.WaitForShutdown();
        }
    }
}
