using System;

namespace MBBSEmu.Btrieve
{
    /// <summary>
    ///     Represents a Key Record within a Key Location Page
    ///
    ///     NOTE: This is no longer used as MBBSEmu is no longer parsing out Key Pages
    ///           It is kept here as additional information for anyone doing research on the
    ///           Btrieve file Format.
    /// </summary>
    [Obsolete("BtrieveKeyRecord is obsolete, do not use.", true)]
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
