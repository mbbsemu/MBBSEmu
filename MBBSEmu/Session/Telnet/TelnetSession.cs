using MBBSEmu.Session.Enums;
using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;

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
        private readonly HashSet<IacResponse> _iacSentResponses = new HashSet<IacResponse>();

        private class TelnetOptionsValue {
            public bool Local { get; set; }
            public bool Remote { get; set; }

            public EnumIacVerbs GetLocalStatusVerb()
            {
                return Local ? EnumIacVerbs.WILL : EnumIacVerbs.WONT;
            }

            public EnumIacVerbs GetRemoteCommandVerb()
            {
                return Remote ? EnumIacVerbs.DO : EnumIacVerbs.DONT;
            }
        }

        private readonly Dictionary<EnumIacOptions, TelnetOptionsValue> _localOptions =
            new Dictionary<EnumIacOptions, TelnetOptionsValue>()
            {
                {EnumIacOptions.BinaryTransmission, new TelnetOptionsValue() { Local = true, Remote = true}},
                {EnumIacOptions.Echo, new TelnetOptionsValue() { Local = true, Remote = false}},
                {EnumIacOptions.SuppressGoAhead, new TelnetOptionsValue() { Local = true, Remote = true}},
            };

        private readonly IacFilter _iacFilter;

        public TelnetSession(ILogger logger, Socket telnetConnection) : base(logger, telnetConnection)
        {
            SessionType = EnumSessionType.Telnet;
            SessionState = EnumSessionState.Unauthenticated;

            _iacFilter = new IacFilter(logger);
            _iacFilter.IacVerbReceived += OnIacVerbReceived;
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
            return _iacFilter.ProcessIncomingClientData(clientData, bytesReceived);
        }

        /// <summary>
        ///     Initiates server side IAC negotation
        /// </summary>
        private void TriggerIACNegotiation() {
            _iacPhase = 1;
            base.Send(new IacResponse(EnumIacVerbs.DO, EnumIacOptions.BinaryTransmission).ToArray());
        }

        private void AddInitialNegotations(HashSet<IacResponse> responses)
        {
            foreach(KeyValuePair<EnumIacOptions, TelnetOptionsValue> entry in _localOptions)
            {
                responses.Add(new IacResponse(entry.Value.GetLocalStatusVerb(), entry.Key));
                responses.Add(new IacResponse(entry.Value.GetRemoteCommandVerb(), entry.Key));
            }
        }

        private void OnIacVerbReceived(object sender, IacFilter.IacVerbReceivedEventArgs args)
        {
            var iacResponses = new HashSet<IacResponse>();

            _logger.Debug($">> Channel {Channel}: IAC {args.Verb} {args.Option}");

            if (_localOptions.TryGetValue(args.Option, out var localOptionsValue))
            {
                // we support this, and we're hard coded, so tell client to back off and listen to
                // us
                iacResponses.Add(new IacResponse(localOptionsValue.GetLocalStatusVerb(), args.Option));
                if (args.Verb == EnumIacVerbs.WILL || args.Verb == EnumIacVerbs.WONT)
                {
                    iacResponses.Add(new IacResponse(localOptionsValue.GetRemoteCommandVerb(), args.Option));
                }
            }
            else
            {
                // not supported, just return that we WONT do this
                iacResponses.Add(new IacResponse(EnumIacVerbs.WONT, args.Option));
            }

            //In the 1st phase of IAC negotiation, ensure the required items are being negotiated
            if (_iacPhase == 0)
            {
                AddInitialNegotations(iacResponses);
            }

            _iacPhase++;

            if (iacResponses.Count == 0)
            {
                return;
            }

            using var msIacToSend = new MemoryStream();
            foreach (var resp in iacResponses)
            {
                if (_iacSentResponses.Add(resp))
                {
                    _logger.Debug($"<< Channel {Channel}: IAC {resp.Verb} {resp.Option}");
                    msIacToSend.Write(resp.ToArray());
                }
            }

            if (msIacToSend.Length > 0)
            {
                base.Send(msIacToSend.ToArray());
            }
        }
    }
}
