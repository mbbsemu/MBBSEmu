using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using MBBSEmu.Logging;
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
        public TelnetServer()
        {

            //Setup Listener
            _ipEndPoint = new IPEndPoint(IPAddress.Any, 21);
            _listenerSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _listenerSocket.Bind(_ipEndPoint);

            _telnetSessions = new List<TelnetSession>();
        }

        public void Start()
        {
            _listenerSocket.Listen(10);
            _isRunning = true;
        }

        public void Listen()
        {
            while (_isRunning)
            {
                var client = _listenerSocket.Accept();
                var session = new TelnetSession(client);
                _telnetSessions.Add(session);
            }
        }
    }
}
