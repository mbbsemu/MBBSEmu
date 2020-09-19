using System;
using System.IO;
using System.Text;

namespace MBBSEmu.HostProcess.Structs
{
    /// <summary>
    ///     Representation of the fndblk Struct
    ///
    ///     More Info: See DOSFACE.H
    /// </summary>
    public class FndblkStruct
    {
        public Guid Guid
        {
            get => new Guid(Data.AsSpan(0, 16));
            set => Array.Copy(value.ToByteArray(), Data, 16);
        }

        [Flags]
        public enum AttributeFlags : byte
        {
            ReadOnly = 1,
            Hidden = 1 << 1,
            System = 1 << 2,
            VolumeId = 1 << 3,
            Directory = 1 << 4,
            Archive = 1 << 5
        }

        // File attribute flags, a bitmask from AttributeFlags
        public byte Attributes
        {
            get => Data[21];
            set => Data[21] = value;
        }

        public void SetAttributes(FileAttributes fileAttributes)
        {
            AttributeFlags fndBlkAttributes = 0;

            if ((fileAttributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                fndBlkAttributes |= AttributeFlags.ReadOnly;

            if ((fileAttributes & FileAttributes.Hidden) == FileAttributes.Hidden)
                fndBlkAttributes |= AttributeFlags.Hidden;

            if ((fileAttributes & FileAttributes.System) == FileAttributes.System)
                fndBlkAttributes |= AttributeFlags.System;

            if ((fileAttributes & FileAttributes.Directory) == FileAttributes.Directory)
                fndBlkAttributes |= AttributeFlags.Directory;

            if ((fileAttributes & FileAttributes.Archive) == FileAttributes.Archive)
                fndBlkAttributes |= AttributeFlags.Archive;

            Attributes = (byte) fndBlkAttributes;
        }

        public DateTime DateTime
        {
          get {
            var time = BitConverter.ToUInt16(Data, 22);
            var date = BitConverter.ToUInt16(Data, 24);

            var year = ((date >> 9) & 0x7F) + 1980;
            var month = (date >> 5) & 0xF;
            var day = date & 0x1F;

            var hours = (time >> 11) & 0x1F;
            var minutes = (time >> 5) & 0x3F;
            var seconds = (time << 1) & 0x3E;

            return new DateTime(year, month, day, hours, minutes, seconds);
          }
          set {
            var time = (ushort)((value.Hour << 11) | (value.Minute << 5 ) | (value.Second >> 1));
            var date = (ushort)(((value.Year - 1980) << 9) | (value.Month << 5) | value.Day);

            Array.Copy(BitConverter.GetBytes(time), 0, Data, 22, 2);
            Array.Copy(BitConverter.GetBytes(date), 0, Data, 24, 2);
          }
        }

        public Int32 Size {
          get => BitConverter.ToInt32(Data, 26);
          set => Array.Copy(BitConverter.GetBytes(value), 0, Data, 26, 4);
        }

        public const int FilenameSize = 13;

        public byte[] NameBytes
        {
            get => new ReadOnlySpan<byte>(Data).Slice(30, FilenameSize).ToArray();
            set
            {
                Array.Copy(value, 0, Data, 30, value.Length);
                // null terminate to fill entire space
                for (var i = value.Length; i < FilenameSize; ++i)
                    Data[30 + i] = 0;
            }
        }

        public string Name
        {
            get => Encoding.ASCII.GetString(NameBytes).Replace("\0", "");
            set => NameBytes = Encoding.ASCII.GetBytes(value);
        }

        public const ushort StructSize = 43;

        public byte[] Data = new byte[StructSize];

        public FndblkStruct() {}

        public FndblkStruct(ReadOnlySpan<byte> structData) => Data = structData.ToArray();

    }
}
