using MBBSEmu.DependencyInjection;
using MBBSEmu.HostProcess;
using MBBSEmu.Logging;
using MBBSEmu.Session.Enums;
using NLog;
using System;
using System.Runtime.InteropServices;
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
        private IMbbsHost _host;
        private Timer _timer;
        private readonly Thread _consoleInputThread;
        private bool _consoleInputThreadIsRunning;

        /// <summary>
        ///     This array allows for easy conversion between Extended ASCII codes used by MajorBBS/WG modules for ANSI graphics and their Unicode
        ///     counterparts.
        /// </summary>
        private readonly ushort[] _extendedASCIItoUnicode = {
                //Standard ASCII
                //  00      01      02      03      04      05      06      07      08      09      0A      0B      0C      0D      0E      0F
                0x0000, 0x0001, 0x0002, 0x0003, 0x0004, 0x0005, 0x0006, 0x0007, 0x0008, 0x0009, 0x000A, 0x000B, 0x000C, 0x000D, 0x000E, 0x000F, //00
                0x0010, 0x0011, 0x0012, 0x0013, 0x0014, 0x0015, 0x0016, 0x0017, 0x0018, 0x0019, 0x001A, 0x001B, 0x001C, 0x001D, 0x001E, 0x001F, //10
                0x0020, 0x0021, 0x0022, 0x0023, 0x0024, 0x0025, 0x0026, 0x0027, 0x0028, 0x0029, 0x002A, 0x002B, 0x002C, 0x002D, 0x002E, 0x002F, //20
                0x0030, 0x0031, 0x0032, 0x0033, 0x0034, 0x0035, 0x0036, 0x0037, 0x0038, 0x0039, 0x003A, 0x003B, 0x003C, 0x003D, 0x003E, 0x003F, //30
                0x0040, 0x0041, 0x0042, 0x0043, 0x0044, 0x0045, 0x0046, 0x0047, 0x0048, 0x0049, 0x004A, 0x004B, 0x004C, 0x004D, 0x004E, 0x004F, //40
                0x0050, 0x0051, 0x0052, 0x0053, 0x0054, 0x0055, 0x0056, 0x0057, 0x0058, 0x0059, 0x005A, 0x005B, 0x005C, 0x005D, 0x005E, 0x005F, //50
                0x0060, 0x0061, 0x0062, 0x0063, 0x0064, 0x0065, 0x0066, 0x0067, 0x0068, 0x0069, 0x006A, 0x006B, 0x006C, 0x006D, 0x006E, 0x006F, //60
                0x0070, 0x0071, 0x0072, 0x0073, 0x0074, 0x0075, 0x0076, 0x0077, 0x0078, 0x0079, 0x007A, 0x007B, 0x007C, 0x007D, 0x007E, 0x007F, //70

                //Extended ASCII
                0x0080, 0x0081, 0x0082, 0x0083, 0x0084, 0x0085, 0x0086, 0x0087, 0x0088, 0x0089, 0x008A, 0x008B, 0x008C, 0x008D, 0x008E, 0x008F, //80
                0x0090, 0x0091, 0x0092, 0x0093, 0x0094, 0x0095, 0x0096, 0x0097, 0x0098, 0x0099, 0x009A, 0x009B, 0x009C, 0x009D, 0x009E, 0x009F, //90
                0x00A0, 0x00A1, 0x00A2, 0x00A3, 0x00A4, 0x00A5, 0x00A6, 0x00A7, 0x00A8, 0x00A9, 0x00AA, 0x00AB, 0x00AC, 0x00AD, 0x00AE, 0x00AF, //A0
                0x2591, 0x2592, 0x2593, 0x2502, 0x2524, 0x2561, 0x2562, 0x2556, 0x2555, 0x2563, 0x2551, 0x2557, 0x255D, 0x255C, 0x255B, 0x2510, //B0
                0x2514, 0x2534, 0x252C, 0x251C, 0x2500, 0x253C, 0x255E, 0x255F, 0x255A, 0x2554, 0x2569, 0x2566, 0x2560, 0x2550, 0x256C, 0x6725, //C0
                0x2568, 0x2564, 0x2565, 0x2559, 0x2558, 0x2552, 0x2553, 0x256B, 0x256A, 0x2518, 0x250C, 0x2588, 0x2584, 0x258C, 0x2590, 0x2580, //D0
                0x03B1, 0x04F1, 0x03C0, 0x06A3, 0x00E4, 0x00E5, 0x00E6, 0x00E7, 0x00E8, 0x00E9, 0x00EA, 0x00EB, 0x00EC, 0x00ED, 0x00EE, 0x00EF, //E0
                0x00F0, 0x00F1, 0x00F2, 0x00F3, 0x00F4, 0x00F5, 0x00F6, 0x00F7, 0x00F8, 0x00F9, 0x00FA, 0x00FB, 0x00FC, 0x00FD, 0x00FE, 0x00FF  //F0
        };

        public LocalConsoleSession(ILogger logger, string sessionId, IMbbsHost host) : base(host, sessionId, EnumSessionState.Unauthenticated)
        {
            _logger = logger;
            _host = host;
            SendToClientMethod = dataToSend => UnicodeANSIOutput(dataToSend);

            //Timer to trigger btuche() if enabled
            _timer = new Timer(_ =>
            {
                if (EchoEmptyInvokeEnabled && DataToClient.Count == 0)
                    EchoEmptyInvoke = true;
            }, this, TimeSpan.FromMilliseconds(500), TimeSpan.FromMilliseconds(500));

            Console.Clear();

            Console.OutputEncoding = Encoding.Unicode;

            //Detect if we're on Windows and enable VT100 on the current Terminal Window
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                new Win32VT100().Enable();

            (_logger as CustomLogger)?.DisableConsoleLogging();

            _consoleInputThreadIsRunning = true;
            _consoleInputThread = new Thread(InputThread);
            _consoleInputThread.Start();
            _host.AddSession(this);
        }

        private void InputThread()
        {
            while (SessionState != EnumSessionState.LoggedOff && _consoleInputThreadIsRunning)
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
                Console.Write(Encoding.Unicode.GetString(BitConverter.GetBytes(_extendedASCIItoUnicode[c])));
            }
        }

        public override void Stop()
        {
            (_logger as CustomLogger)?.EnableConsoleLogging();

            _consoleInputThreadIsRunning = false;
            _timer.Dispose();
            _host.Stop();

            Console.Clear();
            // the thread is stuck in ReadKey, the user needs to free that thread to end the
            // program cleanly
            Console.WriteLine("Press a key to quit");
        }
    }
}
