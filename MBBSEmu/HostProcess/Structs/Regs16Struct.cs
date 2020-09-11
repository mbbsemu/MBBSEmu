using System;

namespace MBBSEmu.HostProcess.Structs
{
    /// <summary>
    ///     REGS16 Struct defined in PHAPI.H
    ///
    ///     Used to pass Register values to Real Mode
    /// </summary>
    public class Regs16Struct
    {
        public ushort ES
        {
            get => BitConverter.ToUInt16(Data, 0);
            set => Array.Copy(BitConverter.GetBytes(value), 0, Data, 0, sizeof(ushort));
        }

        public ushort DS
        {
            get => BitConverter.ToUInt16(Data, 2);
            set => Array.Copy(BitConverter.GetBytes(value), 0, Data, 2, sizeof(ushort));
        }

        public ushort DI
        {
            get => BitConverter.ToUInt16(Data, 4);
            set => Array.Copy(BitConverter.GetBytes(value), 0, Data, 4, sizeof(ushort));
        }

        public ushort SI
        {
            get => BitConverter.ToUInt16(Data, 6);
            set => Array.Copy(BitConverter.GetBytes(value), 0, Data, 6, sizeof(ushort));
        }

        public ushort BP
        {
            get => BitConverter.ToUInt16(Data, 8);
            set => Array.Copy(BitConverter.GetBytes(value), 0, Data, 8, sizeof(ushort));
        }

        public ushort SP
        {
            get => BitConverter.ToUInt16(Data, 10);
            set => Array.Copy(BitConverter.GetBytes(value), 0, Data, 10, sizeof(ushort));
        }

        public ushort BX
        {
            get => BitConverter.ToUInt16(Data, 12);
            set => Array.Copy(BitConverter.GetBytes(value), 0, Data, 12, sizeof(ushort));
        }

        public ushort DX
        {
            get => BitConverter.ToUInt16(Data, 14);
            set => Array.Copy(BitConverter.GetBytes(value), 0, Data, 14, sizeof(ushort));
        }

        public ushort CX
        {
            get => BitConverter.ToUInt16(Data, 16);
            set => Array.Copy(BitConverter.GetBytes(value), 0, Data, 16, sizeof(ushort));
        }

        public ushort AX
        {
            get => BitConverter.ToUInt16(Data, 18);
            set => Array.Copy(BitConverter.GetBytes(value), 0, Data, 18, sizeof(ushort));
        }

        public ushort IP
        {
            get => BitConverter.ToUInt16(Data, 20);
            set => Array.Copy(BitConverter.GetBytes(value), 0, Data, 20, sizeof(ushort));
        }

        public ushort CS
        {
            get => BitConverter.ToUInt16(Data, 22);
            set => Array.Copy(BitConverter.GetBytes(value), 0, Data, 22, sizeof(ushort));
        }

        public ushort Flags
        {
            get => BitConverter.ToUInt16(Data, 24);
            set => Array.Copy(BitConverter.GetBytes(value), 0, Data, 24, sizeof(ushort));
        }

        public readonly byte[] Data = new byte[Size];

        public const ushort Size = 26;

        public Regs16Struct()
        {
            
        }

        public Regs16Struct(byte[] data)
        {
            if(data.Length < Size)
                throw new Exception($"Invalid Data Length. Expected {Size}, Received {data.Length}");

            Data = data;
        }
    }
}
