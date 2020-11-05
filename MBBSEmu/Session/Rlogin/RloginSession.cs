using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using MBBSEmu.HostProcess;
using MBBSEmu.Memory;
using MBBSEmu.Session.Enums;
using NLog;

namespace MBBSEmu.Session.Rlogin
{
    /// <summary>
    ///     Class for handling inbound RLogin Connections
    /// </summary>
    public class RloginSession : SocketSession
    {
        private readonly IMbbsHost _host;
        private readonly PointerDictionary<SessionBase> _channelDictionary;
        private readonly List<string> rloginStrings = new List<string>();
        private readonly MemoryStream memoryStream = new MemoryStream(1024);

        public readonly string ModuleIdentifier;

        public RloginSession(IMbbsHost host, ILogger logger, Socket rloginConnection, PointerDictionary<SessionBase> channelDictionary, string moduleIdentifier = null) : base(logger, rloginConnection)
        {
            _host = host;
            ModuleIdentifier = moduleIdentifier;
            _channelDictionary = channelDictionary;
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

            //Check if user is already logged in
            if (_channelDictionary.Values.Any(s => string.Equals(s.Username,
                rloginStrings.First(s => !string.IsNullOrEmpty(s)), StringComparison.CurrentCultureIgnoreCase)))
            {
                _logger.Info($"Duplicate User");
                return false;
            }
            
            // we have 3 strings, pick out username and launch appropriately
            Username = rloginStrings.First(s => !string.IsNullOrEmpty(s));

            rloginStrings.Clear();

            _logger.Info($"Rlogin For User: {Username}");

            if (!string.IsNullOrEmpty(ModuleIdentifier))
            {
                CurrentModule = _host.GetModule(ModuleIdentifier);
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
                return (clientData, bytesReceived);
            }

            for (var i = 0; i < bytesReceived; ++i)
            {
                if (ProcessIncomingByte(clientData[i]))
                {
                    // return whatever data we may have left in the packet as client data
                    var remaining = bytesReceived - i - 1;
                    return (clientData.TakeLast(remaining).ToArray(), remaining);
                }
            }

            return (null, 0);
        }
    }
}
