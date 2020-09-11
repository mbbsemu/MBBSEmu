using System;

namespace MBBSEmu.HostProcess.Structs
{
    /// <summary>
    ///     Btrieve STAT command Key Specs
    /// </summary>
    public class BtvkeyspecStruct
    {
        public ushort keypos
        {
            get => BitConverter.ToUInt16(Data, 0);
            set => Array.Copy(BitConverter.GetBytes(value), 0, Data, 0, sizeof(ushort));
        }

        public ushort keylen
        {
            get => BitConverter.ToUInt16(Data, 2);
            set => Array.Copy(BitConverter.GetBytes(value), 0, Data, 2, sizeof(ushort));
        }

        public ushort flags
        {
            get => BitConverter.ToUInt16(Data, 4);
            set => Array.Copy(BitConverter.GetBytes(value), 0, Data, 4, sizeof(ushort));
        }

        public uint numofk
        {
            get => BitConverter.ToUInt32(Data, 6);
            set => Array.Copy(BitConverter.GetBytes(value), 0, Data, 6, sizeof(ushort));
        }

        public ushort dontcare
        {
            get => BitConverter.ToUInt16(Data, 10);
            set => Array.Copy(BitConverter.GetBytes(value), 0, Data, 8, sizeof(ushort));
        }

        public uint reserved
        {
            get => BitConverter.ToUInt32(Data, 12);
            set => Array.Copy(BitConverter.GetBytes(value), 0, Data, 10, sizeof(ushort));
        }

        public readonly byte[] Data = new byte[Size];

        public const ushort Size = 16;

        public BtvkeyspecStruct()
        {
            
        }

        public BtvkeyspecStruct(byte[] data)
        {
            Data = data;
        }

        public BtvkeyspecStruct(ReadOnlySpan<byte> data)
        {
            Data = data.ToArray();
        }
    }
}
