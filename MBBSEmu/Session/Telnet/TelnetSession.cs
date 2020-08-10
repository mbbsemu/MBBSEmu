using MBBSEmu.DependencyInjection;
using MBBSEmu.HostProcess;
using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using MBBSEmu.Extensions;

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

        private static readonly byte[] IAC_NOP = { 0xFF, 0xF1};
        private static readonly byte[] ANSI_ERASE_DISPLAY = {0x1B, 0x5B, 0x32, 0x4A};
        private static readonly byte[] ANSI_RESET_CURSOR = {0x1B, 0x5B, 0x48};

        //Tracks Responses We've already sent -- prevents looping
        private readonly List<IacResponse> _iacSentResponses = new List<IacResponse>();

        public TelnetSession(Socket telnetConnection) : base(telnetConnection.RemoteEndPoint.ToString())
        {
            SessionType = EnumSessionType.Telent;
            SendToClientMethod = Send;
            _host = ServiceResolver.GetService<IMbbsHost>();
            _logger = ServiceResolver.GetService<ILogger>();
            _telnetConnection = telnetConnection;
            _telnetConnection.ReceiveTimeout = (1000 * 60) * 5; //5 Minutes
            _telnetConnection.ReceiveBufferSize = 256;

            SessionState = EnumSessionState.Negotiating;

            //Start Listeners & Senders
            _sendThread = new Thread(SendWorker);
            _sendThread.Start();
            _receiveThread = new Thread(ReceiveWorker);
            _receiveThread.Start();

            //Add this Session to the Host
            _host.AddSession(this);

            Send(ANSI_ERASE_DISPLAY);
            Send(ANSI_RESET_CURSOR);

            SessionState = EnumSessionState.Unauthenticated;
        }

        /// <summary>
        ///     Sends a NOP IAC command to detect if the socket is still available
        /// </summary>
        /// <returns></returns>
        public bool Heartbeat()
        {
            try
            {
                //Telnet IAC NOP
                _telnetConnection.Send(IAC_NOP, SocketFlags.None, out var socketState);
                ValidateSocketState(socketState);
            }
            catch (Exception)
            {
                _logger.Warn($"Detected Channel {Channel} disconnect -- cleaning up");
                SessionState = EnumSessionState.LoggedOff;
                return false;
            }

            return true;
        }

        /// <summary>
        ///     Sends data to the connected session
        /// </summary>
        /// <param name="dataToSend"></param>
        public void Send(byte[] dataToSend)
        {
            try
            {
                using var msOutputBuffer = new MemoryStream(dataToSend.Length);
                foreach (var b in dataToSend)
                {
                    //Special Character Escapes while in a Module
                    if (SessionState == EnumSessionState.EnteringModule || SessionState == EnumSessionState.InModule ||
                        SessionState == EnumSessionState.LoginRoutines || SessionState == EnumSessionState.ExitingFullScreenDisplay)
                    {
                        switch (b)
                        {
                            case 0xFF: //Escape 0xFF with two
                                msOutputBuffer.WriteByte(0xFF);
                                break;
                            case 0x13: //ignore
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

        private static TimeSpan MINUTE_TIMESPAN = TimeSpan.FromMinutes(1);

        /// <summary>
        ///     Worker for Thread Responsible for Dequeuing data to be sent to the client
        /// </summary>
        private void SendWorker()
        {
            TimeSpan timeSpan = TimeSpan.FromSeconds(0.5);
            while (SessionState != EnumSessionState.LoggedOff && _telnetConnection.Connected)
            {
                if (!DataToClient.TryTake(out var dataToSend, timeSpan))
                {
                    if (!Heartbeat()) {
                        break;
                    }
                    continue;
                }

                if (SessionTimer.ElapsedMilliseconds >= 500) {
                    if (_iacPhase == 0) {
                        TriggerIACNegotation();
                    }
                    timeSpan = MINUTE_TIMESPAN;
                }

                Send(dataToSend);

                if (SessionState == EnumSessionState.LoggingOffProcessing)
                {
                    SessionState = EnumSessionState.LoggedOff;
                    return;
                }

                if (EchoEmptyInvokeEnabled && DataToClient.Count == 0)
                    EchoEmptyInvoke = true;
            }

            //Cleanup if the connection was dropped
            SessionState = EnumSessionState.LoggedOff;
        }

        /// <summary>
        ///     Worker for Thread Responsible for Receiving Data from Client
        /// </summary>
        private void ReceiveWorker()
        {
            while (SessionState != EnumSessionState.LoggedOff && _telnetConnection.Connected)
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
                    DataFromClient.Add(socketReceiveBuffer[i]);
            }

            //Cleanup if the connection was dropped
            SessionState = EnumSessionState.LoggedOff;

            //Dispose the socket connection
            _telnetConnection.Dispose();
        }

        /// <summary>
        ///     Initiates server side IAC negotation
        /// </summary>
        private void TriggerIACNegotation() {
            _iacPhase = 1;
            Send(new IacResponse(EnumIacVerbs.DO, EnumIacOptions.BinaryTransmission).ToArray());
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

            //In the 1st phase of IAC negotiation, ensure the required items are being negotiated
            if (_iacPhase == 0)
            {
                if (_iacResponses.All(x => x.Option != EnumIacOptions.BinaryTransmission))
                {
                    _iacResponses.Add(new IacResponse(EnumIacVerbs.DO, EnumIacOptions.BinaryTransmission));
                    _iacResponses.Add(new IacResponse(EnumIacVerbs.WILL, EnumIacOptions.BinaryTransmission));
                }

                if (_iacResponses.All(x => x.Option != EnumIacOptions.Echo))
                {
                    _iacResponses.Add(new IacResponse(EnumIacVerbs.DONT, EnumIacOptions.Echo));
                    _iacResponses.Add(new IacResponse(EnumIacVerbs.WILL, EnumIacOptions.Echo));
                }

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
                case SocketError.ConnectionAborted:
                case SocketError.TimedOut:
                    {
                        _logger.Warn($"Session {SessionId} (Channel: {Channel}) forcefully disconnected: {socketError}");
                        SessionState = EnumSessionState.LoggedOff;
                        return;
                    }
                default:
                    throw new Exception($"Socket Error: {Enum.GetName(typeof(SocketError), socketError)}");
            }

        }
    }
}
