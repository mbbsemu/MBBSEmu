using MBBSEmu.HostProcess;
using MBBSEmu.Logging;
using NLog;
using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace MBBSEmu.Telnet
{
    public class TelnetSession : Session.UserSession
    {
        protected static readonly Logger _logger = LogManager.GetCurrentClassLogger(typeof(CustomLogger));

        private Socket _telnetConnection;
        private Thread _sessionThread;
        private bool _isTyping;
        private MbbsHost _host;

        private Thread SendThread;
        private Thread ReceiveThread;

        public TelnetSession(Socket telnetConnection, MbbsHost host)
        {
            _host = host;
            _telnetConnection = telnetConnection;
            _telnetConnection.ReceiveTimeout = (1000 * 60) * 5;
            _telnetConnection.ReceiveBufferSize = 128;
            _sessionThread = new Thread(SessionWorker);
            _sessionThread.Start();

            SendThread = new Thread(SendWorker);
            ReceiveThread = new Thread(ReceiveWorker);

            _host.AddSession(this);
        }

        private void SessionWorker()
        {
            try
            {
                //Disable Local Telnet Echo on Client
                DataToClient.Enqueue(new byte[] {0xFF, 0xFB, 0x01});
                //Client will respond with 3 byte IAC
                byte[] terminalResponse = new byte[3];
                var bytesReceived = _telnetConnection.Receive(terminalResponse, 0, 3, SocketFlags.None);

                //Kick off Entry
                _host.Run("GWWARROW", "sttrou", this);
                _host.Run("GWWARROW", "stsrou", this);

                while (true)
                    Thread.Sleep(1);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
            finally
            {
                //Only do this if the client is actually still connected!
                if (_telnetConnection.Connected)
                {
                    //Send IAC for Logout (Client Do Close)
                    DataToClient.Enqueue(new byte[] { 0xFF, 0xFD, 0x12 });
                    //Client will respond with 3 byte IAC
                    byte[] terminalResponse = new byte[3];
                    var bytesReceived = _telnetConnection.Receive(terminalResponse, 0, 3, SocketFlags.None);
                }

                //Clean up the socket
                _telnetConnection.Dispose();
            }
            
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
                Thread.Sleep(250);
            }
        }

        private void ReceiveWorker()
        {
            using var msReceiveBuffer = new MemoryStream();
            var characterBuffer = new byte[1];
            while (_telnetConnection.Connected)
            {
                Span<byte> characterSpan = characterBuffer;
                var bytesReceived = _telnetConnection.Receive(characterSpan);
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

        private static void ValidateSocketState(SocketError socketError)
        {
            if (socketError != SocketError.Success)
                throw new Exception($"Socket Error: {Enum.GetName(typeof(SocketError), socketError)}");
        }
    }
}
