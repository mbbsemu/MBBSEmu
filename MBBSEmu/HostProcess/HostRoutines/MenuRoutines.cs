using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using MBBSEmu.Btrieve;
using MBBSEmu.Btrieve.Enums;
using MBBSEmu.Database.Repositories.Account;
using MBBSEmu.Database.Repositories.AccountKey;
using MBBSEmu.Extensions;
using MBBSEmu.HostProcess.Enums;
using MBBSEmu.HostProcess.Structs;
using MBBSEmu.Memory;
using MBBSEmu.Module;
using MBBSEmu.Resources;
using MBBSEmu.Session;
using MBBSEmu.Session.Enums;

namespace MBBSEmu.HostProcess.HostRoutines
{
    /// <summary>
    ///     Menu Routines for MBBSEmu
    /// </summary>
    public class MenuRoutines : IHostRoutine
    {
        private readonly IResourceManager _resourceManager;
        private readonly IAccountRepository _accountRepository;
        private readonly IAccountKeyRepository _accountKeyRepository;
        private readonly AppSettings _configuration;
        private readonly IGlobalCache _globalCache;
        private readonly PointerDictionary<SessionBase> _channelDictionary;

        private static readonly byte[] ANSI_ERASE_DISPLAY = { 0x1B, 0x5B, 0x32, 0x4A };
        private static readonly byte[] ANSI_RESET_CURSOR = { 0x1B, 0x5B, 0x48 };

        public MenuRoutines(IResourceManager resourceManager, IAccountRepository accountRepository, AppSettings configuration, IGlobalCache globalCache, IAccountKeyRepository accountKeyRepository, PointerDictionary<SessionBase> channelDictionary)
        {
            _resourceManager = resourceManager;
            _accountRepository = accountRepository;
            _accountKeyRepository = accountKeyRepository;
            _configuration = configuration;
            _globalCache = globalCache;
            _channelDictionary = channelDictionary;
        }

        /// <summary>
        ///     Invokes Method based on the Session State
        /// </summary>
        /// <param name="session"></param>
        /// <param name="modules"></param>
        public bool ProcessSessionState(SessionBase session, Dictionary<string, MbbsModule> modules)
        {
            switch (session.SessionState)
            {
                case EnumSessionState.Unauthenticated:
                    WelcomeScreenDisplay(session);
                    break;
                case EnumSessionState.LoginUsernameDisplay:
                    LoginUsernameDisplay(session);
                    break;
                case EnumSessionState.LoginUsernameInput:
                    LoginUsernameInput(session);
                    break;
                case EnumSessionState.LoginPasswordDisplay:
                    LoginPasswordDisplay(session);
                    break;
                case EnumSessionState.LoginPasswordInput:
                    LoginPasswordInput(session);
                    break;
                case EnumSessionState.SignupPasswordConfirmDisplay:
                    SignupPasswordConfirmDisplay(session);
                    break;
                case EnumSessionState.SignupPasswordConfirmInput:
                    SignupPasswordConfirmInput(session);
                    break;
                case EnumSessionState.MainMenuDisplay:
                    MainMenuDisplay(session, modules);
                    break;
                case EnumSessionState.MainMenuInputDisplay:
                    MainMenuInputDisplay(session);
                    break;
                case EnumSessionState.MainMenuInput:
                    MainMenuInput(session, modules);
                    break;
                case EnumSessionState.ConfirmLogoffDisplay:
                    LogoffConfirmationDisplay(session);
                    break;
                case EnumSessionState.ConfirmLogoffInput:
                    LogoffConfirmationInput(session);
                    break;
                case EnumSessionState.SignupUsernameDisplay:
                    SignupUsernameDisplay(session);
                    break;
                case EnumSessionState.SignupUsernameInput:
                    SignupUsernameInput(session);
                    break;
                case EnumSessionState.SignupPasswordDisplay:
                    SignupPasswordDisplay(session);
                    break;
                case EnumSessionState.SignupPasswordInput:
                    SignupPasswordInput(session);
                    break;
                case EnumSessionState.SignupEmailDisplay:
                    SignupEmailDisplay(session);
                    break;
                case EnumSessionState.SignupEmailInput:
                    SignupEmailInput(session);
                    break;
                case EnumSessionState.SignupGenderDisplay:
                    SignupGenderDisplay(session);
                    break;
                case EnumSessionState.SignupGenderInput:
                    SignupGenderInput(session);
                    break;
                case EnumSessionState.LoggingOffDisplay:
                    LoggingOffDisplay(session);
                    break;
                default:
                    return false;
            }

            return true;
        }

        /// <summary>
        ///     Shows the Login Screen to a new User Session
        /// </summary>
        /// <param name="session"></param>
        private void WelcomeScreenDisplay(SessionBase session)
        {
            session.SendToClient(ANSI_ERASE_DISPLAY);
            session.SendToClient(ANSI_RESET_CURSOR);
            //Load File if specified in appsettings.json and display if it exists, else display default
            var ansiLoginFileName = _configuration.ANSILogin;
            session.SendToClient(
                File.Exists(ansiLoginFileName)
                    ? File.ReadAllBytes(ansiLoginFileName).ToArray()
                    : _resourceManager.GetResource("MBBSEmu.Assets.login.ans").ToArray());
            session.SendToClient(Encoding.ASCII.GetBytes("\r\n "));
            session.SessionState = EnumSessionState.LoginUsernameDisplay;
        }

        private void LoginUsernameDisplay(SessionBase session)
        {
            //Check to see if there is an available channel
            if (_channelDictionary.Count > _configuration.BBSChannels)
            {
                session.SendToClient($"\r\n|RED||B|{_configuration.BBSTitle} has reached the maximum number of users: {_configuration.BBSChannels} -- Please try again later.\r\n|RESET|".EncodeToANSIArray());
                session.InputBuffer.SetLength(0);
                session.SessionState = EnumSessionState.LoggedOff;
                return;
            }

            session.SendToClient("\r\n|YELLOW|Enter Username or enter \"|B|NEW|RESET||YELLOW|\" to create a new Account\r\n"
                .EncodeToANSIArray());
            session.SendToClient("|B||WHITE|Username:|RESET| ".EncodeToANSIArray());
            session.SessionState = EnumSessionState.LoginUsernameInput;
        }

        private void LoginUsernameInput(SessionBase session)
        {
            //Only Process on CR
            if (session.GetStatus() != EnumUserStatus.CR_TERMINATED_STRING_AVAILABLE) return;

            var inputValue = Encoding.ASCII.GetString(session.InputBuffer.ToArray());

            if (string.IsNullOrEmpty(inputValue))
            {
                session.SessionState = EnumSessionState.LoginUsernameDisplay;
                return;
            }

            //Validation for username > 29 characters
            if (inputValue.Length > 29)
            {
                session.SendToClient(
                    "\r\n|RED||B|Please enter a Username with less than 30 characters.\r\n|RESET|"
                        .EncodeToANSIArray());
                session.SessionState = EnumSessionState.LoginUsernameDisplay;
                session.InputBuffer.SetLength(0);
                return;
            }

            if (inputValue.ToUpper() == "NEW")
            {
                session.SessionState = EnumSessionState.SignupUsernameDisplay;
                session.InputBuffer.SetLength(0);
                session.SendToClient(new byte[] { 0x1B, 0x5B, 0x32, 0x4A });
                session.SendToClient(new byte[] { 0x1B, 0x5B, 0x48 });

                //Load File if specified in appsettings.json and display if it exists, else display default
                var ansiSignupFileName = _configuration.ANSISignup;
                session.SendToClient(
                    File.Exists(ansiSignupFileName)
                        ? File.ReadAllBytes(ansiSignupFileName).ToArray()
                        : _resourceManager.GetResource("MBBSEmu.Assets.signup.ans").ToArray());
                return;
            }

            //Check to see if user is already logged in
            if (_channelDictionary.Values.Any(s => string.Equals(s.Username, inputValue, StringComparison.CurrentCultureIgnoreCase)))
            {
                session.SendToClient($"\r\n|RED||B|{inputValue} is already logged in -- only 1 connection allowed per user.\r\n|RESET|".EncodeToANSIArray());
                session.InputBuffer.SetLength(0);
                session.SessionState = EnumSessionState.LoginUsernameDisplay;
                return;
            }

            session.Username = inputValue;
            session.InputBuffer.SetLength(0);
            session.SessionState = EnumSessionState.LoginPasswordDisplay;
        }

        private void LoginPasswordDisplay(SessionBase session)
        {
            session.SendToClient("|B||WHITE|Password:|RESET| ".EncodeToANSIArray());
            session.SessionState = EnumSessionState.LoginPasswordInput;
            session.ExtUsrAcc.wid = 0xFF;
            session.ExtUsrAcc.ech = (byte)'*';
            session.EchoSecureEnabled = true;
        }

        private void LoginPasswordInput(SessionBase session)
        {
            //Only Process on CR
            if (session.GetStatus() != EnumUserStatus.CR_TERMINATED_STRING_AVAILABLE) return;

            //Get The Password
            session.Password = Encoding.ASCII.GetString(session.InputBuffer.ToArray());
            session.InputBuffer.SetLength(0);
            session.SessionState = EnumSessionState.LoginPasswordDisplay;

            //See if the Account exists
            var account = _accountRepository.GetAccountByUsernameAndPassword(session.Username, session.Password);

            if (account == null)
            {
                session.SendToClient("\r\n|B||RED|Invalid Credentials|RESET|\r\n".EncodeToANSIArray());
                session.Username = "";
                session.EchoSecureEnabled = false;
                session.SessionState = EnumSessionState.LoginUsernameDisplay;
                return;
            }

            //Populate Session Variables from mbbsemu.db
            session.Username = account.userName;
            session.UsrAcc.credat = account.createDate.ToDosDate();

            //Lookup User in BBSUSR.db
            var accountBtrieve = _globalCache.Get<BtrieveFileProcessor>("ACCBB-PROCESSOR");

            var result = accountBtrieve.PerformOperation(0, new Span<byte>(new UserAccount
            {
                userid = Encoding.ASCII.GetBytes(session.Username.ToUpper()),
                psword = Encoding.ASCII.GetBytes("<<HASHED>>")
            }.Data).Slice(0, 55), EnumBtrieveOperationCodes.AcquireEqual);

            if (!result)
            {
                session.SendToClient("\r\n|B||RED|USER MISMATCH IN BBSUSR.DAT -- PLEASE NOTIFY SYSOP|RESET|\r\n".EncodeToANSIArray());
                session.Username = "";
                session.EchoSecureEnabled = false;
                session.SessionState = EnumSessionState.LoginUsernameDisplay;
                return;
            }

            //Populate Session Variables from BBSUSR.db
            session.UsrAcc.sex = accountBtrieve.GetRecord().ElementAt(213);

            //Start Session
            session.SessionState = EnumSessionState.LoginRoutines;
            session.EchoSecureEnabled = false;
            session.SessionTimer.Start();

            if (_accountKeyRepository.GetAccountKeysByUsername(session.Username).Any(x => x.accountKey == "SYSOP"))
                session.SendToClient("|RED||B|/SYS to access SYSOP commands\r\n".EncodeToANSIArray());
        }


        private void MainMenuDisplay(SessionBase session, Dictionary<string, MbbsModule> modules)
        {
            //Load File if specified in appsettings.json and display if it exists, else display default
            var ansiMenuFileName = _configuration.ANSIMenu;
            if (!File.Exists(ansiMenuFileName))
            {
                session.SendToClient("\r\n|GREEN||B|Please select one of the following:|RESET|\r\n\r\n".EncodeToANSIArray());

                if (modules.Values.Count > 19)
                {
                    var moduleList = modules.Values.OrderBy(x => x.ModuleConfig.MenuOptionKey.PadLeft(4, '0')).Where(x => (bool)x.ModuleConfig.ModuleEnabled).Select(m => new Tuple<string, string>(m.ModuleConfig.MenuOptionKey, m.ModuleDescription)).ToList();
                    var columnSeed = moduleList.Count / 2;
                    var columnFlag = moduleList.Count % 2;
                    if (columnFlag == 1)
                        columnSeed++;
                    for (var i = 0; i < columnSeed; i++)
                    {
                        if (columnFlag == 0 || i < columnSeed - 1 && columnFlag == 1)
                            session.SendToClient($"      |CYAN||B|{moduleList[i].Item1,-2}|YELLOW| ... {moduleList[i].Item2,-ModuleStruct.DESCRP_SIZE}   |CYAN||B|{moduleList[i + columnSeed].Item1,-2}|YELLOW| ... {moduleList[i + columnSeed].Item2,-ModuleStruct.DESCRP_SIZE}\r\n".EncodeToANSIArray());
                        if (i == columnSeed - 1 && columnFlag == 1)
                            session.SendToClient($"      |CYAN||B|{moduleList[i].Item1,-2}|YELLOW| ... {moduleList[i].Item2,-28}\r\n".EncodeToANSIArray());
                    }
                }
                else
                {
                    foreach (var m in modules.Values.OrderBy(x => x.ModuleConfig.MenuOptionKey.PadLeft(4, '0')).Where(x => (bool)x.ModuleConfig.ModuleEnabled))
                    {
                        session.SendToClient($"   |CYAN||B|{m.ModuleConfig.MenuOptionKey.PadRight(modules.Count > 9 ? 2 : 1, ' ')}|YELLOW| ... {m.ModuleDescription}\r\n".EncodeToANSIArray());
                    }
                }
            }
            else
            {
                session.SendToClient(File.ReadAllBytes(ansiMenuFileName).ToArray());
            }

            session.SessionState = EnumSessionState.MainMenuInputDisplay;
        }

        private void MainMenuInputDisplay(SessionBase session)
        {
            session.SendToClient("\r\n|YELLOW||B|Main Menu\r\n".EncodeToANSIArray());
            session.SendToClient("|CYAN||B|Make your selection (X to exit): ".EncodeToANSIArray());

            session.SessionState = EnumSessionState.MainMenuInput;
        }

        private void MainMenuInput(SessionBase session, Dictionary<string, MbbsModule> modules)
        {
            if (session.GetStatus() != EnumUserStatus.CR_TERMINATED_STRING_AVAILABLE) return;

            if (session.InputBuffer.Length == 0)
            {
                session.SessionState = EnumSessionState.MainMenuDisplay;
                session.InputBuffer.SetLength(0);
                return;
            }

            var inputValue = Encoding.ASCII.GetString(session.InputBuffer.ToArray());

            //convert to uppercase, trim
            var inputCommand = inputValue.ToUpper().TrimEnd('\0');

            //User is Logging Off
            if (Equals(inputCommand, "X"))
            {
                session.SessionState = EnumSessionState.ConfirmLogoffDisplay;
                session.InputBuffer.SetLength(0);
                return;
            }

            var selectedMenuItem = modules.Values.FirstOrDefault(m => m.ModuleConfig.MenuOptionKey.Equals(inputCommand, StringComparison.InvariantCultureIgnoreCase) && (bool)m.ModuleConfig.ModuleEnabled);

            //Check to see if input matched a module, if not redisplay menu
            if (selectedMenuItem == null)
            {
                session.SessionState = EnumSessionState.MainMenuDisplay;
                session.InputBuffer.SetLength(0);
            }
            else
            {
                session.InputBuffer.SetLength(1);
                session.CurrentModule = selectedMenuItem;
                session.SessionState = EnumSessionState.EnteringModule;
                session.SendToClient(new byte[] { 0x1B, 0x5B, 0x32, 0x4A });
                session.SendToClient(new byte[] { 0x1B, 0x5B, 0x48 });
            }
        }

        private void LogoffConfirmationDisplay(SessionBase session)
        {
            if (session.SessionType == EnumSessionType.Rlogin)
            {
                session.SessionState = EnumSessionState.LoggedOff;
                session.SendToClient("\r\n|WHITE||B|<Press Any Key>|RESET|\r\n".EncodeToANSIArray());
                return;
            }

            session.SendToClient("|WHITE||BLINK||B|You are about to terminate this connection!|RESET|\r\n".EncodeToANSIArray());
            session.SendToClient("\r\n|CYAN||B|Are you sure (Y/N, or R to re-logon)? ".EncodeToANSIArray());
            session.SessionState = EnumSessionState.ConfirmLogoffInput;
        }

        private void LogoffConfirmationInput(SessionBase session)
        {
            if (session.GetStatus() != EnumUserStatus.CR_TERMINATED_STRING_AVAILABLE) return;

            var inputValue = Encoding.ASCII.GetString(session.InputBuffer.ToArray()).TrimEnd('\0').ToUpper();

            switch (inputValue)
            {
                case "Y":
                    session.SessionState = EnumSessionState.LoggingOffDisplay;
                    break;
                case "N":
                    session.SessionState = EnumSessionState.MainMenuDisplay;
                    break;
                case "R":
                    session.SessionState = EnumSessionState.Unauthenticated;
                    session.Username = string.Empty;
                    session.Password = string.Empty;
                    break;
                default:
                    session.SessionState = EnumSessionState.ConfirmLogoffDisplay;
                    break;
            }

            //Clear the Input Buffer
            session.InputBuffer.SetLength(0);
        }

        private void LoggingOffDisplay(SessionBase session)
        {
            //Load File if specified in appsettings.json and display if it exists
            var ansiLogoffFileName = _configuration.ANSILogoff;
            session.SendToClient(
                File.Exists(ansiLogoffFileName)
                    ? File.ReadAllBytes(ansiLogoffFileName).ToArray()
                    : "\r\n\r\n|GREEN||B|Ok, thanks for calling!\r\n\r\nHave a nice day...\r\n\r\n"
                        .EncodeToANSIArray());
            session.SessionState = EnumSessionState.LoggingOffProcessing;
            session.Stop();
        }

        private void SignupUsernameDisplay(SessionBase session)
        {
            session.SendToClient("\r\n|CYAN||B|Please enter a unique Username (Max. 29 Characters):|RESET||WHITE||B|\r\n".EncodeToANSIArray());
            session.SessionState = EnumSessionState.SignupUsernameInput;
        }

        private void SignupUsernameInput(SessionBase session)
        {
            if (session.GetStatus() != EnumUserStatus.CR_TERMINATED_STRING_AVAILABLE) return;

            var inputValue = Encoding.ASCII.GetString(session.InputBuffer.ToArray());

            //Validation for the blank username
            if (string.IsNullOrEmpty(inputValue))
            {
                session.SendToClient(
                    "\r\n|RED||B|Please enter a valid Username.\r\n|RESET|"
                        .EncodeToANSIArray());
                session.SessionState = EnumSessionState.SignupUsernameDisplay;
                session.InputBuffer.SetLength(0);
                return;
            }

            //Validation for username > 29 characters
            if (inputValue.Length > 29)
            {
                session.SendToClient(
                    "\r\n|RED||B|Please enter a Username with less than 30 characters.\r\n|RESET|"
                        .EncodeToANSIArray());
                session.SessionState = EnumSessionState.SignupUsernameDisplay;
                session.InputBuffer.SetLength(0);
                return;
            }

            //Validation for an existing username
            if (_accountRepository.GetAccountByUsername(inputValue) != null)
            {
                session.SendToClient(
                    "\r\n|RED||B|That Username is unavailable, please choose another.\r\n|RESET|"
                        .EncodeToANSIArray());
                session.SessionState = EnumSessionState.SignupUsernameDisplay;
                session.InputBuffer.SetLength(0);
                return;
            }

            session.Username = inputValue;
            session.SessionState = EnumSessionState.SignupPasswordDisplay;
            session.InputBuffer.SetLength(0);
        }

        private void SignupPasswordDisplay(SessionBase session)
        {
            session.SendToClient("\r\n|RED||B|NOTE: Passwords are stored in a local database hashed with SHA-512+unique salt.\r\n".EncodeToANSIArray());
            session.SendToClient("The MajorBBS and Worldgroup have a maximum password size of only 9 characters,\r\n".EncodeToANSIArray());
            session.SendToClient("so please select a strong, unique password that you have never used before\r\n".EncodeToANSIArray());
            session.SendToClient("and will only use on this system.\r\n|RESET|".EncodeToANSIArray());
            session.SendToClient("\r\n|CYAN||B|Please enter a strong Password:|RESET|\r\n|WHITE||B|".EncodeToANSIArray());
            session.SessionState = EnumSessionState.SignupPasswordInput;
            session.ExtUsrAcc.wid = 0xFF;
            session.ExtUsrAcc.ech = (byte)'*';
            session.EchoSecureEnabled = true;
        }

        private void SignupPasswordInput(SessionBase session)
        {
            if (session.GetStatus() != EnumUserStatus.CR_TERMINATED_STRING_AVAILABLE) return;

            var inputValue = Encoding.ASCII.GetString(session.InputBuffer.ToArray());

            if (inputValue.Length <= 4 || inputValue.Length >= 10)
            {
                session.SendToClient(
                    "\r\n|RED||B|Passwords must be between 5 and 9 characters long.\r\n|RESET|"
                        .EncodeToANSIArray());
                session.SessionState = EnumSessionState.SignupPasswordDisplay;
                session.InputBuffer.SetLength(0);
                return;
            }

            if (inputValue.ToUpper() == "PASSWORD")
            {
                session.SendToClient(
                    "\r\n|RED||B|Please enter a better password. Seriously.\r\n|RESET|".EncodeToANSIArray());
                session.SessionState = EnumSessionState.SignupPasswordDisplay;
                session.InputBuffer.SetLength(0);
                return;
            }

            session.Password = inputValue;
            session.SessionState = EnumSessionState.SignupPasswordConfirmDisplay;
            session.InputBuffer.SetLength(0);
        }

        private void SignupPasswordConfirmDisplay(SessionBase session)
        {
            session.SendToClient("\r\n|CYAN||B|Please re-enter your password to confirm:|RESET|\r\n|WHITE||B|".EncodeToANSIArray());
            session.SessionState = EnumSessionState.SignupPasswordConfirmInput;
            session.ExtUsrAcc.wid = 0xFF;
            session.ExtUsrAcc.ech = (byte)'*';
            session.EchoSecureEnabled = true;
        }

        private void SignupPasswordConfirmInput(SessionBase session)
        {
            if (session.GetStatus() != EnumUserStatus.CR_TERMINATED_STRING_AVAILABLE) return;

            var inputValue = Encoding.ASCII.GetString(session.InputBuffer.ToArray());

            if (inputValue != session.Password)
            {
                session.SendToClient(
                    "\r\n|RED||B|The passwords you entered did not match, please try again.|RESET|\r\n"
                        .EncodeToANSIArray());
                session.SessionState = EnumSessionState.SignupPasswordDisplay;
                session.InputBuffer.SetLength(0);
                return;
            }

            session.SessionState = EnumSessionState.SignupEmailDisplay;
            session.InputBuffer.SetLength(0);
        }

        private void SignupEmailDisplay(SessionBase session)
        {
            session.EchoSecureEnabled = false;
            session.SendToClient("\r\n|CYAN||B|Please enter a valid e-Mail Address:|RESET|\r\n|WHITE||B|".EncodeToANSIArray());
            session.SessionState = EnumSessionState.SignupEmailInput;
        }

        private void SignupEmailInput(SessionBase session)
        {
            if (session.GetStatus() != EnumUserStatus.CR_TERMINATED_STRING_AVAILABLE) return;

            var inputValue = Encoding.ASCII.GetString(session.InputBuffer.ToArray());

            var emailRegEx = new Regex(
                "^(?(\")(\".+?(?<!\\\\)\"@)|(([0-9a-z]((\\.(?!\\.))|[-!#\\$%&'\\*\\+/=\\?\\^`\\{\\}\\|~\\w])*)(?<=[0-9a-z])@))(?(\\[)(\\[(\\d{1,3}\\.){3}\\d{1,3}\\])|(([0-9a-z][-0-9a-z]*[0-9a-z]*\\.)+[a-z0-9][\\-a-z0-9]{0,22}[a-z0-9]))$");

            if (!emailRegEx.IsMatch(inputValue))
            {
                session.SendToClient(
                    "\r\n|RED||B|Please enter a valid e-Mail address.\r\n|RESET|".EncodeToANSIArray());
                session.SessionState = EnumSessionState.SignupEmailDisplay;
                session.InputBuffer.SetLength(0);
                return;
            }

            session.Email = inputValue;

            session.SessionState = EnumSessionState.SignupGenderDisplay;
            session.InputBuffer.SetLength(0);
        }

        private void SignupGenderDisplay(SessionBase session)
        {
            session.SendToClient("\r\n|CYAN||B|Please enter your gender 'M' or 'F' (can be changed later):|RESET|\r\n|WHITE||B|".EncodeToANSIArray());
            session.SessionState = EnumSessionState.SignupGenderInput;
        }

        private void SignupGenderInput(SessionBase session)
        {
            if (session.GetStatus() != EnumUserStatus.CR_TERMINATED_STRING_AVAILABLE) return;

            var inputValue = Encoding.ASCII.GetString(session.InputBuffer.ToArray());

            if (inputValue.ToUpper() is not ("M" or "F"))
            {
                session.SendToClient("\r\n|RED||B|Please enter a valid gender selection ('M' or 'F').\r\n|RESET|".EncodeToANSIArray());
                session.SessionState = EnumSessionState.SignupGenderDisplay;
                session.InputBuffer.SetLength(0);
                return;
            }

            session.UsrAcc.sex = (byte) char.Parse(inputValue);

            //Create the user in the database
            var accountId = _accountRepository.InsertAccount(session.Username, session.Password, session.Email);
            foreach (var c in _configuration.DefaultKeys)
                _accountKeyRepository.InsertAccountKey(accountId, c);

            //Add The User to the BBS Btrieve User Database
            var _accountBtrieve = _globalCache.Get<BtrieveFileProcessor>("ACCBB-PROCESSOR");
            _accountBtrieve.Insert(new UserAccount { userid = Encoding.ASCII.GetBytes(session.Username), psword = Encoding.ASCII.GetBytes("<<HASHED>>"), sex = session.UsrAcc.sex }.Data, LogLevel.Error);

            session.SessionState = EnumSessionState.LoginRoutines;
            session.InputBuffer.SetLength(0);
        }
    }
}
