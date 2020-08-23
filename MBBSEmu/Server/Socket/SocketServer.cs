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
using MBBSEmu.Session.Enums;

namespace MBBSEmu.Server.Socket
{
    /// <summary>
    ///     Socket Server that handles setting up the TCP socket and accepting incoming connections
    ///
    ///     This class is used for both Telnet and Rlogin Listeners
    /// </summary>
    public class SocketServer : ISocketServer
    {
        private readonly ILogger _logger;
        private readonly IMbbsHost _host;
        private readonly IConfiguration _configuration;

        private System.Net.Sockets.Socket _listenerSocket;
        private EnumSessionType _sessionType;
        private string _moduleIdentifier;

        public SocketServer(ILogger logger, IMbbsHost host, IConfiguration configuration)
        {
            _logger = logger;
            _host = host;
            _configuration = configuration;
        }

        public void Start(EnumSessionType sessionType, int port, string moduleIdentifier = null)
        {
            _sessionType = sessionType;
            _moduleIdentifier = moduleIdentifier;

            //Setup Listener
            var ipEndPoint = new IPEndPoint(IPAddress.Any, port);
            _listenerSocket = new System.Net.Sockets.Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            _listenerSocket.Bind(ipEndPoint);
            _listenerSocket.Listen(10);
            _listenerSocket.BeginAccept(OnNewConnection, this);
        }

        public void Stop()
        {
            _listenerSocket.Close();
        }

        private void OnNewConnection(IAsyncResult asyncResult) {
            System.Net.Sockets.Socket client;
            try
            {
                client = _listenerSocket.EndAccept(asyncResult);
            } catch (ObjectDisposedException) {
                // ignore, happens during shutdown
                return;
            }

            client.NoDelay = true;

            _listenerSocket.BeginAccept(OnNewConnection, this);

            switch (_sessionType)
            {
                case EnumSessionType.Telnet:
                    {
                        _logger.Info($"Accepting incoming Telnet connection from {client.RemoteEndPoint}...");
                        var session = new TelnetSession(client);
                        _host.AddSession(session);
                        session.Start();
                        break;
                    }
                case EnumSessionType.Rlogin:
                    {
                        if (((IPEndPoint)client.RemoteEndPoint).Address.ToString() != _configuration["Rlogin.RemoteIP"])
                        {
                            _logger.Info(
                                $"Rejecting incoming Rlogin connection from unauthorized Remote Host: {client.RemoteEndPoint}");
                            client.Close();
                            return;
                        }

                        _logger.Info($"Accepting incoming Rlogin connection from {client.RemoteEndPoint}...");
                        var session = new RloginSession(_host, client, _moduleIdentifier);
                        _host.AddSession(session);
                        session.Start();
                        break;
                    }
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}
