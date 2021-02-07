using MBBSEmu.HostProcess;
using MBBSEmu.Memory;
using MBBSEmu.Session;
using MBBSEmu.Session.Enums;
using MBBSEmu.Session.Rlogin;
using MBBSEmu.Session.Telnet;
using MBBSEmu.TextVariables;
using NLog;
using System;
using System.Net;
using System.Net.Sockets;

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
        private readonly ITextVariableService _textVariableService;
        private readonly AppSettings _configuration;

        private System.Net.Sockets.Socket _listenerSocket;
        private EnumSessionType _sessionType;
        private string _moduleIdentifier;
        private readonly PointerDictionary<SessionBase> _channelDictionary;

        public SocketServer(ILogger logger, IMbbsHost host, AppSettings configuration, ITextVariableService textVariableService, PointerDictionary<SessionBase> channelDictionary)
        {
            _logger = logger;
            _host = host;
            _configuration = configuration;
            _textVariableService = textVariableService;
            _channelDictionary = channelDictionary;
        }

        public void Start(EnumSessionType sessionType, string hostIpAddress, int port, string moduleIdentifier = null)
        {
            _sessionType = sessionType;
            _moduleIdentifier = moduleIdentifier;

            //Setup Listener
            var ipEndPoint = new IPEndPoint(IPAddress.Parse(hostIpAddress), port);
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
                        var session = new TelnetSession(_host, _logger, client, _configuration, _textVariableService);
                        _host.AddSession(session);
                        session.Start();
                        break;
                    }
                case EnumSessionType.Rlogin:
                    {
                        if (((IPEndPoint)client.RemoteEndPoint).Address.ToString() != _configuration.RloginRemoteIP)
                        {
                            _logger.Info(
                                $"Rejecting incoming Rlogin connection from unauthorized Remote Host: {client.RemoteEndPoint}");
                            client.Close();
                            return;
                        }

                        _logger.Info($"Accepting incoming Rlogin connection from {client.RemoteEndPoint}...");
                        var session = new RloginSession(_host, _logger, client, _channelDictionary, _configuration, _textVariableService, _moduleIdentifier);
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
