using System.IO;
using System.Text;
using System;

namespace MBBSEmu.IO
{
    public class TextWriterStream : IStream
    {
        private readonly TextWriter _writer;

        public TextWriterStream(TextWriter writer)
        {
            _writer = writer;
        }

        public byte Read() => throw new NotSupportedException("Can't read from TextWriter");

        public void Write(byte c) => _writer.Write((char)c);

        public void Write(byte[] c) => _writer.Write(Encoding.ASCII.GetString(c));

        public void Flush() => _writer.Flush();

        public void Dispose() => _writer.Close();
    }
}
