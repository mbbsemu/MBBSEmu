using System;
using System.Collections.Generic;
using System.Text;

namespace MBBSEmu.Btrieve
{
    public class BtrieveKey
    {
        public byte[] Data { get; set; }
        public ushort Number { get; set; }

        public ushort Offset => BitConverter.ToUInt16(Data, Data.Length - 3);

        public byte[] Key
        {
            get
            {
                ReadOnlySpan<byte> dataSpan = Data;
                return dataSpan.Slice(4, dataSpan.Length - 4 - 2).ToArray();
            }
        }

        public BtrieveKey(byte[] data, ushort number)
        {
            Data = data;
            Number = number;
        }

    }
}
