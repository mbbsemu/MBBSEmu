using System;
using Newtonsoft.Json;

namespace MBBSEmu.Btrieve
{
    public class BtrieveRecord
    {
        public uint Offset { get; set; }

        public byte[] Data { get; set; }

        public BtrieveRecord(uint offset, byte[] data)
        {
            Offset = offset;
            Data = data;
        }

        public ReadOnlySpan<byte> ToSpan() => Data;
    }
}
