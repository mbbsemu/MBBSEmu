using System;

namespace MBBSEmu.IO
{
    public interface IStream : IDisposable
    {
        /// <summary>
        ///     Reads a byte from the stream.
        /// </summary>
        /// <returns>The byte read, or -1 if at EOF</returns>
        int ReadByte();
        void Write(byte c);
        void Write(byte[] c);
        void Flush();
    }
}
