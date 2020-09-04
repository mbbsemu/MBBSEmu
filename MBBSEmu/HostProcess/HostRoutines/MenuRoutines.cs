using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using MBBSEmu.Database.Repositories.Account;
using MBBSEmu.Extensions;
using MBBSEmu.Module;
using MBBSEmu.Resources;
using MBBSEmu.Session;
using MBBSEmu.Session.Enums;
using Microsoft.Extensions.Configuration;

namespace MBBSEmu.HostProcess.HostRoutines
{
    /// <summary>
    ///     Menu Routines for MBBSEmu
    /// </summary>
    public class MenuRoutines : IHostRoutine
    {
        private readonly IResourceManager _resourceManager;
        private readonly IAccountRepository _accountRepository;
        /// Configuration Class giving access to the appsettings.json file
        private readonly IConfiguration _configuration;
        public MenuRoutines(IResourceManager resourceManager, IAccountRepository accountRepository, IConfiguration configuration)
        {
            _resourceManager = resourceManager;
            _accountRepository = accountRepository;
            _configuration = configuration;
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
                    ProcessCharacter(session);
                    break;
                case EnumSessionState.LoginPasswordDisplay:
                    LoginPasswordDisplay(session);
                    break;
                case EnumSessionState.LoginPasswordInput:
                    LoginPasswordInput(session);
                    ProcessCharacter(session, true);
                    break;
                case EnumSessionState.SignupPasswordConfirmDisplay:
                    SignupPasswordConfirmDisplay(session);
                    ProcessCharacter(session, true);
                    break;
                case EnumSessionState.SignupPasswordConfirmInput:
                    SignupPasswordConfirmInput(session);
                    ProcessCharacter(session, true);
                    break;
                case EnumSessionState.MainMenuDisplay:
                    MainMenuDisplay(session, modules);
                    break;
                case EnumSessionState.MainMenuInput:
                    MainMenuInput(session, modules);
                    ProcessCharacter(session);
                    break;
                case EnumSessionState.ConfirmLogoffDisplay:
                    LogoffConfirmationDisplay(session);
                    break;
                case EnumSessionState.ConfirmLogoffInput:
                    LogoffConfirmationInput(session);
                    ProcessCharacter(session);
                    break;
                case EnumSessionState.SignupUsernameDisplay:
                    SignupUsernameDisplay(session);
                    break;
                case EnumSessionState.SignupUsernameInput:
                    SignupUsernameInput(session);
                    ProcessCharacter(session);
                    break;
                case EnumSessionState.SignupPasswordDisplay:
                    SignupPasswordDisplay(session);
                    break;
                case EnumSessionState.SignupPasswordInput:
                    SignupPasswordInput(session);
                    ProcessCharacter(session, true);
                    break;
                case EnumSessionState.SignupEmailDisplay:
                    SignupEmailDisplay(session);
                    break;
                case EnumSessionState.SignupEmailInput:
                    SignupEmailInput(session);
                    ProcessCharacter(session);
                    break;
                case EnumSessionState.LoggingOffDisplay:
                    LoggingOffDisplay(session);
                    break;
                default:
                    return false;
            }

            return true;
        }

        private void ProcessCharacter(SessionBase session, bool secure = false)
        {
            //Check to see if there's anything that needs doing
            if (!session.DataToProcess) return;

            session.DataToProcess = false;

            if (HandleBackspace(session, session.LastCharacterReceived))
                return;

            EchoToClient(session, secure ? (byte)0x2A : session.LastCharacterReceived);
        }

        private void EchoToClient(SessionBase session, ReadOnlySpan<byte> dataToEcho)
        {
            foreach (var t in dataToEcho)
                EchoToClient(session, t);
        }

        /// <summary>
        ///     Data to Echo to client
        ///
        ///     This will handle cases like properly doing backspaces, etc.
        /// </summary>
        /// <param name="session"></param>
        /// <param name="dataToEcho"></param>
        private void EchoToClient(SessionBase session, byte dataToEcho)
        {
            //Handle Backspace or Delete
            if (dataToEcho == 0x8 || dataToEcho == 0x7F)
            {
                session.SendToClient(new byte[] { 0x08, 0x20, 0x08 });
                return;
            }

            session.SendToClient(new[] { dataToEcho });
        }

        /// <summary>
        ///     Handles Backspace from the Client
        /// </summary>
        /// <param name="session"></param>
        /// <param name="inputFromUser"></param>
        /// <returns></returns>
        private bool HandleBackspace(SessionBase session, byte inputFromUser)
        {
            //Not a backspace character
            if (inputFromUser != 0x8 && inputFromUser != 0x7F)
                return false;

            if (session.InputBuffer.Length <= 1)
            {
                //Only backspace is in the buffer (first character entered)
                session.InputBuffer.SetLength(0);
            }
            else
            {
                //Remove backspace character and the preceding character
                session.InputBuffer.SetLength(session.InputBuffer.Length -2);
                EchoToClient(session, new byte[] { 0x08, 0x20, 0x08 });
            }

            return true;
        }

        /// <summary>
        ///     Shows the Login Screen to a new User Session
        /// </summary>
        /// <param name="session"></param>
        private void WelcomeScreenDisplay(SessionBase session)
        {
            EchoToClient(session, new byte[] { 0x1B, 0x5B, 0x32, 0x4A });
            EchoToClient(session, new byte[] { 0x1B, 0x5B, 0x48 });
            //Load File if specified in appsettings.json and display if it exists, else display default
            var ANSILoginFileName = _configuration["ANSI.Login"];
            if (File.Exists(ANSILoginFileName)) {
                EchoToClient(session, File.ReadAllBytes(ANSILoginFileName).ToArray());
            } else {
                EchoToClient(session, _resourceManager.GetResource("MBBSEmu.Assets.login.ans").ToArray());           
            }
            EchoToClient(session, Encoding.ASCII.GetBytes("\r\n "));
            session.SessionState = EnumSessionState.LoginUsernameDisplay;
        }

        private void LoginUsernameDisplay(SessionBase session)
        {
            EchoToClient(session, "\r\n|YELLOW|Enter Username or enter \"|B|NEW|RESET||YELLOW|\" to create a new Account\r\n"
                .EncodeToANSIArray());
            EchoToClient(session, "|B||WHITE|Username:|RESET| ".EncodeToANSIArray());
            session.SessionState = EnumSessionState.LoginUsernameInput;
        }

        private void LoginUsernameInput(SessionBase session)
        {
            //Only Process on CR
            if (session.Status != 3) return;
            session.Status = 0;

            var inputValue = Encoding.ASCII.GetString(session.InputBuffer.ToArray());

            if (string.IsNullOrEmpty(inputValue))
            {
                session.SessionState = EnumSessionState.LoginUsernameDisplay;
                return;
            }

            //Validation for username > 29 characters
            if (inputValue.Length > 29 )
            {
                EchoToClient(session,
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
                EchoToClient(session, new byte[] { 0x1B, 0x5B, 0x32, 0x4A });
                EchoToClient(session, new byte[] { 0x1B, 0x5B, 0x48 });
                
                //Load File if specified in appsettings.json and display if it exists, else display default
                var ANSISignupFileName = _configuration["ANSI.Signup"];
                if (File.Exists(ANSISignupFileName)) {
                    EchoToClient(session, File.ReadAllBytes(ANSISignupFileName).ToArray());
                } else {              
                    EchoToClient(session, _resourceManager.GetResource("MBBSEmu.Assets.signup.ans").ToArray());
                }
                return;
            }

            session.Username = inputValue;
            session.InputBuffer.SetLength(0);
            session.SessionState = EnumSessionState.LoginPasswordDisplay;
        }

        private void LoginPasswordDisplay(SessionBase session)
        {
            EchoToClient(session, "|B||WHITE|Password:|RESET| ".EncodeToANSIArray());
            session.SessionState = EnumSessionState.LoginPasswordInput;
        }

        private void LoginPasswordInput(SessionBase session)
        {
            //Only Process on CR
            if (session.Status != 3) return;
            session.Status = 0;

            //Get The Password
            session.Password = Encoding.ASCII.GetString(session.InputBuffer.ToArray());
            session.InputBuffer.SetLength(0);
            session.SessionState = EnumSessionState.LoginPasswordDisplay;

            //See if the Account exists
            var accountRepo = DependencyInjection.ServiceResolver.GetService<IAccountRepository>();
            var account = accountRepo.GetAccountByUsernameAndPassword(session.Username, session.Password);

            if (account == null)
            {
                EchoToClient(session, "\r\n|B||RED|Invalid Credentials|RESET|\r\n".EncodeToANSIArray());
                session.SessionState = EnumSessionState.LoginUsernameDisplay;
                return;
            }

            session.SessionState = EnumSessionState.LoginRoutines;
            session.SessionTimer.Start();
        }


        private void MainMenuDisplay(SessionBase session, Dictionary<string, MbbsModule> modules)
        {
            EchoToClient(session, "\r\n|GREEN||B|Please select one of the following:|RESET|\r\n\r\n".EncodeToANSIArray());

            var currentMenuItem = 0;
            foreach (var m in modules)
            {
                EchoToClient(session, $"   |CYAN||B|{currentMenuItem}|YELLOW| ... {m.Value.ModuleDescription}\r\n".EncodeToANSIArray());
                currentMenuItem++;
            }

            EchoToClient(session, "\r\n|GREEN|Main Menu|RESET|\r\n".EncodeToANSIArray());
            EchoToClient(session, "|CYAN||B|Make your selection (X to exit): ".EncodeToANSIArray());
            session.SessionState = EnumSessionState.MainMenuInput;
        }

        private void MainMenuInput(SessionBase session, Dictionary<string, MbbsModule> modules)
        {
            if (session.Status != 3) return;
            session.Status = 0;

            if (session.InputBuffer.Length == 0)
            {
                session.SessionState = EnumSessionState.MainMenuDisplay;
                session.InputBuffer.SetLength(0);
                return;
            }

            var inputValue = Encoding.ASCII.GetString(session.InputBuffer.ToArray()).ToUpper()[0];

            //User is Logging Off
            if (inputValue == 'X')
            {
                session.SessionState = EnumSessionState.ConfirmLogoffDisplay;
                //Load File if specified in appsettings.json and display if it exists, else display default
                var ANSILogoffFileName = _configuration["ANSI.Logoff"];
                if (File.Exists(ANSILogoffFileName)) {
                    EchoToClient(session, File.ReadAllBytes(ANSILogoffFileName).ToArray());
                }
                session.InputBuffer.SetLength(0);
                return;
            }

            //If at this point, it's an unknown selection and it's NOT a module number, then re-display menu
            if (!int.TryParse(inputValue.ToString(), out var selectedMenuItem) || selectedMenuItem >= modules.Count)
            {
                session.SessionState = EnumSessionState.MainMenuDisplay;
                session.InputBuffer.SetLength(0);
                return;
            }

            var selectedModule = modules.ElementAt(selectedMenuItem);
            session.CurrentModule = selectedModule.Value;
            session.SessionState = EnumSessionState.EnteringModule;
            session.SendToClient(new byte[] { 0x1B, 0x5B, 0x32, 0x4A });
            session.SendToClient(new byte[] { 0x1B, 0x5B, 0x48 });
        }

        private void LogoffConfirmationDisplay(SessionBase session)
        {
            if (session.SessionType == EnumSessionType.Rlogin)
            {
                session.SessionState = EnumSessionState.LoggedOff;
                EchoToClient(session, "\r\n|WHITE||B|<Press Any Key>|RESET|\r\n".EncodeToANSIArray());
                return;
            }

            EchoToClient(session, "|WHITE||BLINK||B|You are about to terminate this connection!|RESET|\r\n".EncodeToANSIArray());
            EchoToClient(session, "\r\n|CYAN||B|Are you sure (Y/N, or R to re-logon)? ".EncodeToANSIArray());
            session.SessionState = EnumSessionState.ConfirmLogoffInput;
        }

        private void LogoffConfirmationInput(SessionBase session)
        {
            if (session.Status != 3) return;
            session.Status = 0;

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
            EchoToClient(session, "\r\n\r\n|GREEN||B|Ok, thanks for calling!\r\n\r\nHave a nice day...\r\n\r\n".EncodeToANSIArray());
            session.SessionState = EnumSessionState.LoggingOffProcessing;
            session.Stop();
        }

        private void SignupUsernameDisplay(SessionBase session)
        {
            EchoToClient(session, "\r\n|CYAN||B|Please enter a unique Username (Max. 29 Characters):|RESET||WHITE||B|\r\n".EncodeToANSIArray());
            session.SessionState = EnumSessionState.SignupUsernameInput;
        }

        private void SignupUsernameInput(SessionBase session)
        {
            if (session.Status != 3) return;
            session.Status = 0;

            var inputValue = Encoding.ASCII.GetString(session.InputBuffer.ToArray());

            //Validation for the blank username
            if (string.IsNullOrEmpty(inputValue))
            {
                EchoToClient(session,
                    "\r\n|RED||B|Please enter a valid Username.\r\n|RESET|"
                        .EncodeToANSIArray());
                session.SessionState = EnumSessionState.SignupUsernameDisplay;
                session.InputBuffer.SetLength(0);
                return;
            }

            //Validation for username > 29 characters
            if (inputValue.Length > 29 )
            {
                EchoToClient(session,
                    "\r\n|RED||B|Please enter a Username with less than 30 characters.\r\n|RESET|"
                        .EncodeToANSIArray());
                session.SessionState = EnumSessionState.SignupUsernameDisplay;
                session.InputBuffer.SetLength(0);
                return;
            }

            //Validation for an existing username
            if (_accountRepository.GetAccountByUsername(inputValue) != null)
            {
                EchoToClient(session,
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
            EchoToClient(session, "\r\n|RED||B|NOTE: Passwords are stored in a local database hashed with SHA-512+unique salt.\r\n".EncodeToANSIArray());
            EchoToClient(session, "The MajorBBS and Worldgroup have a maximum password size of only 9 characters,\r\n".EncodeToANSIArray());
            EchoToClient(session, "so please select a strong, unique password that you have never used before\r\n".EncodeToANSIArray());
            EchoToClient(session, "and will only use on this system.\r\n|RESET|".EncodeToANSIArray());
            EchoToClient(session, "\r\n|CYAN||B|Please enter a strong Password:|RESET|\r\n|WHITE||B|".EncodeToANSIArray());
            session.SessionState = EnumSessionState.SignupPasswordInput;
        }

        private void SignupPasswordInput(SessionBase session)
        {
            if (session.Status != 3) return;
            session.Status = 0;

            var inputValue = Encoding.ASCII.GetString(session.InputBuffer.ToArray());

            if (inputValue.Length <= 4 || inputValue.Length >= 10)
            {
                EchoToClient(session,
                    "\r\n|RED||B|Passwords must be between 5 and 9 characters long.\r\n|RESET|"
                        .EncodeToANSIArray());
                session.SessionState = EnumSessionState.SignupPasswordDisplay;
                session.InputBuffer.SetLength(0);
                return;
            }

            if (inputValue.ToUpper() == "PASSWORD")
            {
                EchoToClient(session,
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
            EchoToClient(session, "\r\n|CYAN||B|Please re-enter your password to confirm:|RESET|\r\n|WHITE||B|".EncodeToANSIArray());
            session.SessionState = EnumSessionState.SignupPasswordConfirmInput;
        }

        private void SignupPasswordConfirmInput(SessionBase session)
        {
            if (session.Status != 3) return;
            session.Status = 0;
            var inputValue = Encoding.ASCII.GetString(session.InputBuffer.ToArray());

            if (inputValue != session.Password)
            {
                EchoToClient(session,
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
            EchoToClient(session, "\r\n|CYAN||B|Please enter a valid e-Mail Address:|RESET|\r\n|WHITE||B|".EncodeToANSIArray());
            session.SessionState = EnumSessionState.SignupEmailInput;
        }

        private void SignupEmailInput(SessionBase session)
        {
            if (session.Status != 3) return;
            session.Status = 0;

            var inputValue = Encoding.ASCII.GetString(session.InputBuffer.ToArray());

            var emailRegEx = new Regex(
                "^(?(\")(\".+?(?<!\\\\)\"@)|(([0-9a-z]((\\.(?!\\.))|[-!#\\$%&'\\*\\+/=\\?\\^`\\{\\}\\|~\\w])*)(?<=[0-9a-z])@))(?(\\[)(\\[(\\d{1,3}\\.){3}\\d{1,3}\\])|(([0-9a-z][-0-9a-z]*[0-9a-z]*\\.)+[a-z0-9][\\-a-z0-9]{0,22}[a-z0-9]))$");

            if (!emailRegEx.IsMatch(inputValue))
            {
                EchoToClient(session,
                    "\r\n|RED||B|Please enter a valid e-Mail address.\r\n|RESET|".EncodeToANSIArray());
                session.SessionState = EnumSessionState.SignupEmailDisplay;
                session.InputBuffer.SetLength(0);
                return;
            }

            session.Email = inputValue;

            //Create the user in the database
            _accountRepository.InsertAccount(session.Username, session.Password, session.Email);

            session.SessionState = EnumSessionState.LoginRoutines;
            session.InputBuffer.SetLength(0);
        }
    }
}
