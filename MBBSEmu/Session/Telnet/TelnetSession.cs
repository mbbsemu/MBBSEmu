using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using MBBSEmu.DependencyInjection;
using MBBSEmu.Extensions;
using MBBSEmu.HostProcess;
using NLog;

namespace MBBSEmu.Session.Telnet
{
    /// <summary>
    ///     Class for handling inbound Telnet Connections
    /// </summary>
    public class TelnetSession : SessionBase
    {
        private readonly ILogger _logger;
        private readonly IMbbsHost _host;

        private readonly Socket _telnetConnection;
        private readonly Thread _sendThread;
        private readonly Thread _receiveThread;
        private readonly byte[] socketReceiveBuffer = new byte[256];

        private int _iacPhase;

        //Tracks Responses We've already sent -- prevents looping
        private readonly List<IacResponse> _iacSentResponses = new List<IacResponse>();

        public TelnetSession(Socket telnetConnection) : base(telnetConnection.RemoteEndPoint.ToString())
        {
            SessionType = EnumSessionType.Telent;
            SendToClientMethod = Send;
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

            Send(new byte[] { 0x1B, 0x5B, 0x32, 0x4A });
            Send(new byte[] { 0x1B, 0x5B, 0x48 });
            
            SessionState = EnumSessionState.Unauthenticated;
        }

        public void Send(byte[] dataToSend)
        {
            try
            {
                using var msOutputBuffer = new MemoryStream();
                foreach (var b in dataToSend)
                {
                    //When we're in a module, since new lines are stripped from MVC's etc, and only
                    //carriage returns remain, IF we're in a module and encounter a CR, add a NL
                    if (SessionState == EnumSessionState.EnteringModule || SessionState == EnumSessionState.InModule ||
                        SessionState == EnumSessionState.LoginRoutines)
                    {
                        switch (b)
                        {
                            case 0xD:
                                msOutputBuffer.WriteByte(0xA);
                                break;
                            case 0xA:
                                msOutputBuffer.WriteByte(0xD);
                                break;
                            case 0xFF:
                                msOutputBuffer.WriteByte(0xFF);
                                break;
                            case 0x13:
                                continue;
                        }
                    }

                    msOutputBuffer.WriteByte(b);
                }

                _telnetConnection.Send(msOutputBuffer.ToArray(), SocketFlags.None, out var socketState);
                ValidateSocketState(socketState);
            }
            catch (ObjectDisposedException)
            {
                _logger.Warn($"Channel {Channel}: Attempted to write on a disposed socket");
            }
        }

        /// <summary>
        ///     Send a Byte Array to the client
        /// </summary>
        private void SendWorker()
        {
            while (SessionState != EnumSessionState.LoggedOff && _telnetConnection.IsConnected())
            {
                //If client didn't initiate IAC, initiate it from the server
                if (_iacPhase == 0 && SessionTimer.ElapsedMilliseconds > 500)
                {
                    _logger.Warn("Client hasn't negotiated IAC -- Sending Minimum");
                    _iacPhase = 1;
                    Send(new IacResponse(EnumIacVerbs.DO, EnumIacOptions.BinaryTransmission).ToArray());
                    
                }

                while (DataToClient.TryDequeue(out var dataToSend))
                {
                    Send(dataToSend);
                }

                if (SessionState == EnumSessionState.LoggingOffProcessing)
                {
                    SessionState = EnumSessionState.LoggedOff;
                    return;
                }

                Thread.Sleep(100);
            }

            //Cleanup if the connection was dropped
            SessionState = EnumSessionState.LoggedOff;
        }

        private void ReceiveWorker()
        {
            while (SessionState != EnumSessionState.LoggedOff && _telnetConnection.IsConnected())
            {
                var bytesReceived =
                    _telnetConnection.Receive(socketReceiveBuffer, SocketFlags.None, out var socketState);
                ValidateSocketState(socketState);

                if (bytesReceived == 0)
                    continue;

                //Process if it's an IAC command
                if (socketReceiveBuffer[0] == 0xFF)
                {
                    ParseIAC(socketReceiveBuffer);
                    continue;
                }

                //Enqueue the incoming bytes for processing
                for (var i = 0; i < bytesReceived; i++)
                    DataFromClient.Enqueue(socketReceiveBuffer[0]);

                Thread.Sleep(1);
            }

            //Cleanup if the connection was dropped
            SessionState = EnumSessionState.LoggedOff;

            //Dispose the socket connection
            _telnetConnection.Dispose();
        }

        /// <summary>
        ///     Parses IAC Commands Received by 
        /// </summary>
        /// <param name="iacResponse"></param>
        private void ParseIAC(ReadOnlySpan<byte> iacResponse)
        {
            var _iacResponses = new List<IacResponse>();

            for (var i = 0; i < iacResponse.Length; i += 3)
            {
                if (iacResponse[i] == 0)
                    break;

                if (iacResponse[i] != 0xFF)
                    throw new Exception("Invalid IAC?");

                var iacVerb = (EnumIacVerbs)iacResponse[i + 1];
                var iacOption = (EnumIacOptions)iacResponse[i + 2];

                _logger.Info($">> Channel {Channel}: IAC {iacVerb} {iacOption}");

                switch (iacOption)
                {
                    case EnumIacOptions.BinaryTransmission:
                        {
                            switch (iacVerb)
                            {
                                case EnumIacVerbs.WILL:
                                    _iacResponses.Add(new IacResponse(EnumIacVerbs.DO, EnumIacOptions.BinaryTransmission));
                                    break;
                                case EnumIacVerbs.DO:
                                    _iacResponses.Add(new IacResponse(EnumIacVerbs.WILL, EnumIacOptions.BinaryTransmission));
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

            if (_iacPhase == 0 && _iacResponses.All(x => x.Option != EnumIacOptions.BinaryTransmission))
            {
                _iacResponses.Add(new IacResponse(EnumIacVerbs.DO, EnumIacOptions.BinaryTransmission));
                _iacResponses.Add(new IacResponse(EnumIacVerbs.WILL, EnumIacOptions.BinaryTransmission));
            }

            _iacPhase++;

            using var msIacToSend = new MemoryStream();
            foreach (var resp in _iacResponses)
            {
                //Prevent Duplicate Responses
                if (!_iacSentResponses.Any(x => x.Verb == resp.Verb && x.Option == resp.Option))
                {
                    _iacSentResponses.Add(resp);
                    _logger.Info($"<< Channel {Channel}: IAC {resp.Verb} {resp.Option}");
                    msIacToSend.Write(resp.ToArray());
                }
                else
                {
                    _logger.Info($"<< Channel {Channel}: IAC {resp.Verb} {resp.Option} (Ignored, Duplicate)");
                }
            }

            if (msIacToSend.Length == 0)
                return;

            Send(msIacToSend.ToArray());
        }

        /// <summary>
        ///     Validates SocketError returned from an operation doesn't put the socket in an error state
        /// </summary>
        /// <param name="socketError"></param>
        private void ValidateSocketState(SocketError socketError)
        {
            switch (socketError)
            {
                case SocketError.Success:
                    return;
                case SocketError.ConnectionReset:
                case SocketError.TimedOut:
                    {
                        _logger.Warn($"Session {SessionId} (Channel: {Channel}) forcefully disconnected: {socketError}");
                        SessionState = EnumSessionState.LoggedOff;
                        _telnetConnection.Dispose();
                        return;
                    }
                default:
                    throw new Exception($"Socket Error: {Enum.GetName(typeof(SocketError), socketError)}");
            }

        }
    }
}
