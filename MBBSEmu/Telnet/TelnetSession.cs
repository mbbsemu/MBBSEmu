using MBBSEmu.DependencyInjection;
using MBBSEmu.HostProcess;
using MBBSEmu.Session;
using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Threading;

namespace MBBSEmu.Telnet
{
    public class TelnetSession : UserSession
    {
        private readonly ILogger _logger;
        private readonly IMbbsHost _host;

        private readonly Socket _telnetConnection;
        private readonly Thread _sendThread;
        private readonly Thread _receiveThread;

        private bool _sentServerIac;

        public TelnetSession(Socket telnetConnection) : base(telnetConnection.RemoteEndPoint.ToString())
        {
            _host = ServiceResolver.GetService<IMbbsHost>();
            _logger = ServiceResolver.GetService<ILogger>();
            _telnetConnection = telnetConnection;
            _telnetConnection.ReceiveTimeout = (1000 * 60) * 5;
            _telnetConnection.ReceiveBufferSize = 128;
            
            SessionState = EnumSessionState.Negotiating;

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
            ModuleIdentifier = "GWWARROW";
            SessionState = EnumSessionState.EnteringModule;
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
                    if (!_sentServerIac)
                    {
                        _sentServerIac = true;
                        DataFromClient.Enqueue(new byte[] { 0xFF, (byte)EnumIacVerbs.DO, (byte)EnumIacOptions.BinaryTransmission });
                    }
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

                var responseVerb = EnumIacVerbs.None;
                var responseOption = EnumIacOptions.None;

                switch (iacOption)
                {
                    case EnumIacOptions.BinaryTransmission:
                    {
                        responseOption = EnumIacOptions.BinaryTransmission;
                        responseVerb = iacVerb switch
                        {
                            EnumIacVerbs.WILL => EnumIacVerbs.DO,
                            EnumIacVerbs.DO => EnumIacVerbs.WILL,
                            _ => EnumIacVerbs.None
                        };

                        break;
                    }
                    case EnumIacOptions.Echo:
                    {
                        responseOption = EnumIacOptions.Echo;
                        responseVerb = iacVerb switch
                        {
                            EnumIacVerbs.DO => EnumIacVerbs.WONT,
                            _ => EnumIacVerbs.None
                        };
                        break;
                    }
                    case EnumIacOptions.NegotiateAboutWindowSize:
                    {
                        responseOption = EnumIacOptions.NegotiateAboutWindowSize;
                        responseVerb = iacVerb switch
                        {
                            EnumIacVerbs.WILL => EnumIacVerbs.WONT,
                            _ => EnumIacVerbs.None
                        };

                        break;
                    }
                    case EnumIacOptions.TerminalSpeed:
                    {
                        responseOption = EnumIacOptions.TerminalSpeed;
                        responseVerb = iacVerb switch
                        {
                            EnumIacVerbs.WILL => EnumIacVerbs.WONT,
                            _ => EnumIacVerbs.None
                        };

                        break;
                    }
                    case EnumIacOptions.TerminalType:
                    {
                        responseOption = EnumIacOptions.TerminalType;
                        responseVerb = iacVerb switch
                        {
                            EnumIacVerbs.WILL => EnumIacVerbs.WONT,
                            _ => EnumIacVerbs.None
                        };

                        break;
                    }
                    case EnumIacOptions.NewEnvironmentOption:
                    {
                        responseOption = EnumIacOptions.NewEnvironmentOption;
                        responseVerb = iacVerb switch
                        {
                            EnumIacVerbs.WILL => EnumIacVerbs.WONT,
                            _ => EnumIacVerbs.None
                        };
                        break;
                    }
                }

                if(responseOption != EnumIacOptions.None && responseVerb != EnumIacVerbs.None)
                    DataFromClient.Enqueue(new byte[] { 0xFF, (byte)responseVerb, (byte)responseOption });
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
