using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using MBBSEmu.Extensions;
using MBBSEmu.HostProcess;
using MBBSEmu.Memory;
using MBBSEmu.Session.Enums;
using MBBSEmu.TextVariables;
using NLog;

namespace MBBSEmu.Session.Rlogin
{
    /// <summary>
    ///     Class for handling inbound RLogin Connections
    /// </summary>
    public class RloginSession : SocketSession
    {
        private readonly PointerDictionary<SessionBase> _channelDictionary;
        private readonly AppSettings _configuration;
        private readonly List<string> rloginStrings = new List<string>();
        private readonly MemoryStream memoryStream = new MemoryStream(1024);

        public readonly string ModuleIdentifier;

        public RloginSession(IMbbsHost host, ILogger logger, Socket rloginConnection, PointerDictionary<SessionBase> channelDictionary, AppSettings configuration, ITextVariableService textVariableService, string moduleIdentifier = null) : base(host, logger, rloginConnection, textVariableService)
        {
            ModuleIdentifier = moduleIdentifier;
            _channelDictionary = channelDictionary;
            _configuration = configuration;
            SessionType = EnumSessionType.Rlogin;
            SessionState = EnumSessionState.Negotiating;
        }

        public override void Stop() {
            base.Stop();

            TransparentMode = false;
        }

        /// <summary>
        ///     Scans incoming Rlogin bytes looking for the first three nulls, accumulating them
        ///     in the rloginStrings collection. Once found, picks out the first non-empty string
        ///     as the UserName and sets the proper Session states.
        /// </summary>
        /// <returns>Returns true when rlogin analysis has completed</returns>
        private bool ProcessIncomingByte(byte b)
        {
            if (b != 0)
            {
                memoryStream.WriteByte(b);
                return false;
            }

            rloginStrings.Add(Encoding.ASCII.GetString(memoryStream.ToArray()));
            memoryStream.SetLength(0);

            if (rloginStrings.Count < 3 || rloginStrings.Count(s => !string.IsNullOrEmpty(s)) < 2 )
            {
                return false;
            }

            //Check to see if there is an available channel
            if (_channelDictionary.Count > _configuration.BBSChannels)
            {
                var channelFullMsg =
                    $"\r\n|RED||B|{_configuration.BBSTitle} has reached the maximum number of users: {_configuration.BBSChannels} -- Please try again later.\r\n|RESET|"
                        .EncodeToANSIArray();
                Send(channelFullMsg);
                SessionState = EnumSessionState.LoggedOff;
                return false;
            }

            //Check if user is already logged in
            if (_channelDictionary.Values.Any(s => string.Equals(s.Username,
                rloginStrings.First(s => !string.IsNullOrEmpty(s)), StringComparison.CurrentCultureIgnoreCase)))
            {
                _logger.Info($"RLogin -- User already logged in");
                var duplicateLoginMsg =
                    $"\r\n|RED||B|Duplicate user already logged in -- only 1 connection allowed per user.\r\n|RESET|"
                        .EncodeToANSIArray();
                Send(duplicateLoginMsg);
                SessionState = EnumSessionState.LoggedOff;
                return false;
            }

            // we have 3 strings, pick out username and launch appropriately
            Username = rloginStrings.First(s => !string.IsNullOrEmpty(s));

            rloginStrings.Clear();

            _logger.Info($"Rlogin For User: {Username}");

            if (!string.IsNullOrEmpty(ModuleIdentifier))
            {
                CurrentModule = _mbbsHost.GetModule(ModuleIdentifier);
                InputBuffer.WriteByte((byte)CurrentModule.MenuOptionKey[0]);
                SessionState = EnumSessionState.RloginEnteringModule;
            }
            else
            {
                SessionState = EnumSessionState.LoginRoutines;
            }

            //Send 0 byte to ACK
            Send(new byte[] {0x0});

            return true;
        }

        protected override (byte[], int) ProcessIncomingClientData(byte[] clientData, int bytesReceived)
        {
             if (SessionState != EnumSessionState.Negotiating)
            {
                //Ugly WG3NT RLOGIN Extra Data Hack
                if (bytesReceived == 12 && clientData[5] == 24)
                    return (null, 0);

                return (clientData, bytesReceived);
            }

            for (var i = 0; i < bytesReceived; ++i)
            {
                if (ProcessIncomingByte(clientData[i]))
                {
                    // data left in the packet seems to do more harm than good, so we are tossing it, but adding to debug log 
                    var remaining = bytesReceived - i - 1;
                    _logger.Debug($"Ignoring extra rlogin data: \"{System.Text.Encoding.ASCII.GetString(clientData.TakeLast(remaining).ToArray())}\"");
                }
            }

            return (null, 0);
        }
    }
}
