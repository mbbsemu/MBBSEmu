using System;
using MBBSEmu.Memory;

namespace MBBSEmu.HostProcess.Structs
{
    /// <summary>
    ///     USER Struct as defined in MAJORBBS.H
    ///
    ///     41 Bytes in Total Length
    /// </summary>
    public class User : MemoryResidentStructBase
    {
        public short UserClass
        {
            get => BitConverter.ToInt16(Data);
            set => BitConverter.GetBytes(value).CopyTo(Data[..sizeof(short)]);
        }

        public FarPtr Keys
        {
            get => new FarPtr(Data.Slice(2, 4));
            set => value.Data.CopyTo(Data.Slice(2, FarPtr.Size));
        }

        public short State
        {
            get => BitConverter.ToInt16(Data[6..]);
            set => BitConverter.GetBytes(value).CopyTo(Data.Slice(6, sizeof(short)));
        }

        public short Substt
        {
            get => BitConverter.ToInt16(Data[8..]);
            set => BitConverter.GetBytes(value).CopyTo(Data.Slice(8, sizeof(short)));
        }

        public short Lofstt
        {
            get => BitConverter.ToInt16(Data[10..]);
            set => BitConverter.GetBytes(value).CopyTo(Data.Slice(10, 2));
        }

        public short Usetmr
        {
            get => BitConverter.ToInt16(Data[12..]);
            set => BitConverter.GetBytes(value).CopyTo(Data.Slice(12, sizeof(short)));
        }

        public short Minut4
        {
            get => BitConverter.ToInt16(Data[14..]);
            set => BitConverter.GetBytes(value).CopyTo(Data.Slice(14, sizeof(short)));
        }

        public short Countr
        {
            get => BitConverter.ToInt16(Data[16..]);
            set => BitConverter.GetBytes(value).CopyTo(Data.Slice(16, sizeof(short)));
        }

        public short Pfnacc
        {
            get => BitConverter.ToInt16(Data[18..]);
            set => BitConverter.GetBytes(value).CopyTo(Data.Slice(18, sizeof(short)));
        }

        public uint Flags
        {
            get => BitConverter.ToUInt32(Data[20..]);
            set => BitConverter.GetBytes(value).CopyTo(Data.Slice(20, sizeof(uint)));
        }

        public ushort Baud
        {
            get => BitConverter.ToUInt16(Data[24..]);
            set => BitConverter.GetBytes(value).CopyTo(Data.Slice(24, sizeof(ushort)));
        }

        public short Crdrat
        {
            get => BitConverter.ToInt16(Data[26..]);
            set => BitConverter.GetBytes(value).CopyTo(Data.Slice(26, sizeof(short)));
        }

        public short Nazapc
        {
            get => BitConverter.ToInt16(Data[28..]);
            set => BitConverter.GetBytes(value).CopyTo(Data.Slice(28, sizeof(short)));
        }

        public short Linlim
        {
            get => BitConverter.ToInt16(Data[30..]);
            set => BitConverter.GetBytes(value).CopyTo(Data.Slice(30, sizeof(short)));
        }

        public FarPtr Clsptr
        {
            get => new(Data.Slice(32, 4));
            set => value.Data.CopyTo(Data.Slice(32, FarPtr.Size));
        }

        public FarPtr Polrou
        {
            get => new FarPtr(Data.Slice(36, 4));
            set => value.Data.CopyTo(Data.Slice(36, FarPtr.Size));
        }

        public byte lcstat
        {
            get => Data[40];
            set => Data[40] = value;
        }

        /// <summary>
        ///    Size of the User Struct in Bytes
        /// </summary>
        public static ushort Size => 41;

        public User() : base(nameof(User), Size)
        {
            UserClass = 6;
            Minut4 = 0xA00;
            Baud = 38400;
        }
    }
}
