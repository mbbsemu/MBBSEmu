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
        private readonly List<string> rloginStrings = new List<string>();
        private readonly MemoryStream memoryStream = new MemoryStream();

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

            if (rloginStrings.Count != 3)
            {
                return false;
            }

            // we have 3 strings, pick out username and launch appropriately
            Username = rloginStrings.First(s => !string.IsNullOrEmpty(s));

            rloginStrings.Clear();

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

            return true;
        }

        protected override (byte[], int) ProcessIncomingClientData(byte[] clientData, int bytesReceived)
        {
            if (SessionState != EnumSessionState.Negotiating)
            {
                return (clientData, bytesReceived);
            }

            for (int i = 0; i < bytesReceived; ++i)
            {
                if (ProcessIncomingByte(clientData[i]))
                {
                    // return whatever data we may have left in the packet as client data
                    return (clientData.Take(i + 1).ToArray(), bytesReceived - i - 1);
                }
            }

            return (null, 0);
        }
    }
}
