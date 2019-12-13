using MBBSEmu.Module;
using System;
using MBBSEmu.HostProcess;
using MBBSEmu.Telnet;

namespace MBBSEmu
{
    class Program
    {
        static void Main(string[] args)
        {
            var sInputModule = string.Empty;
            var sInputPath = string.Empty;
            for (var i = 0; i < args.Length; i++)
            {
                

                switch (args[i].ToUpper())
                {
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

            var host = new MbbsHost();
            host.AddModule(module);
            //host.Init(module.ModuleIdentifier);
            var server = new TelnetServer(host);
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
