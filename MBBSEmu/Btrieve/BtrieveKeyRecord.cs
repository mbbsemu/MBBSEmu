using System;

namespace MBBSEmu.Btrieve
{
    public class BtrieveKeyRecord
    {
        public byte[] Data { get; set; }
        public ushort Number { get; set; }
        public ushort Length { get; set; }

        public uint Offset
        {
            get
            {
                var offsetValue = BitConverter.ToUInt32(Data, Data.Length - 4);
                //Swap Endian
                return ((offsetValue >> 16) & 0xFFFF) | ((offsetValue << 16) & 0xFFFF0000);
            }
        } 

        public byte[] Key
        {
            get
            {
                ReadOnlySpan<byte> dataSpan = Data;
                return dataSpan.Slice(4, Length).ToArray();
            }
        }

        public BtrieveKeyRecord(byte[] data, ushort number, ushort length)
        {
            Data = data;
            Number = number;
            Length = length;
        }

    }
}
