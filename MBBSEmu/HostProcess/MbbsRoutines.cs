using System;
using System.Collections.Generic;
using System.Linq;
using MBBSEmu.Resources;
using MBBSEmu.Session;
using System.Text;
using MBBSEmu.Database.Repositories.Account;
using MBBSEmu.HostProcess.Models;
using MBBSEmu.Module;
using MBBSEmu.Extensions;

namespace MBBSEmu.HostProcess
{
    public class MbbsRoutines : IMbbsRoutines
    {
        private readonly IResourceManager _resourceManager;
        
        public MbbsRoutines(IResourceManager resourceManager)
        {
            _resourceManager = resourceManager;
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
            //Handle Backspace
            if (dataToEcho.Length == 1 && dataToEcho[0] == 0x8)
            {
                session.DataToClient.Enqueue(new byte[] { 0x08, 0x20, 0x08 });
                return;
            }

            session.DataToClient.Enqueue(dataToEcho);
        }

        /// <summary>
        ///     Shows the Login Screen to a new User Session
        /// </summary>
        /// <param name="session"></param>
        public void ShowLogin(UserSession session)
        {
            session.DataToClient.Enqueue(new byte[] { 0x1B, 0x5B, 0x32, 0x4A });
            session.DataToClient.Enqueue(new byte[] { 0x1B, 0x5B, 0x48 });
            session.DataToClient.Enqueue(_resourceManager.GetResource("MBBSEmu.Assets.login.ans").ToArray());
            session.SessionState = EnumSessionState.DisplayingLoginUsername;
        }

        public void DisplayLoginUsername(UserSession session)
        {
            session.DataToClient.Enqueue("\r\n|YELLOW|Enter Username or enter \"|B|NEW|!B|\" to create a new Account\r\n".EncodeToANSIArray());
            session.DataToClient.Enqueue("|B||WHITE|Username:|!B| ".EncodeToANSIArray());
            session.SessionState = EnumSessionState.AuthenticatingUsername;
        }

        public void AuthenticatingUsername(UserSession session)
        {
            //Process Data
            if (session.DataFromClient.Count > 0)
            {
                session.DataFromClient.TryDequeue(out var inputBytes);

                EchoToClient(session, inputBytes);

                //CR
                if (inputBytes[0] == 0xD)
                {
                    session.Username = Encoding.ASCII.GetString(session.InputBuffer.ToArray());
                    session.InputBuffer.Position = 0;
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

        public void AuthenticatingPassword(UserSession session)
        {
            //Process Data
            if (session.DataFromClient.Count > 0)
            {
                session.DataFromClient.TryDequeue(out var inputBytes);

                //CR
                if (inputBytes[0] == 0xD)
                {
                    //Get The Password
                    session.Password = Encoding.ASCII.GetString(session.InputBuffer.ToArray());
                    session.InputBuffer.Position = 0;
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

                //Mask Passwords
                EchoToClient(session, new byte[] { 0x2A });

                session.InputBuffer.Write(inputBytes);
            }
        }

        public void MainMenuDisplay(UserSession session, Dictionary<string, MbbsModule> modules)
        {
            EchoToClient(session, "\r\n\r\n|GREEN|Please select one of the following:|RESET|\r\n\r\n".EncodeToANSIArray());

            var currentMenuItem = 0;
            foreach (var m in modules)
            {
                EchoToClient(session, $"   |CYAN|{currentMenuItem}|YELLOW| ... {m.Value.ModuleDescription}\r\n".EncodeToANSIArray());
            }

            EchoToClient(session, "\r\n|GREEN|Main Menu|RESET|\r\n".EncodeToANSIArray());
            EchoToClient(session, "|CYAN|Make your selection (X to exit):|RESET| ".EncodeToANSIArray());
            session.SessionState = EnumSessionState.MainMenuInput;
        }

        public void MainMenuInput(UserSession session, Dictionary<string, MbbsModule> modules)
        {
            if (session.DataFromClient.Count > 0)
            {
                session.DataFromClient.TryDequeue(out var inputBytes);
                EchoToClient(session, inputBytes);

                if (inputBytes[0] == 0x58 || inputBytes[0] == 0x78)
                {
                    session.SessionState = EnumSessionState.LoggingOff;
                    return;
                }

                if (inputBytes[0] == 0xD)
                {
                    if (!int.TryParse(Encoding.ASCII.GetString(session.InputBuffer.ToArray()),
                        out var selectedMenuItem))
                    {
                        session.SessionState = EnumSessionState.MainMenuDisplay;

                        //Clear the Input Buffer
                        session.InputBuffer.Position = 0;
                        session.InputBuffer.SetLength(0);
                        return;
                    }

                    var selectedModule = modules.ElementAt(selectedMenuItem);
                    session.ModuleIdentifier = selectedModule.Value.ModuleIdentifier;
                    session.SessionState = EnumSessionState.EnteringModule;
                    session.DataToClient.Enqueue(new byte[] { 0x1B, 0x5B, 0x32, 0x4A });
                    session.DataToClient.Enqueue(new byte[] { 0x1B, 0x5B, 0x48 });

                    //Clear the Input Buffer
                    session.InputBuffer.Position = 0;
                    session.InputBuffer.SetLength(0);
                }

                session.InputBuffer.Write(inputBytes);
            }
        }
    }
}
