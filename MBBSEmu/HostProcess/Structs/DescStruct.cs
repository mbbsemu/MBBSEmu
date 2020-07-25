using System;

namespace MBBSEmu.HostProcess.Structs
{
    /// <summary>
    ///     Idealized Segment Descriptor
    ///
    ///     PHAPI.H
    /// </summary>
    public class DescStruct
    {
        public uint segmentBase
        {
            get => BitConverter.ToUInt32(Data, 0);
            set => Array.Copy(BitConverter.GetBytes(value), 0, Data, 0, sizeof(uint));
        }

        public uint segmentSize
        {
            get => BitConverter.ToUInt32(Data, 4);
            set => Array.Copy(BitConverter.GetBytes(value), 0, Data, 4, sizeof(uint));
        }

        public ushort attributes
        {
            get => BitConverter.ToUInt16(Data, 8);
            set => Array.Copy(BitConverter.GetBytes(value), 0, Data, 8, sizeof(ushort));
        }

        public const ushort Size = 10;

        public readonly byte[] Data = new byte[Size];
    }
}
