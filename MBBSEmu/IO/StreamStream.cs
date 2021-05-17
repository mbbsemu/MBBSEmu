using System;
using System.IO;

namespace MBBSEmu.IO
{
    public class StreamStream : IStream
    {
        private readonly Stream _stream;

        public StreamStream(Stream stream)
        {
            _stream = stream;
        }

        public int ReadByte() => _stream.ReadByte();

        public void Write(byte c) => _stream.WriteByte(c);

        public void Write(byte[] c) => _stream.Write(c);

        public void Flush() => _stream.Flush();

        public void Dispose()
        {
            _stream.Close();
            _stream.Dispose();
        }
    }
}
