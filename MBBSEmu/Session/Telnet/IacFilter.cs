using NLog;
using System.IO;
using System;

namespace MBBSEmu.Session.Telnet
{
    /// <summary>
    ///     Parses incoming client Telnet data and invokes the IacVerbReceived
    ///     event when a verb is received.
    /// </summary>
    public class IacFilter
    {
        private readonly ILogger _logger;

        private const byte IAC = 0xFF;

        private const byte SB = 0xFA;
        private const byte SE = 0xF0;

        private enum ParseState {
            Normal,
            FoundIAC,
            IACCommand,
            SBStart,
            SBValue,
            SBIAC
        }

        // for jumbo frame size
        private readonly MemoryStream _memoryStream = new MemoryStream(10000);
        private ParseState _parseState = ParseState.Normal;
        private EnumIacVerbs _currentVerb;
        private EnumIacOptions _currentSubnegotiationOption;

        public class IacVerbReceivedEventArgs : EventArgs
        {
            public EnumIacVerbs Verb { get; set; }
            public EnumIacOptions Option { get; set; }
        }

        public class IacSubnegotiationEventArgs : EventArgs 
        {
            public EnumIacOptions Option { get; set; }
            public byte[] Data { get; set; }
        }

        public event EventHandler<IacVerbReceivedEventArgs> IacVerbReceived;
        public event EventHandler<IacSubnegotiationEventArgs> IacSubnegotiationReceived;

        public IacFilter(ILogger logger)
        {
            _logger = logger;
        }

        public (byte[], int) ProcessIncomingClientData(byte[] clientData, int bytesReceived)
        {
            _memoryStream.SetLength(0);

            for (var i = 0; i < bytesReceived; ++i)
            {
                Process(clientData[i]);
            }

            return (_memoryStream.GetBuffer(), (int)_memoryStream.Length);
        }

        private readonly MemoryStream _sbValue = new MemoryStream();

        private void Process(byte b)
        {
            switch (_parseState)
            {
                case ParseState.Normal when b == IAC:
                    _parseState = ParseState.FoundIAC;
                    break;
                case ParseState.Normal:
                    _memoryStream.WriteByte(b);
                    break;
                case ParseState.FoundIAC when b == SB:
                    _parseState = ParseState.SBStart;
                    break;
                case ParseState.FoundIAC when b == (byte)EnumIacVerbs.WILL:
                case ParseState.FoundIAC when b == (byte)EnumIacVerbs.WONT:
                case ParseState.FoundIAC when b == (byte)EnumIacVerbs.DO:
                case ParseState.FoundIAC when b == (byte)EnumIacVerbs.DONT:
                    _currentVerb = (EnumIacVerbs) b;
                    _parseState = ParseState.IACCommand;
                    break;
                case ParseState.FoundIAC when b == IAC:
                    // special escape sequence
                    _memoryStream.WriteByte(b);
                    break;
                case ParseState.FoundIAC:
                    _parseState = ParseState.Normal;
                    break;
                case ParseState.IACCommand:
                    IacVerbReceived?.Invoke(this, new IacVerbReceivedEventArgs()
                    {
                        Verb = _currentVerb,
                        Option = (EnumIacOptions)b
                    });
                    _parseState = ParseState.Normal;
                    break;
                case ParseState.SBStart:
                    _sbValue.SetLength(0);
                    _currentSubnegotiationOption = (EnumIacOptions) b;
                    _parseState = ParseState.SBValue;
                    break;
                case ParseState.SBValue when b == IAC:
                    _parseState = ParseState.SBIAC;
                    break;
                case ParseState.SBValue:
                    _sbValue.WriteByte(b);
                    break;
                case ParseState.SBIAC when b == SE:
                    IacSubnegotiationReceived?.Invoke(this, new IacSubnegotiationEventArgs() 
                    {
                        Option = _currentSubnegotiationOption,
                        Data = _sbValue.ToArray()
                    });
                    _parseState = ParseState.Normal;
                    break;
                case ParseState.SBIAC:
                    _sbValue.WriteByte(IAC);
                    _sbValue.WriteByte(b);
                    _parseState = ParseState.SBValue;
                    break;
            }
        }
    }
}
