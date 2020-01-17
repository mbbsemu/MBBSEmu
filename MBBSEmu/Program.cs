using MBBSEmu.Database.Repositories.Account;
using MBBSEmu.Database.Repositories.AccountKey;
using MBBSEmu.DependencyInjection;
using MBBSEmu.HostProcess;
using MBBSEmu.Module;
using MBBSEmu.Telnet;
using NLog;
using System;
using MBBSEmu.Reports;

namespace MBBSEmu
{
    class Program
    {
        private static ILogger _logger = ServiceResolver.GetService<ILogger>();

        static void Main(string[] args)
        {
            var sInputModule = string.Empty;
            var sInputPath = string.Empty;
            var bApiReport = false;
            for (var i = 0; i < args.Length; i++)
            {


                switch (args[i].ToUpper())
                {
                    case "-DBRESET":
                    {
                        _logger.Info("Resetting Database...");
                        var acct = ServiceResolver.GetService<IAccountRepository>();
                        if (acct.TableExists())
                            acct.DropTable();

                        acct.CreateTable();
                        var sysopUserId = acct.InsertAccount("sysop", "sysop", "eric@nusbaum.me");
                        
                        var keys = ServiceResolver.GetService<IAccountKeyRepository>();

                        if(keys.TableExists())
                            keys.DropTable();
                        
                        keys.CreateTable();
                        keys.InsertAccountKey(sysopUserId, "DEMO");
                        keys.InsertAccountKey(sysopUserId, "NORMAL");
                        keys.InsertAccountKey(sysopUserId, "SUPER");
                        keys.InsertAccountKey(sysopUserId, "SYSOP");
                        _logger.Info("Database Reset!");
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
                        Console.WriteLine("-I <file> -- Input File to DisassembleSegment");
                        Console.WriteLine("-O <file> -- Output File for Disassembly (Default ConsoleUI)");
                        Console.WriteLine("-MINIMAL -- Minimal Disassembler Output");
                        Console.WriteLine(
                            "-ANALYSIS -- Additional Analysis on Imported Functions (if available)");
                        Console.WriteLine(
                            "-STRINGS -- Output all strings found in DATA segments at end of Disassembly");
                        return;
                    default:
                        Console.WriteLine($"Unknown Command Line Argument: {args[i]}");
                        return;
                }
            }

            if (string.IsNullOrEmpty(sInputModule))
            {
                Console.WriteLine("No Module Specified");
                return;
            }

            var module = new MbbsModule(sInputModule, sInputPath);
            if (bApiReport)
            {
                var apiReport = new ApiReport(module);
                apiReport.GenerateReport();
                return;
            }

            var host = ServiceResolver.GetService<IMbbsHost>();
            host.Start();

            



            host.AddModule(module);

            var server = ServiceResolver.GetService<ITelnetServer>();

            server.Start();

            Console.ReadKey();
            //host.Init();
            //host.Run("sttrou");
            //host.Run("stsrou");
            //var textUI = new TextGUI();
            //textUI.Run();
        }
    }
}
