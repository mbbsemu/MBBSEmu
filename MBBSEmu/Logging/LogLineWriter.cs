using System;
using System.IO;
using System.Text;

namespace MBBSEmu.Logging
{
    /// <summary>
    ///     TextWriter that turns written text into whole lines for a logger
    ///
    ///     Used to route a DOS program's console output into the log while the Admin Console owns the terminal
    /// </summary>
    public sealed class LogLineWriter : TextWriter
    {
        private readonly Action<string> _writeLine;
        private readonly StringBuilder _buffer = new();

        /// <param name="writeLine">Receives each line, without its line ending</param>
        public LogLineWriter(Action<string> writeLine)
        {
            _writeLine = writeLine;
        }

        public override Encoding Encoding => Encoding.UTF8;

        public override void Write(char value)
        {
            switch (value)
            {
                case '\n':
                    EmitBufferedLine();
                    break;
                case '\r':
                    break;
                default:
                    _buffer.Append(value);
                    break;
            }
        }

        /// <summary>
        ///     Emits any partial line still buffered
        /// </summary>
        public override void Flush()
        {
            if (_buffer.Length > 0)
                EmitBufferedLine();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                Flush();

            base.Dispose(disposing);
        }

        private void EmitBufferedLine()
        {
            _writeLine(_buffer.ToString());
            _buffer.Clear();
        }
    }
}
