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
using MBBSEmu.Session.Enums;

namespace MBBSEmu.Session.Telnet
{
    /// <summary>
    ///     Class for handling inbound Telnet Connections
    /// </summary>
    public class TelnetSession : SocketSession
    {
        private int _iacPhase;

        private static readonly byte[] IAC_NOP = { 0xFF, 0xF1};
        private static readonly byte[] ANSI_ERASE_DISPLAY = {0x1B, 0x5B, 0x32, 0x4A};
        private static readonly byte[] ANSI_RESET_CURSOR = {0x1B, 0x5B, 0x48};

        //Tracks Responses We've already sent -- prevents looping
        private readonly List<IacResponse> _iacSentResponses = new List<IacResponse>();

        public TelnetSession(Socket telnetConnection) : base(telnetConnection)
        {
            SessionType = EnumSessionType.Telnet;
            SessionState = EnumSessionState.Unauthenticated;
        }

        public override void Start()
        {
            base.Start();

            base.Send(ANSI_ERASE_DISPLAY);
            base.Send(ANSI_RESET_CURSOR);
        }

        /// <summary>
        ///     Sends a NOP IAC command to detect if the socket is still available
        /// </summary>
        /// <returns></returns>
        protected override bool Heartbeat()
        {
            try
            {
                base.Send(IAC_NOP);
            }
            catch (Exception ex)
            {
                CloseSocket($"failure sending heartbeat {ex.Message}");
                return false;
            }

            return true;
        }

        /// <summary>
        ///     Sends data to the connected session
        /// </summary>
        /// <param name="dataToSend"></param>
        public override void Send(byte[] dataToSend)
        {
            // we have to escape 0xFF , so see if we have any, and if not, just send the current
            // buffer as is
            if (!Array.Exists(dataToSend, b => b == 0xFF)) {
                base.Send(dataToSend);
                return;
            }

            using var msOutputBuffer = new MemoryStream(dataToSend.Length);
            foreach (var b in dataToSend)
            {
                switch (b)
                {
                    case 0xFF: //Escape 0xFF with two
                        msOutputBuffer.WriteByte(0xFF);
                        break;
                }

                msOutputBuffer.WriteByte(b);
            }

            base.Send(msOutputBuffer.ToArray());
        }

        protected override void PreSend()
        {
            if (SessionTimer.ElapsedMilliseconds >= 500 && _iacPhase == 0)
            {
                TriggerIACNegotiation();
            }
        }

        protected override (byte[], int) ProcessIncomingClientData(byte[] clientData, int bytesReceived)
        {
            //Process if it's an IAC command
            if (clientData[0] == 0xFF)
            {
                ParseIAC(_socketReceiveBuffer);
                return (null, 0);
            }

            return (clientData, bytesReceived);
        }

        /// <summary>
        ///     Initiates server side IAC negotation
        /// </summary>
        private void TriggerIACNegotiation() {
            _iacPhase = 1;
            base.Send(new IacResponse(EnumIacVerbs.DO, EnumIacOptions.BinaryTransmission).ToArray());
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

            base.Send(msIacToSend.ToArray());
        }
    }
}
