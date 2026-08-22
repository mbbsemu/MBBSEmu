using MBBSEmu.DOS;
using MBBSEmu.HostProcess;
using MBBSEmu.Logging;
using MBBSEmu.Logging.Targets;
using MBBSEmu.Session.Enums;
using MBBSEmu.TextEncoding;
using MBBSEmu.TextVariables;
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
        private readonly IMessageLogger _logger;
        private readonly IMbbsHost _host;
        private readonly Timer _timer;
        private readonly Thread _consoleInputThread;
        private readonly Thread _consoleOutputThread;
        private bool _consoleInputThreadIsRunning;
        private readonly bool _processClientData;
        public bool StopHostOnStop { get; set; } = true;

        public LocalConsoleSession(IMessageLogger logger, string sessionId, IMbbsHost host, ITextVariableService textVariableService, bool processClientData = true, bool disableLogging = true) : base(host, sessionId, EnumSessionState.Unauthenticated, textVariableService)
        {
            _logger = logger;
            _host = host;
            _processClientData = processClientData;
            SendToClientMethod = dataToSend => UnicodeANSIOutput(dataToSend);

            //Timer to trigger btuche() if enabled
            _timer = new Timer(_ =>
            {
                if (EchoEmptyInvokeEnabled && DataToClient.Count == 0)
                    EchoEmptyInvoke = true;
            }, this, TimeSpan.FromMilliseconds(500), TimeSpan.FromMilliseconds(500));

            Console.Clear();

            Console.OutputEncoding = GetConsoleOutputEncoding(RuntimeInformation.IsOSPlatform(OSPlatform.Windows));

            //Detect if we're on Windows and enable VT100 on the current Terminal Window
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                new Win32VT100(_logger).Enable();

            if (disableLogging)
                (_logger as LoggerBase)?.RemoveTarget<ConsoleTarget>();

            _consoleInputThreadIsRunning = true;
            _consoleInputThread = new Thread(InputThread);
            _consoleInputThread.Start();

            _consoleOutputThread = new Thread(OutputThread);
            _consoleOutputThread.Start();

            _host?.AddSession(this);
        }

        /// <summary>
        ///     Returns the console output encoding appropriate for the host platform.
        ///
        ///     The Windows console renders UTF-16 correctly because .NET routes it through
        ///     WriteConsoleW, but a Unix terminal reads the raw UTF-16LE bytes as UTF-8: the
        ///     box-drawing character U+2551 emits 0x51 0x25 and prints as "Q%", which garbles
        ///     every piece of ANSI art. ASCII appears to survive only because its high byte is
        ///     NUL, which terminals discard.
        ///
        ///     The UTF-8 encoding is explicitly BOM-free. Console suppresses preambles on its
        ///     own, but returning a BOM-free encoding keeps the result correct for any writer.
        /// </summary>
        public static Encoding GetConsoleOutputEncoding(bool isWindows) =>
            isWindows ? Encoding.Unicode : new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        private void InputThread()
        {
            while (SessionState != EnumSessionState.LoggedOff && _consoleInputThreadIsRunning)
            {
                DataFromClient.TryAdd((byte)Console.ReadKey(true).KeyChar);

                if (_processClientData)
                    ProcessDataFromClient();
            }
        }

        private void OutputThread()
        {
            while (_consoleInputThreadIsRunning)
            {
                if (DataToClient.TryTake(out var dataToSend, 500))
                    UnicodeANSIOutput(dataToSend);
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
                Console.Write(Encoding.Unicode.GetString(BitConverter.GetBytes(CP437Converter.GetUnicodeCodePoint(c))));
            }
        }

        public override void Stop()
        {
            (_logger as LoggerBase)?.AddTarget(new ConsoleTarget());

            _consoleInputThreadIsRunning = false;
            _timer.Dispose();
            if (StopHostOnStop)
                _host?.Stop();

            Console.Clear();
            // the thread is stuck in ReadKey, the user needs to free that thread to end the
            // program cleanly
            Console.WriteLine("Press a key to quit");
        }
    }
}
