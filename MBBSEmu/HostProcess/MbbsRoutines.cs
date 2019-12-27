using MBBSEmu.Database.Repositories.Account;
using MBBSEmu.Extensions;
using MBBSEmu.Module;
using MBBSEmu.Resources;
using MBBSEmu.Session;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace MBBSEmu.HostProcess
{
    public class MbbsRoutines : IMbbsRoutines
    {
        private readonly IResourceManager _resourceManager;
        private readonly IConfigurationRoot _configurationRoot;
        private readonly IAccountRepository _accountRepository;

        public MbbsRoutines(IResourceManager resourceManager, IConfigurationRoot configurationRoot,
            IAccountRepository accountRepository)
        {
            _resourceManager = resourceManager;
            _configurationRoot = configurationRoot;
            _accountRepository = accountRepository;
        }

        /// <summary>
        ///     Invokes Method based on the Session State
        /// </summary>
        /// <param name="session"></param>
        /// <param name="modules"></param>
        public void ProcessSessionState(UserSession session, Dictionary<string, MbbsModule> modules)
        {
            switch (session.SessionState)
            {
                case EnumSessionState.Unauthenticated:
                    DisplayWelcomeScreen(session);
                    return;
                case EnumSessionState.DisplayingLoginUsername:
                    DisplayLoginUsername(session);
                    return;
                case EnumSessionState.AuthenticatingUsername:
                    InputtingUsername(session);
                    return;
                case EnumSessionState.DisplayingLoginPassword:
                    DisplayLoginPassword(session);
                    return;
                case EnumSessionState.AuthenticatingPassword:
                    InputtingPassword(session);
                    return;
                case EnumSessionState.MainMenuDisplay:
                    MainMenuDisplay(session, modules);
                    return;
                case EnumSessionState.MainMenuInput:
                    InputtingMainMenuSelection(session, modules);
                    return;
                case EnumSessionState.ConfirmLogoffDisplay:
                    ConfirmLogoffDisplay(session);
                    return;
                case EnumSessionState.ConfirmLogoffInput:
                    InputtingConfirmLogoff(session);
                    return;
                case EnumSessionState.SignupUsernameDisplay:
                    SignupUsernameDisplay(session);
                    return;
                case EnumSessionState.SignupUsernameInput:
                    SignupUsernameInput(session);
                    return;
                case EnumSessionState.SignupPasswordDisplay:
                    SignupPasswordDisplay(session);
                    return;
                case EnumSessionState.SignupPasswordInput:
                    SignupPasswordInput(session);
                    return;
                case EnumSessionState.SignupEmailDisplay:
                    SignupEmailDisplay(session);
                    return;
                case EnumSessionState.SignupEmailInput:
                    SignupEmailInput(session);
                    return;
                case EnumSessionState.LoggingOff:
                    LoggingOff(session);
                    return;
                default:
                    return;
            }
        }

        /// <summary>
        ///     Data to Echo to client
        ///
        ///     This will handle cases like properly doing backspaces, etc.
        /// </summary>
        /// <param name="session"></param>
        /// <param name="dataToEcho"></param>
        private void EchoToClient(UserSession session, byte[] dataToEcho)
        {
            //Handle Backspace or Delete
            if (dataToEcho.Length == 1 && (dataToEcho[0] == 0x8 || dataToEcho[0] == 0x7F))
            {
                session.DataToClient.Enqueue(new byte[] { 0x08, 0x20, 0x08 });
                return;
            }

            session.DataToClient.Enqueue(dataToEcho);
        }

        /// <summary>
        ///     Applies Backspace from the Client to the Input Buffer
        /// </summary>
        /// <param name="session"></param>
        /// <param name="inputFromUser"></param>
        /// <returns></returns>
        private bool HandleBackspace(UserSession session, ReadOnlySpan<byte> inputFromUser)
        {
            if (inputFromUser[0] != 0x8 && inputFromUser[0] != 0x7F)
                return false;

            //If its backspace and the buffer is already empty, do nothing
            if (session.InputBuffer.Length <= 0)
                return true;

            //Because position starts at 0, it's an easy -1 on length
            session.InputBuffer.SetLength(session.InputBuffer.Length - 1);
            return true;
        }

        /// <summary>
        ///     Shows the Login Screen to a new User Session
        /// </summary>
        /// <param name="session"></param>
        public void DisplayWelcomeScreen(UserSession session)
        {
            session.DataToClient.Enqueue(new byte[] { 0x1B, 0x5B, 0x32, 0x4A });
            session.DataToClient.Enqueue(new byte[] { 0x1B, 0x5B, 0x48 });
            session.DataToClient.Enqueue(_resourceManager.GetResource("MBBSEmu.Assets.login.ans").ToArray());
            session.DataToClient.Enqueue(Encoding.ASCII.GetBytes("\r\n "));
            session.SessionState = EnumSessionState.DisplayingLoginUsername;
        }

        public void DisplayLoginUsername(UserSession session)
        {
            session.DataToClient.Enqueue("\r\n|YELLOW|Enter Username or enter \"|B|NEW|!B|\" to create a new Account\r\n".EncodeToANSIArray());
            session.DataToClient.Enqueue("|B||WHITE|Username:|!B| ".EncodeToANSIArray());
            session.SessionState = EnumSessionState.AuthenticatingUsername;
        }

        public void InputtingUsername(UserSession session)
        {
            if (session.DataFromClient.TryDequeue(out var inputBytes))
            {
                EchoToClient(session, inputBytes);

                if (HandleBackspace(session, inputBytes))
                    return;

                //CR
                if (inputBytes[0] == 0xD)
                {
                    var inputValue = Encoding.ASCII.GetString(session.InputBuffer.ToArray());

                    if (inputValue.ToUpper() == "NEW")
                    {
                        session.SessionState = EnumSessionState.SignupUsernameDisplay;
                        session.InputBuffer.SetLength(0);
                        session.DataToClient.Enqueue(new byte[] { 0x1B, 0x5B, 0x32, 0x4A });
                        session.DataToClient.Enqueue(new byte[] { 0x1B, 0x5B, 0x48 });
                        session.DataToClient.Enqueue(_resourceManager.GetResource("MBBSEmu.Assets.signup.ans").ToArray());
                        return;
                    }

                    session.Username = inputValue;
                    session.InputBuffer.SetLength(0);
                    session.SessionState = EnumSessionState.DisplayingLoginPassword;
                    return;
                }

                session.InputBuffer.Write(inputBytes);
            }
        }

        public void DisplayLoginPassword(UserSession session)
        {
            session.DataToClient.Enqueue("\r\n|B||WHITE|Password:|!B| ".EncodeToANSIArray());
            session.SessionState = EnumSessionState.AuthenticatingPassword;
        }

        public void InputtingPassword(UserSession session)
        {
            if (session.DataFromClient.TryDequeue(out var inputBytes))
            {
                //CR
                if (inputBytes[0] == 0xD)
                {
                    //Get The Password
                    session.Password = Encoding.ASCII.GetString(session.InputBuffer.ToArray());
                    session.InputBuffer.SetLength(0);
                    session.SessionState = EnumSessionState.DisplayingLoginPassword;

                    //See if the Account exists
                    var accountRepo = DependencyInjection.ServiceResolver.GetService<IAccountRepository>();
                    var account = accountRepo.GetAccountByUsernameAndPassword(session.Username, session.Password);

                    if (account == null)
                    {
                        EchoToClient(session, "\r\n\r\n|B||RED|Invalid Credentials|RESET|\r\n".EncodeToANSIArray());
                        session.SessionState = EnumSessionState.DisplayingLoginUsername;
                        return;
                    }

                    session.SessionState = EnumSessionState.MainMenuDisplay;
                    return;
                }

                if (HandleBackspace(session, inputBytes))
                {
                    EchoToClient(session, inputBytes);
                    return;
                }

                //Mask Passwords
                EchoToClient(session, new byte[] { 0x2A });

                session.InputBuffer.Write(inputBytes);
            }
        }

        public void MainMenuDisplay(UserSession session, Dictionary<string, MbbsModule> modules)
        {
            EchoToClient(session, "\r\n\r\n|GREEN||B|Please select one of the following:|RESET|\r\n\r\n".EncodeToANSIArray());

            var currentMenuItem = 0;
            foreach (var m in modules)
            {
                EchoToClient(session, $"   |CYAN||B|{currentMenuItem}|YELLOW| ... {m.Value.ModuleDescription}\r\n".EncodeToANSIArray());
            }

            EchoToClient(session, "\r\n|GREEN|Main Menu|RESET|\r\n".EncodeToANSIArray());
            EchoToClient(session, "|CYAN||B|Make your selection (X to exit):|RESET| ".EncodeToANSIArray());
            session.SessionState = EnumSessionState.MainMenuInput;
        }

        public void InputtingMainMenuSelection(UserSession session, Dictionary<string, MbbsModule> modules)
        {
            if (session.DataFromClient.TryDequeue(out var inputBytes))
            {
                EchoToClient(session, inputBytes);

                if (inputBytes[0] == 0xD)
                {
                    var inputValue = Encoding.ASCII.GetString(session.InputBuffer.ToArray()).ToUpper();

                    //User is Logging Off
                    if (inputValue == "X")
                    {
                        session.SessionState = EnumSessionState.ConfirmLogoffDisplay;
                        session.InputBuffer.SetLength(0);
                        return;
                    }

                    //If at this point, it's an unknown selection and it's NOT a module number, then re-display menu
                    if (!int.TryParse(inputValue, out var selectedMenuItem))
                    {
                        session.SessionState = EnumSessionState.MainMenuDisplay;
                        session.InputBuffer.SetLength(0);
                        return;
                    }

                    var selectedModule = modules.ElementAt(selectedMenuItem);
                    session.ModuleIdentifier = selectedModule.Value.ModuleIdentifier;
                    session.SessionState = EnumSessionState.EnteringModule;
                    session.DataToClient.Enqueue(new byte[] {0x1B, 0x5B, 0x32, 0x4A});
                    session.DataToClient.Enqueue(new byte[] {0x1B, 0x5B, 0x48});

                    //Clear the Input Buffer
                    session.InputBuffer.SetLength(0);
                }

                session.InputBuffer.Write(inputBytes);
            }
        }

        public void ConfirmLogoffDisplay(UserSession session)
        {
            EchoToClient(session, "\r\n|WHITE||BLINK||B|You are about to terminate this connection!|RESET|\r\n".EncodeToANSIArray());
            EchoToClient(session, "\r\n|CYAN||B|Are you sure (Y/N, or R to re-logon)? ".EncodeToANSIArray());
            session.SessionState = EnumSessionState.ConfirmLogoffInput;
        }

        public void InputtingConfirmLogoff(UserSession session)
        {
            if (session.DataFromClient.TryDequeue(out var inputBytes))
            {
                EchoToClient(session, inputBytes);

                if (inputBytes[0] == 0xD)
                {
                    var inputValue = Encoding.ASCII.GetString(session.InputBuffer.ToArray()).ToUpper();

                    switch (inputValue)
                    {
                        case "Y":
                            session.SessionState = EnumSessionState.LoggingOff;
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

                    return;
                }
                session.InputBuffer.Write(inputBytes);
            }
        }

        public void LoggingOff(UserSession session)
        {
            EchoToClient(session, "\r\n|GREEN||B|Ok, thanks for calling!\r\n\r\nHave a nice day... ".EncodeToANSIArray());
            session.SessionState = EnumSessionState.LoggedOff;
        }

        public void SignupUsernameDisplay(UserSession session)
        {
            EchoToClient(session, "\r\n|CYAN||B|Please enter a unique Username (Max. 30 Characters):|RESET||WHITE||B|\r\n".EncodeToANSIArray());
            session.SessionState = EnumSessionState.SignupUsernameInput;
        }

        public void SignupUsernameInput(UserSession session)
        {
            if (session.DataFromClient.TryDequeue(out var inputBytes))
            {
                EchoToClient(session, inputBytes);

                if (inputBytes[0] == 0xD)
                {
                    var inputValue = Encoding.ASCII.GetString(session.InputBuffer.ToArray());

                    if (_accountRepository.GetAccountByUsername(inputValue) != null)
                    {
                        EchoToClient(session,
                            "\r\n|RED|B|That Username is unavailable, please choose another.\r\n|RESET|"
                                .EncodeToANSIArray());
                        session.SessionState = EnumSessionState.SignupUsernameDisplay;
                        session.InputBuffer.SetLength(0);
                        return;
                    }

                    session.Username = inputValue;
                    session.SessionState = EnumSessionState.SignupPasswordDisplay;
                    session.InputBuffer.SetLength(0);
                    return;
                }

                session.InputBuffer.Write(inputBytes);
            }
        }

        public void SignupPasswordDisplay(UserSession session)
        {
            EchoToClient(session, "\r\n|RED||B|NOTE: Passwords are stored in a local database hashed with SHA-512+unique salt.\r\n".EncodeToANSIArray());
            EchoToClient(session, "The MajorBBS and Worldgroup have a maximum password size of only 9 characters,\r\n".EncodeToANSIArray());
            EchoToClient(session, "so please select a strong, unique password that you have never used before\r\n".EncodeToANSIArray());
            EchoToClient(session, "and will only use on this system.\r\n|RESET|".EncodeToANSIArray());
            EchoToClient(session, "\r\n|CYAN||B|Please enter a strong Password:|RESET|\r\n|WHITE||B|".EncodeToANSIArray());
            session.SessionState = EnumSessionState.SignupPasswordInput;
        }

        public void SignupPasswordInput(UserSession session)
        {
            if (session.DataFromClient.TryDequeue(out var inputBytes))
            {
                EchoToClient(session, inputBytes);

                if (inputBytes[0] == 0xD)
                {
                    var inputValue = Encoding.ASCII.GetString(session.InputBuffer.ToArray());

                    if (inputValue.Length <= 4 || inputValue.Length >= 10)
                    {
                        EchoToClient(session, "\r\n|RED||B|Passwords must be between 5 and 9 characters long.\r\n|RESET|".EncodeToANSIArray());
                        session.SessionState = EnumSessionState.SignupPasswordDisplay;
                        session.InputBuffer.SetLength(0);
                        return;
                    }

                    if (inputValue.ToUpper() == "PASSWORD")
                    {
                        EchoToClient(session, "\r\n|RED||B|Please enter a better password. Seriously.\r\n|RESET|".EncodeToANSIArray());
                        session.SessionState = EnumSessionState.SignupPasswordDisplay;
                        session.InputBuffer.SetLength(0);
                        return;
                    }

                    session.Password = inputValue;
                    session.SessionState = EnumSessionState.SignupEmailDisplay;
                    session.InputBuffer.SetLength(0);
                    return;
                }

                session.InputBuffer.Write(inputBytes);
            }
        }

        public void SignupEmailDisplay(UserSession session)
        {
            EchoToClient(session, "\r\n|CYAN||B|Please enter a valid e-Mail Address:|RESET|\r\n|WHITE||B|".EncodeToANSIArray());
            session.SessionState = EnumSessionState.SignupEmailInput;
        }

        public void SignupEmailInput(UserSession session)
        {
            if (session.DataFromClient.TryDequeue(out var inputBytes))
            {
                EchoToClient(session, inputBytes);

                if (inputBytes[0] == 0xD)
                {
                    var inputValue = Encoding.ASCII.GetString(session.InputBuffer.ToArray());

                    var emailRegEx = new Regex(
                        "^(?(\")(\".+?(?<!\\\\)\"@)|(([0-9a-z]((\\.(?!\\.))|[-!#\\$%&'\\*\\+/=\\?\\^`\\{\\}\\|~\\w])*)(?<=[0-9a-z])@))(?(\\[)(\\[(\\d{1,3}\\.){3}\\d{1,3}\\])|(([0-9a-z][-0-9a-z]*[0-9a-z]*\\.)+[a-z0-9][\\-a-z0-9]{0,22}[a-z0-9]))$");

                    if (!emailRegEx.IsMatch(inputValue))
                    {
                        EchoToClient(session, "\r\n|RED|B|Please enter a valid e-Mail address.\r\n|RESET|".EncodeToANSIArray());
                        session.SessionState = EnumSessionState.SignupEmailDisplay;
                        session.InputBuffer.SetLength(0);
                        return;
                    }
                    session.email = inputValue;

                    //Create the user in the database
                    _accountRepository.InsertAccount(session.Username, session.Password, session.email);

                    session.SessionState = EnumSessionState.MainMenuDisplay;
                    session.InputBuffer.SetLength(0);
                    return;
                }

                session.InputBuffer.Write(inputBytes);
            }
        }
    }
}
