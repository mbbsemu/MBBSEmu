using System;

namespace MBBSEmu.HostProcess.Structs
{
    /// <summary>
    ///     Btrieve parameter block structure for use with INT 0x7B
    ///
    ///     Passed into PHAPI
    /// </summary>
    public class BtvdatStruct
    {
        public ushort databufoffset
        {
            get => BitConverter.ToUInt16(Data, 0);
            set => Array.Copy(BitConverter.GetBytes(value), 0, Data, 0, sizeof(ushort));
        }

        public ushort databufsegment
        {
            get => BitConverter.ToUInt16(Data, 2);
            set => Array.Copy(BitConverter.GetBytes(value), 0, Data, 2, sizeof(ushort));
        }

        public ushort databuflen
        {
            get => BitConverter.ToUInt16(Data, 4);
            set => Array.Copy(BitConverter.GetBytes(value), 0, Data, 4, sizeof(ushort));
        }

        public ushort posp38off
        {
            get => BitConverter.ToUInt16(Data, 6);
            set => Array.Copy(BitConverter.GetBytes(value), 0, Data, 6, sizeof(ushort));
        }

        public ushort posp38seg
        {
            get => BitConverter.ToUInt16(Data, 8);
            set => Array.Copy(BitConverter.GetBytes(value), 0, Data, 8, sizeof(ushort));
        }

        public ushort posblkoff
        {
            get => BitConverter.ToUInt16(Data, 10);
            set => Array.Copy(BitConverter.GetBytes(value), 0, Data, 10, sizeof(ushort));
        }

        public ushort posblkseg
        {
            get => BitConverter.ToUInt16(Data, 12);
            set => Array.Copy(BitConverter.GetBytes(value), 0, Data, 12, sizeof(ushort));
        }

        public ushort funcno
        {
            get => BitConverter.ToUInt16(Data, 14);
            set => Array.Copy(BitConverter.GetBytes(value), 0, Data, 14, sizeof(ushort));
        }

        public ushort keyoff
        {
            get => BitConverter.ToUInt16(Data, 16);
            set => Array.Copy(BitConverter.GetBytes(value), 0, Data, 16, sizeof(ushort));
        }

        public ushort keyseg
        {
            get => BitConverter.ToUInt16(Data, 18);
            set => Array.Copy(BitConverter.GetBytes(value), 0, Data, 18, sizeof(ushort));
        }

        public byte keylen
        {
            get => Data[20];
            set => Data[20] = value;
        }

        public byte keyno
        {
            get => Data[21];
            set => Data[21] = value;
        }

        public ushort statptoff
        {
            get => BitConverter.ToUInt16(Data, 22);
            set => Array.Copy(BitConverter.GetBytes(value), 0, Data, 22, sizeof(ushort));
        }

        public ushort statptseg
        {
            get => BitConverter.ToUInt16(Data, 24);
            set => Array.Copy(BitConverter.GetBytes(value), 0, Data, 24, sizeof(ushort));
        }

        public ushort magic
        {
            get => BitConverter.ToUInt16(Data, 26);
            set => Array.Copy(BitConverter.GetBytes(value), 0, Data, 26, sizeof(ushort));
        }

        public readonly byte[] Data = new byte[Size];

        public const ushort Size = 28;

        public BtvdatStruct() {}

        public BtvdatStruct(byte[] data)
        {
            Data = data;
        }

        public BtvdatStruct(ReadOnlySpan<byte> data)
        {
            Data = data.ToArray();
        }

    }
}
