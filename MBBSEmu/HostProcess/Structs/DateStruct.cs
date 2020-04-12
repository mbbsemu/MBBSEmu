using System;
using Microsoft.Extensions.Logging.EventLog;

namespace MBBSEmu.HostProcess.Structs
{
    /// <summary>
    ///     Representation of struct date defined in DOS.H
    /// </summary>
    public class DateStruct
    {
        public short year => BitConverter.ToInt16(Data, 0) == 0 ? (short)1970 : BitConverter.ToInt16(Data, 0);

        public byte day => Data[2] == 0 ? (byte)1 : Data[2];

        public byte month => Data[3] == 0 ? (byte)1 : Data[3];

        public readonly byte[] Data;

        public const ushort Size = 4;

        public DateStruct()
        {
            Data = new byte[Size];
        }

        public DateStruct(ReadOnlySpan<byte> data)
        {
            Data = data.ToArray();
        }

        public DateStruct(DateTime date)
        {
            Data = new byte[Size];
            Array.Copy(BitConverter.GetBytes((short) date.Year), 0, Data, 0, 2);
            Data[2] = (byte) date.Day;
            Data[3] = (byte) date.Month;
        }

        public ReadOnlySpan<byte> ToSpan() => Data;
    }
}
