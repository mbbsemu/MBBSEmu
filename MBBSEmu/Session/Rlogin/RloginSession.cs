using MBBSEmu.DependencyInjection;
using MBBSEmu.Extensions;
using MBBSEmu.HostProcess;
using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using MBBSEmu.Session.Enums;

namespace MBBSEmu.Session.Rlogin
{
    /// <summary>
    ///     Class for handling inbound RLogin Connections
    /// </summary>
    public class RloginSession : SocketSession
    {
        private readonly IMbbsHost _host;
        public readonly string ModuleIdentifier;

        public RloginSession(IMbbsHost host, Socket rloginConnection, string moduleIdentifier = null) : base(rloginConnection)
        {
            _host = host;
            ModuleIdentifier = moduleIdentifier;
            SessionType = EnumSessionType.Rlogin;
            SessionState = EnumSessionState.Negotiating;
        }

        public override void Stop() {
            base.Stop();

            TransparentMode = false;
        }

        protected override (byte[], int) ProcessIncomingClientData(byte[] clientData, int bytesReceived)
        {
            if (SessionState != EnumSessionState.Negotiating)
            {
                return (clientData, bytesReceived);
            }

            var rloginStrings = Encoding.ASCII.GetString(clientData, 0, bytesReceived).Split('\0', StringSplitOptions.RemoveEmptyEntries);
            if (rloginStrings.Length == 0)
            {
                CloseSocket($"Invalid rlogin negotiation, didn't receive null terminated strings");
                return (null, 0);
            }
            // all we care about is the username, ignore the other fields
            Username = rloginStrings[0];

            _logger.Info($"Rlogin For User: {Username}");

            OutputEnabled = false;

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

            // ignore any extraneous data, since the client should wait until we ack back with
            // the zero byte
            return (null, 0);
        }
    }
}
