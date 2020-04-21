using System;
using Newtonsoft.Json;

namespace MBBSEmu.Btrieve
{
    public class BtrieveRecord
    {
        public int Offset { get; set; }

        public byte[] Data { get; set; }

        public BtrieveRecord(int offset, byte[] data)
        {
            Offset = offset;
            Data = data;
        }

        public ReadOnlySpan<byte> ToSpan() => Data;
    }
}
