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

        private const byte IAC = (byte) 0xFF;

        private const byte SB = (byte) 0xFA;
        private const byte SE = (byte) 0xF0;

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

        public class IacVerbReceivedEventArgs : EventArgs
        {
            public EnumIacVerbs Verb { get; set; }
            public EnumIacOptions Option { get; set; }
        }

        public event EventHandler<IacVerbReceivedEventArgs> IacVerbReceived;

        public IacFilter(ILogger logger)
        {
            _logger = logger;
        }

        public (byte[], int) ProcessIncomingClientData(byte[] clientData, int bytesReceived)
        {
            _memoryStream.SetLength(0);

            for (int i = 0; i < bytesReceived; ++i)
            {
                process(clientData[i]);
            }

            return (_memoryStream.GetBuffer(), (int)_memoryStream.Length);
        }

        private void process(byte b)
        {
        switch (_parseState) {
            case ParseState.Normal:
                if (b == IAC)
                {
                    _parseState = ParseState.FoundIAC;
                }
                else
                {
                    _memoryStream.WriteByte(b);
                }
                break;
            case ParseState.FoundIAC:
                if (b == SB)
                {
                    _parseState = ParseState.SBStart;
                }
                else if (b == (byte)EnumIacVerbs.WILL || b == (byte)EnumIacVerbs.WONT || b == (byte)EnumIacVerbs.DO || b == (byte)EnumIacVerbs.DONT)
                {
                    _currentVerb = (EnumIacVerbs) b;
                    _parseState = ParseState.IACCommand;
                }
                else if (b == IAC)
                {
                     // special escape sequence
                    _memoryStream.WriteByte(b);
                }
                else
                {
                    _parseState = ParseState.Normal;
                }
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
                _parseState = ParseState.SBValue;
                break;
            case ParseState.SBValue:
                if (b == IAC)
                {
                    _parseState = ParseState.SBIAC;
                }
                break;
            case ParseState.SBIAC:
                if (b == SE)
                {
                    _parseState = ParseState.Normal;
                }
                else
                {
                    _parseState = ParseState.SBValue;
                }
                break;
            }
        }
    }
}
