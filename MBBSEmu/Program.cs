using MBBSEmu.Btrieve;
using MBBSEmu.Converters;
using MBBSEmu.Database.Repositories.Account;
using MBBSEmu.Database.Repositories.AccountKey;
using MBBSEmu.Date;
using MBBSEmu.DependencyInjection;
using MBBSEmu.Disassembler;
using MBBSEmu.DOS;
using MBBSEmu.HostProcess;
using MBBSEmu.HostProcess.Structs;
using MBBSEmu.IO;
using MBBSEmu.Logging;
using MBBSEmu.Logging.Targets;
using MBBSEmu.Memory;
using MBBSEmu.Module;
using MBBSEmu.Resources;
using MBBSEmu.Server;
using MBBSEmu.Server.Socket;
using MBBSEmu.Session.Enums;
using MBBSEmu.Session.LocalConsole;
using MBBSEmu.TextVariables;
using MBBSEmu.UI.Main;
using MBBSEmu.UI.Setup;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace MBBSEmu
{
    public class Program
    {
        private IMessageLogger _logger;

        public const string DefaultEmuSettingsFilename = "appsettings.json";

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
        ///     Specified if -PWRESET Command Line Argument was Passed
        /// </summary>
        private bool _doResetPassword;

        /// <summary>
        ///     New Sysop Password specified by the -DBRESET Command Line Argument
        /// </summary>
        private string _newSysopPassword;

        /// <summary>
        ///     Specified if the -CONSOLE Command Line Argument was passed
        /// </summary>
        private bool _isConsoleSession;


        /// <summary>
        ///     Specified if the -CLI Command Line Argument was passed
        /// </summary>
        private bool _cliMode;

        /// <summary>
        ///     EXE File to be Executed
        /// </summary>
        private string _exeFile;

        /// <summary>
        ///     Module Configuration
        /// </summary>
        private readonly List<ModuleConfiguration> _moduleConfigurations = new();

        private readonly List<IStoppable> _runningServices = new();
        private int _cancellationRequests = 0;

        private ServiceResolver _serviceResolver;

        static void Main(string[] args)
        {
            //Setup Logging
            var factory = new LogFactory();
            factory.AddLogger(new MessageLogger(new ConsoleTarget()));
            factory.AddLogger(new AuditLogger(new ConsoleTarget()));
            new Program().Run(args);
        }

        private void Run(string[] args)
        {
            try
            {
                if (args.Length == 0)
                {
                    Console.Write("Run the MBBSEmu Setup Wizard (Y/n): ");
                    var response = Console.ReadLine()?.TrimEnd().ToUpper();
                    if (string.IsNullOrEmpty(response) || response == "Y")
                    {
                        Console.WriteLine("Starting MBBSEmu Setup Wizard...");
                        var setupWizard = new SetupView();
                        setupWizard.Setup();
                        setupWizard.Run();
                        Console.WriteLine("Setup Wizard Complete!");
                        Console.WriteLine("Restart MBBSEmu for the new settings to take effect. Use the Command Line Option -? for detailed help.");
                        return;
                    }
                    else
                    {
                        args = new[] { "-?" };
                    }
                }

                string[] programArgs = args;

                for (var i = 0; i < args.Length; i++)
                {
                    switch (args[i].ToUpper())
                    {
                        case "-EXE":
                            {
                                if (i + 1 < args.Length && args[i + 1][0] != '-')
                                {
                                    _exeFile = args[i + 1];
                                    programArgs = args.Skip(i + 2).ToArray();
                                    i = args.Length;
                                }

                                break;
                            }
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
                        case "-CLI":
                            _cliMode = true;
                            break;
                        case "-P":
                            _modulePath = args[i + 1];
                            i++;
                            break;
                        case "-?":
                            Console.WriteLine(new ResourceManager().GetString("MBBSEmu.Assets.commandLineHelp.txt"));
                            Console.WriteLine($"Version: {new ResourceManager().GetString("MBBSEmu.Assets.version.txt")}");
                            Console.WriteLine("(Press Any Key to Exit)");
                            Console.ReadKey();
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
                                    _settingsFileName = args[i + 1];

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
                                //Check to see if running Windows and earlier then Windows 8.0
                                if (OperatingSystem.IsWindows() && !OperatingSystem.IsWindowsVersionAtLeast(6, 2))
                                {
                                    throw new ArgumentException("Console not supported on versions of Windows earlier than 8.0");
                                }

                                _isConsoleSession = true;
                                break;
                            }
                        case "-PWRESET":
                        {
                            _doResetPassword = true;
                            if (i + 1 < args.Length && args[i + 1][0] != '-')
                            {
                                _newSysopPassword = args[i + 1];
                                i++;
                            }

                            break;
                        }
                        default:
                            Console.WriteLine($"Unknown Command Line Argument: {args[i]}");
                            return;
                    }
                }

                _serviceResolver = new ServiceResolver();
                _logger = new LogFactory().GetLogger<MessageLogger>();

                //EXE File Execution
                if (!string.IsNullOrEmpty(_exeFile))
                {
                    var mzFile = new MZFile(_exeFile);
                    ExeRuntime exe;
                    if (_isConsoleSession)
                        exe = new ExeRuntime(
                            mzFile,
                            _serviceResolver.GetService<IClock>(),
                            _logger,
                            _serviceResolver.GetService<IFileUtility>(),
                            Directory.GetCurrentDirectory(),
                            new LocalConsoleSession(_logger, "CONSOLE", null, null, false, false));
                    else
                        exe = new ExeRuntime(
                            mzFile,
                            _serviceResolver.GetService<IClock>(),
                            _logger,
                            _serviceResolver.GetService<IFileUtility>(),
                            Directory.GetCurrentDirectory(),
                            sessionBase: null,
                            new TextReaderStream(Console.In),
                            new TextWriterStream(Console.Out),
                            new TextWriterStream(Console.Error));
                    exe.Load(programArgs);
                    exe.Run();

                    _runningServices.Add(exe);

                    return;
                }

                var configuration = _serviceResolver.GetService<AppSettingsManager>();
                var textVariableService = _serviceResolver.GetService<ITextVariableService>();
                var resourceManager = _serviceResolver.GetService<IResourceManager>();
                var globalCache = _serviceResolver.GetService<IGlobalCache>();
                var fileHandler = _serviceResolver.GetService<IFileUtility>();

                //Setup Generic Database
                if (!File.Exists($"BBSGEN.DB"))
                {
                    _logger.Warn($"Unable to find MajorBBS/WG Generic Database, creating new copy of BBSGEN.DB");
                    File.WriteAllBytes($"BBSGEN.DB", resourceManager.GetResource("MBBSEmu.Assets.BBSGEN.DB").ToArray());
                }
                globalCache.Set("GENBB-PROCESSOR", new BtrieveFileProcessor(fileHandler, Directory.GetCurrentDirectory(), "BBSGEN.DAT", configuration.BtrieveCacheSize));

                //Setup User Database
                if (!File.Exists($"BBSUSR.DB"))
                {
                    _logger.Warn($"Unable to find MajorBBS/WG User Database, creating new copy of BBSUSR.DB");
                    File.WriteAllBytes($"BBSUSR.DB", resourceManager.GetResource("MBBSEmu.Assets.BBSUSR.DB").ToArray());
                }
                globalCache.Set("ACCBB-PROCESSOR", new BtrieveFileProcessor(fileHandler, Directory.GetCurrentDirectory(), "BBSUSR.DAT", configuration.BtrieveCacheSize));

                //Database Reset
                if (_doResetDatabase)
                    DatabaseReset();

                //Password Reset
                if (_doResetPassword)
                    PasswordReset();

                //Database Sanity Checks
                var databaseFile = configuration.DatabaseFile;
                if (!File.Exists($"{databaseFile}"))
                {
                    _logger.Warn($"SQLite Database File {databaseFile} missing, performing Database Reset to perform initial configuration");
                    DatabaseReset();
                }

                //Setup Modules
                if (!string.IsNullOrEmpty(_moduleIdentifier))
                {
                    _menuOptionKey ??= "A";

                    //Load Command Line
                    _moduleConfigurations.Add(new ModuleConfiguration { ModuleIdentifier = _moduleIdentifier, ModulePath = _modulePath, MenuOptionKey = _menuOptionKey, ModuleEnabled = true });
                }
                else if (_isModuleConfigFile)
                {
                    var menuOptionKeys = new List<string>();
                    var autoMenuOptionSeed = 1;

                    var options = new JsonSerializerOptions
                    {
                        Converters = {
                            new JsonModuleConfigurationFileConverter(), //Handles BasePath processing for Module Configuration
                            new JsonBooleanConverter(), //Allows TRUE/FALSE values to be parsed
                            new JsonStringEnumConverter(), //Allows Enums to be parsed from Strings
                            new JsonFarPtrConverter() //Allows FarPtr to be parsed from Strings
                        }
                    };

                    var moduleConfigurationFile =
                        JsonSerializer.Deserialize<ModuleConfigurationFile>(File.ReadAllText(_moduleConfigFileName), options);

                    foreach (var m in moduleConfigurationFile.Modules)
                    {
                        m.ModuleEnabled ??= true;

                        //Check for Non Character/Number in MenuOptionKey and Length of 2 or less
                        if (!string.IsNullOrEmpty(m.MenuOptionKey) && !(m.MenuOptionKey).All(char.IsLetterOrDigit))
                        {
                            _logger.Error($"Invalid menu option key character (NOT A-Z or 0-9) for {m.ModuleIdentifier}, auto assigning menu option key");
                            m.MenuOptionKey = "";
                        }

                        //Check MenuOptionKey is Length of 2 or less
                        if (!string.IsNullOrEmpty(m.MenuOptionKey) && m.MenuOptionKey.Length > 2)
                        {
                            _logger.Error($"Invalid menu option key length (MAX Length: 2) {m.ModuleIdentifier}, auto assigning menu option key");
                            m.MenuOptionKey = "";
                        }

                        //Check for duplicate MenuOptionKey
                        if (!string.IsNullOrEmpty(m.MenuOptionKey) && _moduleConfigurations.Any(x => x.MenuOptionKey == m.MenuOptionKey) && menuOptionKeys.Any(x => x.Equals(m.MenuOptionKey)))
                        {
                            _logger.Error($"Duplicate menu option key for {m.ModuleIdentifier}, auto assigning menu option key");
                            m.MenuOptionKey = "";
                        }

                        //Check for duplicate module in moduleConfig
                        if (_moduleConfigurations.Any(x => x.ModuleIdentifier == m.ModuleIdentifier))
                        {
                            _logger.Error($"Module {m.ModuleIdentifier} already loaded, duplicate instance not loaded");
                            continue;
                        }

                        //If MenuOptionKey, add to list
                        if (!string.IsNullOrEmpty(m.MenuOptionKey))
                            menuOptionKeys.Add(m.MenuOptionKey);

                        //Check for missing MenuOptionKey, assign, add to list
                        if (string.IsNullOrEmpty(m.MenuOptionKey))
                        {
                            m.MenuOptionKey = autoMenuOptionSeed.ToString();
                            autoMenuOptionSeed++;
                            menuOptionKeys.Add(m.MenuOptionKey);
                        }

                        //Load Modules
                        _logger.Info($"Loading {m.ModuleIdentifier}");
                        _moduleConfigurations.Add(m);
                    }
                }
                else
                {
                    _logger.Warn($"You must specify a module to load either via Command Line or Config File");
                    _logger.Warn($"View help documentation using -? for more information");
                    return;
                }

                //If the user specified CLI mode, don't start the UI
                if (!_cliMode && !_isConsoleSession)
                {
                    var mainMBBSEmuWindow = new MainView(_serviceResolver);

                    //Start UI in a Task as to not block this thread
                    Task.Run(() =>
                    {
                        mainMBBSEmuWindow.Setup();
                        mainMBBSEmuWindow.Run();
                    });

                    //Wait for the UI to be running before continuing
                    Console.Write("Waiting for MBBSEmu GUI to start...");
                    while (!mainMBBSEmuWindow.isRunning)
                    {
                        Thread.Sleep(500);
                        Console.Write(".");
                    }
                    Console.WriteLine();
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
                    var telnetHostIP = configuration.TelnetIPAddress;

                    telnetService.Start(EnumSessionType.Telnet, telnetHostIP, configuration.TelnetPort);
                    _logger.Info($"Telnet listening on IP {telnetHostIP} port {configuration.TelnetPort}");

                    _runningServices.Add(telnetService);
                }

                //Setup and Run Rlogin Server
                if (configuration.RloginEnabled)
                {
                    var rloginService = _serviceResolver.GetService<ISocketServer>();
                    var rloginHostIP = configuration.RloginIPAddress;

                    rloginService.Start(EnumSessionType.Rlogin, rloginHostIP, configuration.RloginPort);
                    _logger.Info($"Rlogin listening on IP {rloginHostIP} port {configuration.RloginPort}");

                    _runningServices.Add(rloginService);

                    if (configuration.RloginPortPerModule)
                    {
                        var rloginPort = configuration.RloginPort + 1;
                        foreach (var m in _moduleConfigurations)
                        {
                            _logger.Info($"Rlogin {m.ModuleIdentifier} listening on port {rloginPort}");
                            rloginService = _serviceResolver.GetService<ISocketServer>();
                            rloginService.Start(EnumSessionType.Rlogin, rloginHostIP, rloginPort++, m.ModuleIdentifier);
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

                if (_isConsoleSession)
                    _ = new LocalConsoleSession(_logger, "CONSOLE", host, textVariableService);
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

            _logger.Warn("MBBSEmu Shutting down...");

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
                _newSysopPassword = PasswordPrompt("sysop");

            var acct = _serviceResolver.GetService<IAccountRepository>();
            acct.Reset(_newSysopPassword);

            var keys = _serviceResolver.GetService<IAccountKeyRepository>();
            keys.Reset();

            //Insert Into BBS Account Btrieve File
            var _accountBtrieve = _serviceResolver.GetService<IGlobalCache>().Get<BtrieveFileProcessor>("ACCBB-PROCESSOR");
            _accountBtrieve.DeleteAll();
            _accountBtrieve.Insert(new UserAccount { userid = Encoding.ASCII.GetBytes("sysop"), psword = Encoding.ASCII.GetBytes("<<HASHED>>"), sex = (byte)'M' }.Data, LogLevel.Error);
            _accountBtrieve.Insert(new UserAccount { userid = Encoding.ASCII.GetBytes("guest"), psword = Encoding.ASCII.GetBytes("<<HASHED>>"), sex = (byte)'M' }.Data, LogLevel.Error);

            //Reset BBSGEN
            var _genbbBtrieve = _serviceResolver.GetService<IGlobalCache>().Get<BtrieveFileProcessor>("GENBB-PROCESSOR");
            _genbbBtrieve.DeleteAll();

            _logger.Info("Database Reset!");
        }

        /// <summary>
        ///     Prompts the user via the CLI to enter a new password for the specified username
        /// </summary>
        /// <param name="username"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        private string PasswordPrompt(string username)
        {
            while (true)
            {
                Console.Write($"Enter New Password for {username}: ");
                var password1 = Console.ReadLine();
                Console.Write($"Re-Enter New Password for {username}: ");
                var password2 = Console.ReadLine();

                //If they match, return the password
                if (password1 == password2)
                    return password1;

                //Otherwise infinite loop until they match
                Console.WriteLine("Password mismatch, please try again.");
            }
        }

        /// <summary>
        ///     Reset the Sysop Password to the specified value via the console
        /// </summary>
        private void PasswordReset()
        {
            //Interactive Password Reset via Console if one wasn't specified in the command line
            if (string.IsNullOrEmpty(_newSysopPassword))
                _newSysopPassword = PasswordPrompt("sysop");

            var acct = _serviceResolver.GetService<IAccountRepository>();
            var sysopAccount = acct.GetAccountByUsername("sysop");
            acct.UpdateAccountById(sysopAccount.accountId, sysopAccount.userName, _newSysopPassword, sysopAccount.email);
            _logger.Info("Sysop Password Reset!");
            _doResetPassword = false;
        }
    }
}
