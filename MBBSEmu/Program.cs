using MBBSEmu.Database.Repositories.Account;
using MBBSEmu.Database.Repositories.AccountKey;
using MBBSEmu.DependencyInjection;
using MBBSEmu.HostProcess;
using MBBSEmu.Module;
using MBBSEmu.Reports;
using MBBSEmu.Resources;
using MBBSEmu.Telnet;
using Microsoft.Extensions.Configuration;
using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MBBSEmu
{
    class Program
    {
        private static readonly ILogger _logger = ServiceResolver.GetService<ILogger>();

        static void Main(string[] args)
        {
            var config = ServiceResolver.GetService<IConfigurationRoot>();
            var sInputModule = string.Empty;
            var sInputPath = string.Empty;
            var bApiReport = false;

            if (args.Length == 0)
                args = new[] {"-?"};

            for (var i = 0; i < args.Length; i++)
            {
                switch (args[i].ToUpper())
                {
                    case "-DBRESET":
                    {
                        DatabaseReset();
                        break;
                    }
                    case "-APIREPORT":
                        bApiReport = true;
                        break;
                    case "-M":
                        sInputModule = args[i + 1];
                        i++;
                        break;
                    case "-P":
                        sInputPath = args[i + 1];
                        i++;
                        break;
                    case "-?":
                        Console.WriteLine(ServiceResolver.GetService<IResourceManager>().GetString("MBBSEmu.Assets.commandLineHelp.txt"));
                        return;
                    default:
                        Console.WriteLine($"Unknown Command Line Argument: {args[i]}");
                        return;
                }
            }

            if (string.IsNullOrEmpty(sInputModule) && !config.GetSection("Modules").GetChildren().Any())
            {
                Console.WriteLine("No Module Specified");
                return;
            }

            if (!File.Exists("BBSGEN.DAT"))
            {
                _logger.Warn($"Unable to find MajorBBS/WG Generic User Database, creating new copy of BBSGEN.VIR to BBSGEN.DAT");
                File.Copy("BBSGEN.VIR", "BBSGEN.DAT");
            }

            var modules = new List<MbbsModule>();

            if (!string.IsNullOrEmpty(sInputModule))
            {
                //Load Command Line
                modules.Add(new MbbsModule(sInputModule, sInputPath));
            }
            else
            {
                //Load Config File
                foreach (var m in config.GetSection("Modules").GetChildren())
                {
                    _logger.Info($"Loading {m["Identifier"]}");
                    modules.Add(new MbbsModule(m["Identifier"], m["Path"]));
                }
            }

            if (bApiReport)
            {
                foreach (var m in modules)
                {
                    var apiReport = new ApiReport(m);
                    apiReport.GenerateReport();
                }
                return;
            }

            //Database Sanity Checks
            var databaseFile = ServiceResolver.GetService<IConfigurationRoot>()["Database.File"];
            if (string.IsNullOrEmpty(databaseFile))
            {
                _logger.Fatal($"Please set a valid database filename (eg: mbbsemu.db) in the appsettings.json file before running MBBSEmu");
                return;
            }

            if (!File.Exists(databaseFile))
            {
                _logger.Warn($"SQLite Database File {databaseFile} missing, performing Database Reset to perform initial configuration");
                DatabaseReset();
            }

            var host = ServiceResolver.GetService<IMbbsHost>();
            host.Start();
            foreach(var m in modules)
                host.AddModule(m);

            var server = ServiceResolver.GetService<ITelnetServer>();

            server.Start();
        }

        private static void DatabaseReset()
        {
            _logger.Info("Resetting Database...");
            var acct = ServiceResolver.GetService<IAccountRepository>();
            if (acct.TableExists())
                acct.DropTable();

            acct.CreateTable();
            var sysopUserId = acct.InsertAccount("sysop", "sysop", "eric@nusbaum.me");
            var guestUserId = acct.InsertAccount("guest", "guest", "guest@nusbaum.me");

            var keys = ServiceResolver.GetService<IAccountKeyRepository>();

            if (keys.TableExists())
                keys.DropTable();

            keys.CreateTable();

            keys.InsertAccountKey(sysopUserId, "DEMO");
            keys.InsertAccountKey(sysopUserId, "NORMAL");
            keys.InsertAccountKey(sysopUserId, "SUPER");
            keys.InsertAccountKey(sysopUserId, "SYSOP");

            keys.InsertAccountKey(guestUserId, "DEMO");
            keys.InsertAccountKey(guestUserId, "NORMAL");

            _logger.Info("Database Reset!");
        }
    }
}
