using MBBSEmu.DependencyInjection;
using MBBSEmu.HostProcess;
using MBBSEmu.Logging;
using MBBSEmu.Session.Enums;
using NLog;
using System;
using System.Text;
using System.Threading;

namespace MBBSEmu.Session.LocalConsole
{
    /// <summary>
    ///     Console Session Handler for direct play through the command line
    /// </summary>
    public class LocalConsoleSession : SessionBase
    {
        private ILogger _logger;
        private readonly Thread _consoleInputThread;

        public LocalConsoleSession(string sessionId, IMbbsHost host) : base(sessionId)
        {
            _logger = ServiceResolver.GetService<ILogger>();
            SendToClientMethod = dataToSend => UnicodeANSIOutput(dataToSend);
            Console.Clear();
            Console.OutputEncoding = Encoding.Unicode;
            SessionState = EnumSessionState.Unauthenticated;

            (_logger as CustomLogger)?.DisableConsoleLogging();

            _consoleInputThread = new Thread(InputThread);
            _consoleInputThread.Start();
            host.AddSession(this);
        }

        private void InputThread()
        {
            while (SessionState != EnumSessionState.LoggedOff)
            {
                DataFromClient.Add((byte)Console.ReadKey(true).KeyChar);
                ProcessDataFromClient();
            }
        }

        /// <summary>
        ///     Takes an input Extended ASCII string and converts it to Unicode Output
        /// </summary>
        /// <param name="inputString"></param>
        /// <returns></returns>
        private void UnicodeANSIOutput(ReadOnlySpan<byte> inputString)
        {
            foreach (var c in inputString)
            {
                
                //Standard ASCII Characters are written as-is
                if (c <= 127)
                {
                    Console.Write((char)c);
                    continue;
                }

                //Extended ASCII Characters are converted to their Unicode Counterparts
                var unicodeOutput = Array.Empty<byte>();
                switch (c)
                {
                    case 176:
                        unicodeOutput = new byte[] { 0x91, 0x25 };
                        break;
                    case 177:
                        unicodeOutput = new byte[] { 0x92, 0x25 };
                        break;
                    case 178:
                        unicodeOutput = new byte[] { 0x93, 0x25 };
                        break;
                    case 179:
                        unicodeOutput = new byte[] { 0x03, 0x25 };
                        break;
                    case 180:
                        unicodeOutput = new byte[] { 0x1C, 0x25 };
                        break;
                    case 181:
                        unicodeOutput = new byte[] { 0x61, 0x25 };
                        break;
                    case 182:
                        unicodeOutput = new byte[] { 0x62, 0x25 };
                        break;
                    case 183:
                        unicodeOutput = new byte[] { 0x56, 0x25 };
                        break;
                    case 184:
                        unicodeOutput = new byte[] { 0x55, 0x25 };
                        break;
                    case 185:
                        unicodeOutput = new byte[] { 0x63, 0x25 };
                        break;
                    case 186:
                        unicodeOutput = new byte[] { 0x51, 0x25};
                        break;
                    case 187:
                        unicodeOutput = new byte[] { 0x57, 0x25 };
                        break;
                    case 188:
                        unicodeOutput = new byte[] { 0x5D, 0x25 };
                        break;
                    case 189:
                        unicodeOutput = new byte[] { 0x5C, 0x25 };
                        break;
                    case 190:
                        unicodeOutput = new byte[] { 0x5B, 0x25 };
                        break;
                    case 191:
                        unicodeOutput = new byte[] { 0x10, 0x25 };
                        break;
                    case 192:
                        unicodeOutput = new byte[] { 0x14, 0x25 };
                        break;
                    case 193:
                        unicodeOutput = new byte[] { 0x34, 0x25 };
                        break;
                    case 194:
                        unicodeOutput = new byte[] { 0x2C, 0x25 };
                        break;
                    case 195:
                        unicodeOutput = new byte[] { 0x1C, 0x25 };
                        break;
                    case 196:
                        unicodeOutput = new byte[] { 0x00, 0x25 };
                        break;
                    case 197:
                        unicodeOutput = new byte[] { 0x3C, 0x25 };
                        break;
                    case 198:
                        unicodeOutput = new byte[] { 0x5E, 0x25 };
                        break;
                    case 199:
                        unicodeOutput = new byte[] { 0x5F, 0x25 };
                        break;
                    case 200:
                        unicodeOutput = new byte[] { 0x5A, 0x25 };
                        break;
                    case 201:
                        unicodeOutput = new byte[] { 0x54, 0x25 };
                        break;
                    case 202:
                        unicodeOutput = new byte[] { 0x69, 0x25 };
                        break;
                    case 203:
                        unicodeOutput = new byte[] { 0x66, 0x25 };
                        break;
                    case 204:
                        unicodeOutput = new byte[] { 0x60, 0x25 };
                        break;
                    case 205:
                        unicodeOutput = new byte[] { 0x50, 0x25 };
                        break;
                    case 206:
                        unicodeOutput = new byte[] { 0x6C, 0x25 };
                        break;
                    case 207:
                        unicodeOutput = new byte[] { 0x67, 0x25 };
                        break;
                    case 208:
                        unicodeOutput = new byte[] { 0x68, 0x25 };
                        break;
                    case 209:
                        unicodeOutput = new byte[] { 0x64, 0x25 };
                        break;
                    case 210:
                        unicodeOutput = new byte[] { 0x65, 0x25 };
                        break;
                    case 211:
                        unicodeOutput = new byte[] { 0x59, 0x25 };
                        break;
                    case 212:
                        unicodeOutput = new byte[] { 0x58, 0x25 };
                        break;
                    case 213:
                        unicodeOutput = new byte[] { 0x52, 0x25 };
                        break;
                    case 214:
                        unicodeOutput = new byte[] { 0x53, 0x25 };
                        break;
                    case 215:
                        unicodeOutput = new byte[] { 0x6B, 0x25 };
                        break;
                    case 216:
                        unicodeOutput = new byte[] { 0x6A, 0x25 };
                        break;
                    case 217:
                        unicodeOutput = new byte[] { 0x18, 0x25 };
                        break;
                    case 218:
                        unicodeOutput = new byte[] { 0x0C, 0x25 };
                        break;
                    case 219:
                        unicodeOutput = new byte[] { 0x88, 0x25 };
                        break;
                    case 220:
                        unicodeOutput = new byte[] { 0x84, 0x25 };
                        break;
                    case 221:
                        unicodeOutput = new byte[] { 0x8C, 0x25 };
                        break;
                    case 222:
                        unicodeOutput = new byte[] { 0x90, 0x25 };
                        break;
                    case 223:
                        unicodeOutput = new byte[] { 0x80, 0x25 };
                        break;
                    case 224:
                        unicodeOutput = new byte[] { 0xB1, 0x03 };
                        break;
                    case 225:
                        unicodeOutput = new byte[] { 0xB2, 0x03 };
                        break;
                    case 226:
                        unicodeOutput = new byte[] { 0xF6, 0x04 };
                        break;
                    case 227:
                        unicodeOutput = new byte[] { 0xC0, 0x03 };
                        break;
                    case 228:
                        unicodeOutput = new byte[] { 0xA3, 0x06 };
                        break;
                    default:
                        break;
                }
                Console.Write(Encoding.Unicode.GetString(unicodeOutput));
            }


        }

        public override void Stop()
        {
            _consoleInputThread.Abort();
        }
    }
}
