using System;
using MBBSEmu.Resources;
using MBBSEmu.Session;
using System.Text;
using MBBSEmu.HostProcess.Formatters;

namespace MBBSEmu.HostProcess
{
    public class MbbsRoutines : IMbbsRoutines
    {
        private readonly IResourceManager _resourceManager;
        private readonly IANSIFormatter _ansiFormatter;

        public MbbsRoutines(IResourceManager resourceManager, IANSIFormatter ansiFormatter)
        {
            _resourceManager = resourceManager;
            _ansiFormatter = ansiFormatter;
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
            session.DataToClient.Enqueue(dataToEcho);
        }

        /// <summary>
        ///     Shows the Login Screen to a new User Session
        /// </summary>
        /// <param name="session"></param>
        public void ShowLogin(UserSession session)
        {
            

            session.DataToClient.Enqueue(_resourceManager.GetResource("MBBSEmu.Assets.login.ans").ToArray());
            session.SessionState = EnumSessionState.DisplayingLoginUsername;
        }

        public void DisplayLoginUsername(UserSession session)
        {
            var loginInstructionText =_ansiFormatter.Encode("\r\n|YELLOW|Enter Username or enter \"|B|NEW|!B|\" to create a new Account\r\n");
            session.DataToClient.Enqueue(Encoding.ASCII.GetBytes(loginInstructionText));
            var usernameText = _ansiFormatter.Encode("|B||WHITE|Username:|!B| ");
            session.DataToClient.Enqueue(Encoding.ASCII.GetBytes(usernameText));
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
            var passwordText = _ansiFormatter.Encode("\r\n|B||WHITE|Password:|!B| ");
            session.DataToClient.Enqueue(Encoding.ASCII.GetBytes(passwordText));
            session.SessionState = EnumSessionState.AuthenticatingPassword;
        }

        public void AuthenticatingPassword(UserSession session)
        {
            //Process Data
            if (session.DataFromClient.Count > 0)
            {
                session.DataFromClient.TryDequeue(out var inputBytes);

                EchoToClient(session, new byte[] { 0x2A });

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
    }
}
