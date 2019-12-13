using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using MBBSEmu.HostProcess;
using MBBSEmu.Logging;
using MBBSEmu.Module;
using NLog;

namespace MBBSEmu.Telnet
{
    public class TelnetServer
    {
        protected static readonly Logger _logger = LogManager.GetCurrentClassLogger(typeof(CustomLogger));

        private readonly IPEndPoint _ipEndPoint;
        private readonly Socket _listenerSocket;
        private readonly List<TelnetSession> _telnetSessions;
        private bool _isRunning;
        private readonly MbbsHost _host;
        private readonly Thread _listenerThread;

        public TelnetServer(MbbsHost host)
        {
            _host = host;
            

            //Setup Listener
            _ipEndPoint = new IPEndPoint(IPAddress.Any, 23);
            _listenerSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _listenerSocket.Bind(_ipEndPoint);
            _listenerThread = new Thread(Listen);
            _telnetSessions = new List<TelnetSession>();
        }

        public void Start()
        {
            _listenerSocket.Listen(10);
            _isRunning = true;
            _listenerThread.Start();
        }

        public void Listen()
        {
            while (_isRunning)
            {
                var client = _listenerSocket.Accept();
                _logger.Info($"Acceping incoming connection from {client.RemoteEndPoint}...");
                var session = new TelnetSession(client, _host);
                _telnetSessions.Add(session);
            }
        }
    }
}
