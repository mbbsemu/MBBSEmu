using MBBSEmu.HostProcess;
using MBBSEmu.Logging;
using NLog;
using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;

namespace MBBSEmu.Telnet
{
    public class TelnetSession : Session.UserSession
    {
        protected static readonly Logger _logger = LogManager.GetCurrentClassLogger(typeof(CustomLogger));

        private readonly Socket _telnetConnection;
        private readonly MbbsHost _host;

        private readonly Thread _sendThread;
        private readonly Thread _receiveThread;

        public TelnetSession(Socket telnetConnection, MbbsHost host) : base(telnetConnection.RemoteEndPoint.ToString())
        {
            _host = host;
            _telnetConnection = telnetConnection;
            _telnetConnection.ReceiveTimeout = (1000 * 60) * 5;
            _telnetConnection.ReceiveBufferSize = 128;
            
            //Start Listeners & Senders
            _sendThread = new Thread(SendWorker);
            _sendThread.Start();
            _receiveThread = new Thread(ReceiveWorker);
            _receiveThread.Start();

            //Add this Session to the Host
            _host.AddSession(this);

            Enter();
        }

        private void Enter()
        {
            //Disable Local Telnet Echo on Client
            DataToClient.Enqueue(new byte[] {0xFF, 0xFB, 0x01});
            //Client will respond with 3 byte IAC
            byte[] terminalResponse = new byte[3];
            var bytesReceived = _telnetConnection.Receive(terminalResponse, 0, 3, SocketFlags.None);

            //Kick off Entry
            _host.Run("GWWARROW", "sttrou", this);
            _host.Run("GWWARROW", "stsrou", this);
        }

        /// <summary>
        ///     Send a Byte Array to the client
        /// </summary>
        /// <param name="dataToSend"></param>
        private void SendWorker()
        {
            while (_telnetConnection.Connected)
            {
                if (DataToClient.TryDequeue(out var sendBuffer))
                {
                    var bytesSent = _telnetConnection.Send(sendBuffer, SocketFlags.None, out var socketState);
                    ValidateSocketState(socketState);
                }
                Thread.Sleep(100);
            }
        }

        private void ReceiveWorker()
        {
            using var msReceiveBuffer = new MemoryStream();
            var characterBuffer = new byte[1];
            while (_telnetConnection.Connected)
            {
                Span<byte> characterSpan = characterBuffer;
                
                var bytesReceived = _telnetConnection.Receive(characterSpan, SocketFlags.None, out var errorCode);
                ValidateSocketState(errorCode);
                
                msReceiveBuffer.Write(characterSpan);

                //Only Send when characters are LF, for now
                if (characterSpan[0] == 0xA)
                {
                    DataFromClient.Enqueue(msReceiveBuffer.ToArray());
                    msReceiveBuffer.Position = 0;
                    msReceiveBuffer.SetLength(0);
                    continue;
                }
                
                Thread.Sleep(100);
            }
        }

        /// <summary>
        ///     Validates SocketError returned from an operation doesn't put the socket in an error state
        /// </summary>
        /// <param name="socketError"></param>
        private static void ValidateSocketState(SocketError socketError)
        {
            if (socketError != SocketError.Success)
                throw new Exception($"Socket Error: {Enum.GetName(typeof(SocketError), socketError)}");
        }
    }
}
