using MBBSEmu.HostProcess;
using MBBSEmu.Session;
using MBBSEmu.Session.Rlogin;
using MBBSEmu.Session.Telnet;
using Microsoft.Extensions.Configuration;
using NLog;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace MBBSEmu.Server.Socket
{
    public class SocketServer : ISocketServer
    {
        private readonly ILogger _logger;
        private readonly IMbbsHost _host;
        private readonly IConfiguration _configuration;

        private System.Net.Sockets.Socket _listenerSocket;
        private bool _isRunning;
        private Thread _listenerThread;
        private EnumSessionType _sessionType;
        private string _moduleIdentifier;

        public SocketServer(IMbbsHost host, ILogger logger, IConfiguration configuration)
        {
            _host = host;
            _logger = logger;
            _configuration = configuration;
        }

        public void Start(EnumSessionType sessionType, int port, string moduleIdentifier = null)
        {
            _sessionType = sessionType;
            _moduleIdentifier = moduleIdentifier;

            //Setup Listener
            var ipEndPoint = new IPEndPoint(IPAddress.Any, port);
            _listenerSocket = new System.Net.Sockets.Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
            {
                ReceiveBufferSize = 0x800
            };
            _listenerSocket.Bind(ipEndPoint);
            _listenerThread = new Thread(ListenerThread);

            _listenerSocket.Listen(10);
            _isRunning = true;
            _listenerThread.Start();
        }

        public void Stop()
        {
            _isRunning = false;
            _listenerSocket.Dispose();
        }

        private void ListenerThread()
        {
            while (_isRunning)
            {
                var client = _listenerSocket.Accept();

                switch (_sessionType)
                {
                    case EnumSessionType.Telent:
                        {
                            _logger.Info($"Acceping incoming Telnet connection from {client.RemoteEndPoint}...");
                            var telnetSession = new TelnetSession(client);
                            break;
                        }
                    case EnumSessionType.Rlogin:
                        {
                            if (((IPEndPoint)client.RemoteEndPoint).Address.ToString() != _configuration["Rlogin.RemoteIP"])
                            {
                                _logger.Info(
                                    $"Rejecting incoming Rlogin connection from unauthorized Remote Host: {client.RemoteEndPoint}");
                                client.Dispose();
                                continue;
                            }

                            _logger.Info($"Acceping incoming Rlogin connection from {client.RemoteEndPoint}...");
                            var rloginSession = new RloginSession(client, _moduleIdentifier);
                            break;
                        }
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }
    }
}