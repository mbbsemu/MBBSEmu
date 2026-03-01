using MBBSEmu.DOS;
using MBBSEmu.TextEncoding;
using MBBSEmu.HostProcess;
using MBBSEmu.Logging;
using MBBSEmu.Session.Enums;
using MBBSEmu.TextVariables;
using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using MBBSEmu.Logging.Targets;

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

            Console.OutputEncoding = Encoding.Unicode;

            //Detect if we're on Windows and enable VT100 on the current Terminal Window
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                new Win32VT100(_logger).Enable();

            if(disableLogging)
                (_logger as LoggerBase)?.RemoveTarget<ConsoleTarget>();

            _consoleInputThreadIsRunning = true;
            _consoleInputThread = new Thread(InputThread);
            _consoleInputThread.Start();

            _consoleOutputThread = new Thread(OutputThread);
            _consoleOutputThread.Start();

            _host?.AddSession(this);
        }

        private void InputThread()
        {
            while (SessionState != EnumSessionState.LoggedOff && _consoleInputThreadIsRunning)
            {
                DataFromClient.TryAdd((byte)Console.ReadKey(true).KeyChar);

                if(_processClientData)
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
            _host?.Stop();

            Console.Clear();
            // the thread is stuck in ReadKey, the user needs to free that thread to end the
            // program cleanly
            Console.WriteLine("Press a key to quit");
        }
    }
}
