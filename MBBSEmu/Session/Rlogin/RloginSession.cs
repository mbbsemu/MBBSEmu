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

            int[] nulls = clientData.FindIndexes(bytesReceived, b => b == 0).ToArray();
            if (nulls.Length < 2)
            {
                CloseSocket($"Failed to find at least 2 \\0 in the RLogin negotiation {nulls}");
                return (null, 0);
            }

            ReadOnlySpan<byte> bufferSpan = clientData;
            Username = Encoding.ASCII.GetString(bufferSpan.Slice(0, nulls[0]));
            var second = Encoding.ASCII.GetString(bufferSpan.Slice(nulls[0] + 1, nulls[1] - nulls[0] - 1));
            // if not sent first, grab 2nd and use that
            if (String.IsNullOrEmpty(Username) && !String.IsNullOrEmpty(second))
            {
                Username = second;
            }

            int remainingData = bytesReceived - nulls[^1] - 1;
            _logger.Info($"Rlogin For User: {Username}");

            if (!string.IsNullOrEmpty(ModuleIdentifier))
            {
                CurrentModule = _host.GetModule(ModuleIdentifier);
                SessionState = EnumSessionState.EnteringModule;
            }
            else
            {
                SessionState = EnumSessionState.MainMenuDisplay;
            }

            //Send 0 byte to ACK
            Send(new byte[] {0x0});

            if (remainingData > 0)
            {
                return (bufferSpan.Slice(nulls[^1] + 1).ToArray(), remainingData);
            }
            else
            {
                return (null, 0);
            }
        }
    }
}
