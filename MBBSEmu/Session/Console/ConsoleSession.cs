using MBBSEmu.DependencyInjection;
using MBBSEmu.HostProcess;
using MBBSEmu.Logging;
using MBBSEmu.Session.Enums;
using NLog;
using System.Text;
using System.Threading;

namespace MBBSEmu.Session.Console
{
    /// <summary>
    ///     Console Session Handler for direct play through the command line
    /// </summary>
    public class ConsoleSession : SessionBase
    {
        private ILogger _logger;
        private readonly Thread _consoleInputThread;

        public ConsoleSession(string sessionId, IMbbsHost host) : base(sessionId)
        {
            _logger = ServiceResolver.GetService<ILogger>();
            SendToClientMethod = dataToSend => System.Console.Write(Encoding.ASCII.GetString(dataToSend));
            System.Console.Clear();
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
                DataFromClient.Add((byte)System.Console.ReadKey(true).KeyChar);
                ProcessDataFromClient();
                Thread.Sleep(1);
            }
        }

        public override void Stop()
        {
            _consoleInputThread.Abort();
        }
    }
}
