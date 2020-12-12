using MBBSEmu.Btrieve;
using MBBSEmu.Database.Repositories.Account;
using MBBSEmu.Database.Repositories.AccountKey;
using MBBSEmu.DependencyInjection;
using MBBSEmu.HostProcess;
using MBBSEmu.HostProcess.Structs;
using MBBSEmu.IO;
using MBBSEmu.Memory;
using MBBSEmu.Module;
using MBBSEmu.Resources;
using MBBSEmu.Server;
using MBBSEmu.Server.Socket;
using MBBSEmu.Session.Enums;
using MBBSEmu.Session.LocalConsole;
using Microsoft.Extensions.Configuration;
using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace MBBSEmu
{
    public class Program
    {
        public const string DefaultEmuSettingsFilename = "appsettings.json";

        private ILogger _logger;

        /// <summary>
        ///     Module Identifier specified by the -M Command Line Argument
        /// </summary>
        private string _moduleIdentifier;

        /// <summary>
        ///     Module Path specified by the -P Command Line Argument
        /// </summary>
        private string _modulePath;

        /// <summary>
        ///     Module Option Key specified by the -K Command Line Argument
        /// </summary>
        private string _menuOptionKey;

        /// <summary>
        ///     Specified if -APIREPORT Command Line Argument was passed
        /// </summary>
        private bool _doApiReport;

        /// <summary>
        ///     Specified if -C Command Line Argument want passed
        /// </summary>
        private bool _isModuleConfigFile;

        /// <summary>
        ///     Custom modules json file specified by the -C Command Line Argument
        /// </summary>
        private string _moduleConfigFileName;

        /// <summary>
        ///     Custom appsettings.json File specified by the -S Command Line Argument
        /// </summary>
        public static string _settingsFileName;

        /// <summary>
        ///     Specified if -DBRESET Command Line Argument was Passed
        /// </summary>
        private bool _doResetDatabase;

        /// <summary>
        ///     New Sysop Password specified by the -DBRESET Command Line Argument
        /// </summary>
        private string _newSysopPassword;

        /// <summary>
        ///     Specified if the -CONSOLE Command Line Argument was passed
        /// </summary>
        private bool _isConsoleSession;

        /// <summary>
        ///     Module Configuration
        /// </summary>
        private readonly List<ModuleConfiguration> _moduleConfigurations = new List<ModuleConfiguration>();

        private readonly List<IStoppable> _runningServices = new List<IStoppable>();
        private int _cancellationRequests = 0;

        private ServiceResolver _serviceResolver;

        static void Main(string[] args)
        {
            new Program().Run(args);
        }

        private void Run(string[] args) {
            try
            {
                if (args.Length == 0)
                    args = new[] { "-?" };

                for (var i = 0; i < args.Length; i++)
                {
                    switch (args[i].ToUpper())
                    {
                        case "-DBRESET":
                            {
                                _doResetDatabase = true;
                                if (i + 1 < args.Length && args[i + 1][0] != '-')
                                {
                                    _newSysopPassword = args[i + 1];
                                    i++;
                                }

                                break;
                            }
                        case "-APIREPORT":
                            _doApiReport = true;
                            break;
                        case "-M":
                            _moduleIdentifier = args[i + 1];
                            i++;
                            break;
                        case "-K":
                            _menuOptionKey = args[i + 1];
                            i++;
                            break;
                        case "-P":
                            _modulePath = args[i + 1];
                            i++;
                            break;
                        case "-?":
                            Console.WriteLine(new ResourceManager().GetString("MBBSEmu.Assets.commandLineHelp.txt"));
                            Console.WriteLine($"Version: {new ResourceManager().GetString("MBBSEmu.Assets.version.txt")}");
                            return;
                        case "-CONFIG":
                        case "-C":
                            {
                                _isModuleConfigFile = true;
                                //Is there a following argument that doesn't start with '-'
                                //If so, it's the config file name
                                if (i + 1 < args.Length && args[i + 1][0] != '-')
                                {
                                    _moduleConfigFileName = args[i + 1];

                                    if (!File.Exists(_moduleConfigFileName))
                                    {
                                        Console.Write($"Specified Module Configuration File not found: {_moduleConfigFileName}");
                                        return;
                                    }
                                    i++;
                                }
                                else
                                {
                                    Console.WriteLine("Please specify a Module Configuration File when using the -C command line option");
                                }

                                break;
                            }
                        case "-S":
                            {
                                //Is there a following argument that doesn't start with '-'
                                //If so, it's the config file name
                                if (i + 1 < args.Length && args[i + 1][0] != '-')
                                {
                                    _settingsFileName =  args[i + 1];

                                    if (!File.Exists(_settingsFileName))
                                    {

                                        Console.WriteLine($"Specified MBBSEmu settings not found: {_settingsFileName}");
                                        return;
                                    }
                                    i++;
                                }
                                else
                                {
                                    Console.WriteLine("Please specify an MBBSEmu configuration file when using the -S command line option");
                                }

                                break;
                            }
                        case "-CONSOLE":
                        {
                            _isConsoleSession = true;
                            break;
                        }
                        default:
                            Console.WriteLine($"Unknown Command Line Argument: {args[i]}");
                            return;
                    }
                }
                
                _serviceResolver = new ServiceResolver();

                _logger = _serviceResolver.GetService<ILogger>();
                var configuration = _serviceResolver.GetService<AppSettings>();
                var resourceManager = _serviceResolver.GetService<IResourceManager>();
                var globalCache = _serviceResolver.GetService<IGlobalCache>();
                var fileHandler = _serviceResolver.GetService<IFileUtility>();

                //Setup Generic Database
                if (!File.Exists($"BBSGEN.DB"))
                {
                    _logger.Warn($"Unable to find MajorBBS/WG Generic Database, creating new copy of BBSGEN.DB");
                    File.WriteAllBytes($"BBSGEN.DB", resourceManager.GetResource("MBBSEmu.Assets.BBSGEN.DB").ToArray());
                }
                globalCache.Set("GENBB-PROCESSOR", new BtrieveFileProcessor(fileHandler, Directory.GetCurrentDirectory(), "BBSGEN.DAT"));

                //Setup User Database
                if (!File.Exists($"BBSUSR.DB"))
                {
                    _logger.Warn($"Unable to find MajorBBS/WG User Database, creating new copy of BBSUSR.DB");
                    File.WriteAllBytes($"BBSUSR.DB", resourceManager.GetResource("MBBSEmu.Assets.BBSUSR.DB").ToArray());
                }
                globalCache.Set("ACCBB-PROCESSOR", new BtrieveFileProcessor(fileHandler, Directory.GetCurrentDirectory(), "BBSUSR.DAT"));

                //Database Reset
                if (_doResetDatabase)
                    DatabaseReset();

                //Database Sanity Checks
                var databaseFile = configuration.DatabaseFile;
                if (!File.Exists($"{databaseFile}"))
                {
                    _logger.Warn($"SQLite Database File {databaseFile} missing, performing Database Reset to perform initial configuration");
                    DatabaseReset();
                }

                //Load IPLocation list if enabled
                if (configuration.IPLocationAllow != null)
                {
                    using var reader = new StreamReader("IP2LOCATION-LITE-DB1.CSV");

                    while (!reader.EndOfStream)
                    {
                        var line = reader.ReadLine();
                        var values = line.Split(',');

                        if (values[2].Trim('"') == configuration.IPLocationAllow || values[2].Trim('"') == "-")
                        {
                            configuration.AllowedIPStartRange.Add(values[0].Trim('"'));
                            configuration.AllowedIPEndRange.Add(values[1].Trim('"'));
                        }
                    }
                }

                //Setup Modules
                if (!string.IsNullOrEmpty(_moduleIdentifier))
                {
                    //Load Command Line
                    _moduleConfigurations.Add(new ModuleConfiguration { ModuleIdentifier = _moduleIdentifier, ModulePath = _modulePath, MenuOptionKey = _menuOptionKey});
                }
                else if (_isModuleConfigFile)
                {
                    //Load Menu Option Keys
                    var menuOptionKeyList = "ABCDEFGHIJKLMNOPQRSTUVWXYZ123456789".ToCharArray().ToList();

                    //Load Config File
                    var moduleConfiguration = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory())
                        .AddJsonFile(_moduleConfigFileName, optional: false, reloadOnChange: true).Build();

                    foreach (var m in moduleConfiguration.GetSection("Modules").GetChildren())
                    {
                        //Check for available MenuOptionKeys
                        if (menuOptionKeyList.Count < 1)
                        {
                            _logger.Error($"Maximum module limit reached -- {m["Identifier"]} not loaded");
                            continue;
                        }
                        
                        //Check for Non Character MenuOptionKey
                        if (!string.IsNullOrEmpty(m["MenuOptionKey"]) && (!char.IsLetter(m["MenuOptionKey"][0])))
                        {
                            _logger.Error($"Invalid menu option key (NOT A-Z) for {m["Identifier"]}, module not loaded");
                            continue;
                        }

                        //Check for duplicate MenuOptionKey
                        if (!string.IsNullOrEmpty(m["MenuOptionKey"]) && _moduleConfigurations.Any(x => x.MenuOptionKey == m["MenuOptionKey"]))
                        {
                            _logger.Error($"Duplicate menu option key for {m["Identifier"]}, module not loaded");
                            continue;
                        }

                        //Check for duplicate module in moduleConfig
                        if (_moduleConfigurations.Any(x => x.ModuleIdentifier == m["Identifier"]))
                        {
                            _logger.Error($"Module {m["Identifier"]} already loaded, duplicate instance not loaded");
                            continue;
                        }

                        //If MenuOptionKey, remove from allowable list
                        if (!string.IsNullOrEmpty(m["MenuOptionKey"]))
                            menuOptionKeyList.Remove(char.Parse(m["MenuOptionKey"]));
                        
                        //Check for missing MenuOptionKey, assign, remove from allowable list
                        if (string.IsNullOrEmpty(m["MenuOptionKey"]))
                        {
                            m["MenuOptionKey"] = menuOptionKeyList[0].ToString();
                            menuOptionKeyList.RemoveAt(0);
                        }

                        //Load Modules
                        _logger.Info($"Loading {m["Identifier"]}");
                        _moduleConfigurations.Add(new ModuleConfiguration { ModuleIdentifier = m["Identifier"], ModulePath = m["Path"], MenuOptionKey = m["MenuOptionKey"]});
                    }
                }
                else
                {
                    _logger.Warn($"You must specify a module to load either via Command Line or Config File");
                    _logger.Warn($"View help documentation using -? for more information");
                    return;
                }

                //Setup and Run Host
                var host = _serviceResolver.GetService<IMbbsHost>();
                host.Start(_moduleConfigurations);

                //API Report
                if (_doApiReport)
                {
                    host.GenerateAPIReport();

                    host.Stop();
                    return;
                }

                _runningServices.Add(host);

                //Setup and Run Telnet Server
                if (configuration.TelnetEnabled)
                {
                    var telnetService = _serviceResolver.GetService<ISocketServer>();
                    telnetService.Start(EnumSessionType.Telnet, configuration.TelnetPort);

                    _logger.Info($"Telnet listening on port {configuration.TelnetPort}");

                    _runningServices.Add(telnetService);
                }

                //Setup and Run Rlogin Server
                if (configuration.RloginEnabled)
                {
                    var rloginService = _serviceResolver.GetService<ISocketServer>();
                    rloginService.Start(EnumSessionType.Rlogin, configuration.RloginPort);

                    _logger.Info($"Rlogin listening on port {configuration.RloginPort}");

                    _runningServices.Add(rloginService);

                    if (configuration.RloginPortPerModule)
                    {
                        var rloginPort = configuration.RloginPort + 1;
                        foreach (var m in _moduleConfigurations)
                        {
                            _logger.Info($"Rlogin {m.ModuleIdentifier} listening on port {rloginPort}");
                            rloginService = _serviceResolver.GetService<ISocketServer>();
                            rloginService.Start(EnumSessionType.Rlogin, rloginPort++, m.ModuleIdentifier);
                            _runningServices.Add(rloginService);
                        }
                    }
                }
                else
                {
                    _logger.Info("Rlogin Server Disabled (via appsettings.json)");
                }

                _logger.Info($"Started MBBSEmu Build #{new ResourceManager().GetString("MBBSEmu.Assets.version.txt")}");

                Console.CancelKeyPress += CancelKeyPressHandler;

                if(_isConsoleSession)
                    _ = new LocalConsoleSession(_logger, "CONSOLE", host);
            }
            catch (Exception e)
            {
                Console.WriteLine("Critical Exception has occurred:");
                Console.WriteLine(e);
                Environment.Exit(0);
            }
        }

        /// <summary>
        ///     Event Handler to handle Ctrl-C from the Console
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void CancelKeyPressHandler(object sender, ConsoleCancelEventArgs args)
        {
            // so args.Cancel is a bit strange. Cancel means to cancel the Ctrl-C processing, so
            // setting it to true keeps the app alive. We want this at first to allow the shutdown
            // routines to process naturally. If we get a 2nd (or more) Ctrl-C, then we set
            // args.Cancel to false which means the app will die a horrible death, and prevents the
            // app from being unkillable by normal means.
            args.Cancel = _cancellationRequests <= 0;

            _cancellationRequests++;

            _logger.Warn("BBS Shutting down");

            foreach (var runningService in _runningServices)
            {
                runningService.Stop();
            }
        }

        /// <summary>
        ///     Performs a Database Reset
        ///
        ///     Deletes the Accounts Table and sets up a new SYSOP and GUEST user
        /// </summary>
        private void DatabaseReset()
        {
            _logger.Info("Resetting Database...");
            

            if (string.IsNullOrEmpty(_newSysopPassword))
            {
                var bPasswordMatch = false;
                while (!bPasswordMatch)
                {
                    Console.Write("Enter New Sysop Password: ");
                    var password1 = Console.ReadLine();
                    Console.Write("Re-Enter New Sysop Password: ");
                    var password2 = Console.ReadLine();
                    if (password1 == password2)
                    {
                        bPasswordMatch = true;
                        _newSysopPassword = password1;
                    }
                    else
                    {
                        Console.WriteLine("Password mismatch, please try again.");
                    }
                }
            }

            var acct = _serviceResolver.GetService<IAccountRepository>();
            acct.Reset(_newSysopPassword);

            var keys = _serviceResolver.GetService<IAccountKeyRepository>();
            keys.Reset();

            //Insert Into BBS Account Btrieve File
            var _accountBtrieve = _serviceResolver.GetService<IGlobalCache>().Get<BtrieveFileProcessor>("ACCBB-PROCESSOR");
            _accountBtrieve.DeleteAll();
            _accountBtrieve.Insert(new UserAccount { userid = Encoding.ASCII.GetBytes("sysop"), psword = Encoding.ASCII.GetBytes("<<HASHED>>")}.Data);
            _accountBtrieve.Insert(new UserAccount { userid = Encoding.ASCII.GetBytes("guest"), psword = Encoding.ASCII.GetBytes("<<HASHED>>") }.Data);

            _logger.Info("Database Reset!");
        }
    }
}
