using MBBSEmu.HostProcess;
using MBBSEmu.Logging;
using NLog;
using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using MBBSEmu.Session;

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
            SessionState = EnumSessionState.Negotiating;

            //Start Listeners & Senders
            _sendThread = new Thread(SendWorker);
            _sendThread.Start();
            _receiveThread = new Thread(ReceiveWorker);
            _receiveThread.Start();


            //Disable Local Echo
            DataToClient.Enqueue(new byte[] { 0xFF, 0xFB, 0x01 });

            //Add this Session to the Host
            _host.AddSession(this);

            Enter();
        }

        private void Enter()
        {
            ModuleIdentifier = "GWWARROW";
            SessionState = EnumSessionState.EnteringModule;
            //Kick off Entry
            //_host.Run("GWWARROW", "sttrou", this);
            //_host.Run("GWWARROW", "stsrou", this);
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
            var characterBuffer = new byte[256];
            while (_telnetConnection.Connected)
            {
                Span<byte> characterBufferSpan = characterBuffer;
                var bytesReceived = _telnetConnection.Receive(characterBufferSpan, SocketFlags.Partial, out var socketState);
                ValidateSocketState(socketState);

                //IAC Responses
                if (characterBufferSpan[0] == 0xFF)
                {
                    ParseIAC(characterBufferSpan.Slice(0, bytesReceived));
                }
                else
                {
                    DataFromClient.Enqueue(characterBufferSpan.Slice(0, bytesReceived).ToArray());
                }
                
                Thread.Sleep(1);
            }
        }

        /// <summary>
        ///     Parses IAC Commands Received by 
        /// </summary>
        /// <param name="iacResponse"></param>
        private void ParseIAC(ReadOnlySpan<byte> iacResponse)
        {
            for (var i = 0; i < iacResponse.Length; i+= 3)
            {
                if (iacResponse[i] == 0)
                    return;

                if(iacResponse[i] != 0xFF)
                    throw new Exception("Invalid IAC?");

                var iacVerb = (EnumIacVerbs) iacResponse[i + 1];
                var iacOption = (EnumIacOptions) iacResponse[i + 2];

                _logger.Info($"Channel {Channel}: IAC {iacVerb} {iacOption}");

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
