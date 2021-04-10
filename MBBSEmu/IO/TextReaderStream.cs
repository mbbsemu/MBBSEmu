using System;
using System.IO;

namespace MBBSEmu.IO
{
    public class TextReaderStream : IStream
    {
        private readonly TextReader _reader;

        public TextReaderStream(TextReader reader)
        {
            _reader = reader;
        }

        public byte Read() => (byte)_reader.Read();

        public void Write(byte c) => throw new NotSupportedException("Can't write to TextReader");

        public void Write(byte[] c) => throw new NotSupportedException("Can't write to TextReader");

        public void Flush() {}

        public void Dispose() => _reader.Close();
    }
}
