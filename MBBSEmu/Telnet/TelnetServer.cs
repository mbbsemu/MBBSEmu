using MBBSEmu.HostProcess;
using NLog;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace MBBSEmu.Telnet
{
    public class TelnetServer : ITelnetServer
    {
        private readonly ILogger _logger;
        private readonly IMbbsHost _host;

        private Socket _listenerSocket;
        private bool _isRunning;
        private Thread _listenerThread;

        public TelnetServer(IMbbsHost host, ILogger logger)
        {
            _host = host;
            _logger = logger;
        }

        public void Start()
        {
            //Setup Listener
            var ipEndPoint = new IPEndPoint(IPAddress.Loopback, 23);
            _listenerSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _listenerSocket.ReceiveBufferSize = 0x800;
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
                _logger.Info($"Acceping incoming connection from {client.RemoteEndPoint}...");
                var session = new TelnetSession(client);
            }
        }
    }
}
