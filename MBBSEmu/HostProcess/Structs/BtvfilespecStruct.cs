using System;
using System.Collections.Generic;
using System.Text;

namespace MBBSEmu.HostProcess.Structs
{
    /// <summary>
    ///     Struct representing the raw return value of the Btrieve STAT command
    /// </summary>
    public class BtvfilespecStruct
    {
        public ushort reclen
        {
            get => BitConverter.ToUInt16(Data, 0);
            set => Array.Copy(BitConverter.GetBytes(value), 0, Data, 0, sizeof(ushort));
        }

        public ushort pagsiz
        {
            get => BitConverter.ToUInt16(Data, 2);
            set => Array.Copy(BitConverter.GetBytes(value), 0, Data, 2, sizeof(ushort));
        }

        public ushort numofx
        {
            get => BitConverter.ToUInt16(Data, 4);
            set => Array.Copy(BitConverter.GetBytes(value), 0, Data, 4, sizeof(ushort));
        }

        public uint numofr
        {
            get => BitConverter.ToUInt32(Data, 6);
            set => Array.Copy(BitConverter.GetBytes(value), 0, Data, 6, sizeof(ushort));
        }

        public ushort flags
        {
            get => BitConverter.ToUInt16(Data, 10);
            set => Array.Copy(BitConverter.GetBytes(value), 0, Data, 8, sizeof(ushort));
        }

        public ushort reserved
        {
            get => BitConverter.ToUInt16(Data, 12);
            set => Array.Copy(BitConverter.GetBytes(value), 0, Data, 10, sizeof(ushort));
        }

        public ushort unupag
        {
            get => BitConverter.ToUInt16(Data, 14);
            set => Array.Copy(BitConverter.GetBytes(value), 0, Data, 12, sizeof(ushort));
        }

        public readonly byte[] Data = new byte[Size];

        public const ushort Size = 16;

        public BtvfilespecStruct()
        {
            
        }

        public BtvfilespecStruct(byte[] data)
        {
            Data = data;
        }

        public BtvfilespecStruct(ReadOnlySpan<byte> data)
        {
            Data = data.ToArray();
        }

    }
}
