using System;
using System.Collections.Generic;
using System.Text;

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
    }
}
