using System;

namespace MBBSEmu.HostProcess.Structs
{
    /// <summary>
    ///     Representation of struct time defined in DOS.H
    /// </summary>
    public class TimeStruct
    {
        public byte minutes => Data[0];

        public byte hours => Data[1];

        public byte centiseconds => Data[2];

        public byte seconds => Data[3];

        public readonly byte[] Data;

        public const ushort Size = 4;

        public TimeStruct()
        {
            Data = new byte[Size];
        }

        public TimeStruct(ReadOnlySpan<byte> data)
        {
            Data = data.ToArray();
        }

        public TimeStruct(DateTime date)
        {
            Data = new byte[Size];
            Data[0] = (byte)date.Minute;
            Data[1] = (byte)date.Hour;
            Data[2] = 0;
            Data[3] = (byte)date.Second;
        }

        public ReadOnlySpan<byte> ToSpan => Data;
    }
}
