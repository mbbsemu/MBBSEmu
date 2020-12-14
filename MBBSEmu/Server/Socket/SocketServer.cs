using MBBSEmu.HostProcess;
using MBBSEmu.Memory;
using MBBSEmu.Session;
using MBBSEmu.Session.Enums;
using MBBSEmu.Session.Rlogin;
using MBBSEmu.Session.Telnet;
using NLog;
using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text.Json;

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
        private readonly AppSettings _configuration;

        private System.Net.Sockets.Socket _listenerSocket;
        private EnumSessionType _sessionType;
        private string _moduleIdentifier;
        private readonly PointerDictionary<SessionBase> _channelDictionary;

        public SocketServer(ILogger logger, IMbbsHost host, AppSettings configuration, PointerDictionary<SessionBase> channelDictionary)
        {
            _logger = logger;
            _host = host;
            _configuration = configuration;
            _channelDictionary = channelDictionary;
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

        private void OnNewConnection(IAsyncResult asyncResult) 
        {
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
                        if (_configuration.IPLocationAllow != null)
                        {
                            var ipAllowed = false;
                            var ipCountry = GetIP2Location(((IPEndPoint)client.RemoteEndPoint).Address.ToString());
                            _logger.Info($"Response from IP2LOCATION: {ipCountry}");

                            if (ipCountry == _configuration.IPLocationAllow || ipCountry == "-" || ipCountry == null) // "-" allows private ranges 10.x, 192.168.x etc., fail open
                                ipAllowed = true;

                            //Deny connection if not a valid IP range
                            if (!ipAllowed)
                            {
                                _logger.Info(
                                    $"Rejecting incoming telnet connection from unauthorized country -- Allowed: {_configuration.IPLocationAllow}, Remote Host: {client.RemoteEndPoint}");
                                client.Close();
                                return;
                            }
                        }

                        _logger.Info($"Accepting incoming Telnet connection from {client.RemoteEndPoint}...");
                        var session = new TelnetSession(_logger, client, _configuration);
                        _host.AddSession(session);
                        session.Start();
                        break;
                    }
                case EnumSessionType.Rlogin:
                    {
                        if (((IPEndPoint)client.RemoteEndPoint).Address.ToString() != _configuration.RloginoRemoteIP)
                        {
                            _logger.Info(
                                $"Rejecting incoming Rlogin connection from unauthorized Remote Host: {client.RemoteEndPoint}");
                            client.Close();
                            return;
                        }

                        _logger.Info($"Accepting incoming Rlogin connection from {client.RemoteEndPoint}...");
                        var session = new RloginSession(_host, _logger, client, _channelDictionary, _configuration, _moduleIdentifier);
                        _host.AddSession(session);
                        session.Start();
                        break;
                    }
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private string GetIP2Location(string ipAddress)
        {
            const string url = "https://api.ip2location.com/v2/";
            var urlParameters = $"?ip={ipAddress}&key={_configuration.IPLocationKey}&package=WS1";
            var client = new HttpClient();
            var ip2LocationResponse = new IP2Location();
            
            client.BaseAddress = new Uri(url);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            var response = client.GetAsync(urlParameters).Result;
            
            if (response.IsSuccessStatusCode)
            {
                ip2LocationResponse = JsonSerializer.Deserialize<IP2Location>(response.Content.ReadAsStringAsync().Result);
            }
            else
            {
                _logger.Info($"{(int)response.StatusCode} ({response.ReasonPhrase}) -- allowing connection");
            }

            client.Dispose();
            if (ip2LocationResponse != null) return ip2LocationResponse.CountryCode;
        }

        // JSON from IP2LOCATION
        private class IP2Location
        {
            public string CountryCode { get; set; }
            public int CreditsConsumed { get; set; }
        }
    }
}
