using System;
using System.Collections.Generic;
using System.Text;

namespace MBBSEmu.HostProcess.Structs
{
    /// <summary>
    ///     Implementation of JMP_BUF struct for 16-bit Turbo C++ in SETJMP.H
    /// </summary>
    class JmpBufStruct
    {
        public ushort sp
        {
            get => BitConverter.ToUInt16(Data, 0);
            set => Array.Copy(BitConverter.GetBytes(value), 0, Data, 0, 2);
        }

        public ushort ss
        {
            get => BitConverter.ToUInt16(Data, 2);
            set => Array.Copy(BitConverter.GetBytes(value), 0, Data, 2, 2);
        }

        public ushort flag
        {
            get => BitConverter.ToUInt16(Data, 4);
            set => Array.Copy(BitConverter.GetBytes(value), 0, Data, 4, 2);
        }

        public ushort cs
        {
            get => BitConverter.ToUInt16(Data, 6);
            set => Array.Copy(BitConverter.GetBytes(value), 0, Data, 6, 2);
        }

        public ushort ip
        {
            get => BitConverter.ToUInt16(Data, 8);
            set => Array.Copy(BitConverter.GetBytes(value), 0, Data, 8, 2);
        }

        public ushort bp
        {
            get => BitConverter.ToUInt16(Data, 10);
            set => Array.Copy(BitConverter.GetBytes(value), 0, Data, 10, 2);
        }

        public ushort di
        {
            get => BitConverter.ToUInt16(Data, 12);
            set => Array.Copy(BitConverter.GetBytes(value), 0, Data, 12, 2);
        }

        public ushort es
        {
            get => BitConverter.ToUInt16(Data, 14);
            set => Array.Copy(BitConverter.GetBytes(value), 0, Data, 14, 2);
        }

        public ushort si
        {
            get => BitConverter.ToUInt16(Data, 16);
            set => Array.Copy(BitConverter.GetBytes(value), 0, Data, 16, 2);
        }

        public ushort ds
        {
            get => BitConverter.ToUInt16(Data, 18);
            set => Array.Copy(BitConverter.GetBytes(value), 0, Data, 18, 2);
        }

        public byte[] Data;

        public const ushort Size = 20;

        public JmpBufStruct()
        {
            Data = new byte[Size];
        }

        public JmpBufStruct(ReadOnlySpan<byte> data)
        {
            if(data.Length > Size)
                throw new OverflowException($"Data for JmpBuf is too long and will overflow: {data.Length} bytes");

            Data = data.ToArray();
        }

        public ReadOnlySpan<byte> ToSpan => new ReadOnlySpan<byte>(Data);

        public void FromSpan(ReadOnlySpan<byte> data) => Data = data.ToArray();
    }
}
