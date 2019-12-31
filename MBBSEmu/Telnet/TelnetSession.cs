using MBBSEmu.DependencyInjection;
using MBBSEmu.Extensions;
using MBBSEmu.HostProcess;
using MBBSEmu.Session;
using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

        private int _iacPhase;
        private bool _iacComplete;

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
        }


        /// <summary>
        ///     Send a Byte Array to the client
        /// </summary>
        private void SendWorker()
        {
            while (_telnetConnection.IsConnected() && SessionState != EnumSessionState.LoggedOff)
            {
                if (DataToClient.TryDequeue(out var sendBuffer))
                {
                    var bytesSent = _telnetConnection.Send(sendBuffer, SocketFlags.None, out var socketState);
                    ValidateSocketState(socketState);
                }

                if (SessionState == EnumSessionState.LoggingOffProcessing)
                {
                    SessionState = EnumSessionState.LoggedOff;
                    return;
                }

                Thread.Sleep(5);
            }

            //Cleanup if the connection was dropped
            SessionState = EnumSessionState.LoggedOff;
        }

        private void ReceiveWorker()
        {
            using var msReceiveBuffer = new MemoryStream();
            var characterBuffer = new byte[256];
            while (_telnetConnection.IsConnected() && SessionState != EnumSessionState.LoggedOff)
            {

                Span<byte> characterBufferSpan = characterBuffer;
                var bytesReceived = _telnetConnection.Receive(characterBufferSpan, SocketFlags.Partial, out var socketState);
                ValidateSocketState(socketState);

                if(bytesReceived == 0)
                    continue;

                //IAC Responses
                if (characterBufferSpan[0] == 0xFF)
                {
                    ParseIAC(characterBufferSpan.Slice(0, bytesReceived));
                    if (_iacComplete)
                        SessionState = EnumSessionState.Unauthenticated;
                }
                else
                {
                    DataFromClient.Enqueue(characterBufferSpan.Slice(0, bytesReceived).ToArray());
                }
                Thread.Sleep(1);
            }

            //Cleanup if the connection was dropped
            SessionState = EnumSessionState.LoggedOff;
        }

        /// <summary>
        ///     Parses IAC Commands Received by 
        /// </summary>
        /// <param name="iacResponse"></param>
        private void ParseIAC(ReadOnlySpan<byte> iacResponse)
        {
            var _iacResponses = new List<IacResponse>();

            for (var i = 0; i < iacResponse.Length; i+= 3)
            {
                if (iacResponse[i] == 0)
                    return;

                if(iacResponse[i] != 0xFF)
                    throw new Exception("Invalid IAC?");

                var iacVerb = (EnumIacVerbs) iacResponse[i + 1];
                var iacOption = (EnumIacOptions) iacResponse[i + 2];

                _logger.Info($"Channel {Channel}: IAC {iacVerb} {iacOption}");

                switch (iacOption)
                {
                    case EnumIacOptions.BinaryTransmission:
                    {
                        switch (iacVerb)
                        {
                            case EnumIacVerbs.WILL:
                                _iacResponses.Add(new IacResponse(EnumIacVerbs.DO, EnumIacOptions.BinaryTransmission));
                                _iacComplete = true;
                                break;
                            default:
                                _logger.Warn($"Unhandled IAC Verb fpr {iacOption}: {iacVerb}");
                                break;
                        }
                        break;
                    }
                    case EnumIacOptions.Echo:
                    {
                        switch (iacVerb)
                        {
                            case EnumIacVerbs.DO:
                                _iacResponses.Add(new IacResponse(EnumIacVerbs.DONT, EnumIacOptions.Echo));
                                _iacResponses.Add(new IacResponse(EnumIacVerbs.WILL, EnumIacOptions.Echo));
                                break;
                            default:
                                _logger.Warn($"Unhandled IAC Verb fpr {iacOption}: {iacVerb}");
                                    break;
                        }

                        break;
                    }
                    case EnumIacOptions.NegotiateAboutWindowSize:
                    {
                        switch (iacVerb)
                        {
                            case EnumIacVerbs.WILL:
                                _iacResponses.Add(new IacResponse(EnumIacVerbs.WONT,
                                    EnumIacOptions.NegotiateAboutWindowSize));
                                break;
                            default:
                                _logger.Warn($"Unhandled IAC Verb fpr {iacOption}: {iacVerb}");
                                break;
                        }
                        break;
                    }
                    case EnumIacOptions.TerminalSpeed:
                    {
                        switch (iacVerb)
                        {
                            case EnumIacVerbs.WILL:
                                _iacResponses.Add(new IacResponse(EnumIacVerbs.WONT, EnumIacOptions.TerminalSpeed));
                                break;
                            default:
                                _logger.Warn($"Unhandled IAC Verb fpr {iacOption}: {iacVerb}");
                                break;
                        }
                        break;
                    }
                    case EnumIacOptions.TerminalType:
                    {
                        switch (iacVerb)
                        {
                            case EnumIacVerbs.WILL:
                                _iacResponses.Add(new IacResponse(EnumIacVerbs.WONT, EnumIacOptions.TerminalType));
                                break;
                            default:
                                _logger.Warn($"Unhandled IAC Verb fpr {iacOption}: {iacVerb}");
                                break;
                        }
                        break;
                    }
                    case EnumIacOptions.EnvironmentOption:
                    {
                        switch (iacVerb)
                        {
                            case EnumIacVerbs.WILL:
                                _iacResponses.Add(new IacResponse(EnumIacVerbs.WONT, EnumIacOptions.EnvironmentOption));
                                break;
                            default:
                                _logger.Warn($"Unhandled IAC Verb fpr {iacOption}: {iacVerb}");
                                break;
                        }
                        break;
                    }
                    case EnumIacOptions.SuppressGoAhead:
                    {
                        switch (iacVerb)
                        {
                            case EnumIacVerbs.WILL:
                                _iacResponses.Add(new IacResponse(EnumIacVerbs.DO, EnumIacOptions.SuppressGoAhead));
                                break;
                            case EnumIacVerbs.DO:
                                _iacResponses.Add(new IacResponse(EnumIacVerbs.WILL, EnumIacOptions.SuppressGoAhead));
                                break;
                            default:
                                _logger.Warn($"Unhandled IAC Verb fpr {iacOption}: {iacVerb}");
                                break;
                        }
                        break;
                    }
                }
            }

            if(_iacPhase == 0 && _iacResponses.All(x => x.Option != EnumIacOptions.BinaryTransmission))
            {
                _iacResponses.Add(new IacResponse(EnumIacVerbs.DO, EnumIacOptions.BinaryTransmission));
            }

            _iacPhase++;

            using var msIacToSend = new MemoryStream();
            foreach(var resp in _iacResponses)
                msIacToSend.Write(resp.ToArray());

            if (msIacToSend.Length == 0)
            {
                _iacComplete = true;
                return;
            }
            
            DataToClient.Enqueue(msIacToSend.ToArray());
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
