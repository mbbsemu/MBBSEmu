using System;

namespace MBBSEmu.IO
{
    public interface IStream : IDisposable
    {
        byte Read();
        void Write(byte c);
        void Write(byte[] c);
        void Flush();
    }
}
