using MBBSEmu.DependencyInjection;
using MBBSEmu.IO;
using MBBSEmu.Memory;
using MBBSEmu.Module;
using MBBSEmu.Resources;
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

        [Theory]
        [InlineData("x\r\n", "Hahahah")]
        public void test(string clientToSend, string expected)
        {
            //"Rlogin.Enabled": "True",
            var list = new List<KeyValuePair<string, string>>();
            list.Add(new KeyValuePair<string, string>("BBS.Title", "Test"));
            list.Add(new KeyValuePair<string, string>("Telnet.Enabled", "False"));
            list.Add(new KeyValuePair<string, string>("Rlogin.Enabled", "False"));
            list.Add(new KeyValuePair<string, string>("Database.File", "mbbsemu.db"));

            ServiceResolver.Create(list);

            //Setup Generic Database
            var resourceManager = ServiceResolver.GetService<IResourceManager>();
            File.WriteAllBytes($"BBSGEN.DAT", resourceManager.GetResource("MBBSEmu.Assets.BBSGEN.VIR").ToArray());

            CopyModuleToTempPath(resourceManager);

            var fileUtility = ServiceResolver.GetService<IFileUtility>();

            var modules = new List<MbbsModule>();
            modules.Add(new MbbsModule(fileUtility, "MBBSEMU", _modulePath));

            /*Setup Modules
            var modules = new List<MbbsModule>();
            if (!string.IsNullOrEmpty(_moduleIdentifier))
            {
                //Load Command Line
                modules.Add(new MbbsModule(fileUtility, _moduleIdentifier, _modulePath));
            }
            else if (_isModuleConfigFile)
            {
                //Load Config File
                var moduleConfiguration = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile(_moduleConfigFileName, optional: false, reloadOnChange: true).Build();

                foreach (var m in moduleConfiguration.GetSection("Modules").GetChildren())
                {
                    _logger.Info($"Loading {m["Identifier"]}");
                    modules.Add(new MbbsModule(fileUtility, m["Identifier"], m["Path"]));
                }
            }
            else
            {
                _logger.Warn($"You must specify a module to load either via Command Line or Config File");
                _logger.Warn($"View help documentation using -? for more information");
                return;
            }

            //API Report
            if (_doApiReport)
            {
                foreach (var m in modules)
                {
                    var apiReport = new ApiReport(m);
                    apiReport.GenerateReport();
                }
                return;
            }

            //Database Sanity Checks
            var databaseFile = ServiceResolver.GetService<IConfiguration>()["Database.File"];
            if (string.IsNullOrEmpty(databaseFile))
            {
                _logger.Fatal($"Please set a valid database filename (eg: mbbsemu.db) in the appsettings.json file before running MBBSEmu");
                return;
            }
            if (!File.Exists($"{databaseFile}"))
            {
                _logger.Warn($"SQLite Database File {databaseFile} missing, performing Database Reset to perform initial configuration");
                DatabaseReset();
            }

            //Setup and Run Host
            var host = ServiceResolver.GetService<IMbbsHost>();
            foreach (var m in modules)
                host.AddModule(m);

            host.Start();

            _runningServices.Add(host);*/
        }
    }
}
