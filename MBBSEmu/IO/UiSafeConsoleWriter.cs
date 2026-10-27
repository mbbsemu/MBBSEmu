using MBBSEmu.Logging;
using System;
using System.IO;
using System.Text;

namespace MBBSEmu.IO
{
    /// <summary>
    ///     Console writer for output that can arrive while the Admin Console owns the terminal (e.g. a module writing
    ///     to stderr through INT 21h)
    ///
    ///     Checks ConsoleOwnership on every write, because these writers are created while modules load, before the
    ///     UI starts: with the UI up, text goes to the log one line at a time, otherwise straight to the console.
    /// </summary>
    public sealed class UiSafeConsoleWriter : TextWriter
    {
        private readonly Func<TextWriter> _console;
        private readonly LogLineWriter _log;

        /// <param name="console">The console writer to use when no UI is active (e.g. () => Console.Error)</param>
        /// <param name="logLine">Receives each line written while the UI is active</param>
        public UiSafeConsoleWriter(Func<TextWriter> console, Action<string> logLine)
        {
            _console = console;
            _log = new LogLineWriter(logLine);
        }

        public override Encoding Encoding => Encoding.UTF8;

        public override void Write(char value)
        {
            if (ConsoleOwnership.IsUiActive)
                _log.Write(value);
            else
                _console().Write(value);
        }

        public override void Flush()
        {
            _log.Flush();
            _console().Flush();
        }

        /// <summary>
        ///     Flushes, but never closes the console it writes to
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _log.Flush();

            base.Dispose(disposing);
        }
    }
}
