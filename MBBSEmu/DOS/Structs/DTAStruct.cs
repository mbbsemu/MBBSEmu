using System;
using System.Text;

namespace MBBSEmu.DOS.Structs
{
    /// <summary>
    ///     Struct Representing the structure of the DOS Data Transfer Area
    /// </summary>
    public class DTAStruct
    {
        public const byte FILENAME_SIZE = 13;
        public const byte SEARCHNAME_SIZE = 11;

        public byte AttributeOfSearch { get; set; }
        public byte DriveOfSearch { get; set; }
        public byte[] SearchName { get; set; }
        public ushort DirectoryEntryNumber { get; set; }
        public ushort StartingDirectoryClusterNumberDOS3 { get; set; }
        public ushort Reserved { get; set; }
        public ushort StartingDirectoryClusterNumberDOS2 { get; set; }
        public byte AttributeOfMatchingFile { get; set; }
        public ushort FileTime { get; set; }
        public ushort FileDate { get; set; }
        public ushort FileSize { get; set; }
        public byte[] FileName { get; set; }

        public const ushort Size = 43;

        private byte[] _data = new byte[Size];

        public byte[] Data
        {
            get
            {
                _data[0] = AttributeOfSearch;
                _data[1] = DriveOfSearch;
                Array.Copy(SearchName, 0, _data, 2, SEARCHNAME_SIZE);
                Array.Copy(BitConverter.GetBytes(DirectoryEntryNumber), 0, _data, 0xD, sizeof(ushort));
                Array.Copy(BitConverter.GetBytes(StartingDirectoryClusterNumberDOS3), 0, _data, 0xF, sizeof(ushort));
                Array.Copy(BitConverter.GetBytes(Reserved), 0, _data, 0x11, sizeof(ushort));
                Array.Copy(BitConverter.GetBytes(StartingDirectoryClusterNumberDOS2), 0, _data, 0x13, sizeof(ushort));
                _data[0x15] = AttributeOfMatchingFile;
                Array.Copy(BitConverter.GetBytes(FileTime), 0, _data, 0x16, sizeof(ushort));
                Array.Copy(BitConverter.GetBytes(FileDate), 0, _data, 0x18, sizeof(ushort));
                Array.Copy(BitConverter.GetBytes(FileSize), 0, _data, 0x1A, sizeof(ushort));
                Array.Copy(FileName, 0, _data, 0x1E, FILENAME_SIZE);
                return _data;
            }
            
            set
            {
                _data = value;
                var dataSpan = new ReadOnlySpan<byte>(_data);
                AttributeOfSearch = _data[0];
                DriveOfSearch = _data[1];
                SearchName = dataSpan.Slice(2, SEARCHNAME_SIZE).ToArray();
                DirectoryEntryNumber = BitConverter.ToUInt16(_data, 0xD);
                StartingDirectoryClusterNumberDOS3 = BitConverter.ToUInt16(_data, 0xF);
                Reserved = BitConverter.ToUInt16(_data, 0x11);
                StartingDirectoryClusterNumberDOS2 = BitConverter.ToUInt16(_data, 0x13);
                AttributeOfMatchingFile = _data[0x15];
                FileTime = BitConverter.ToUInt16(_data, 0x16);
                FileDate = BitConverter.ToUInt16(_data, 0x18);
                FileSize = BitConverter.ToUInt16(_data, 0x1A);
                FileName = dataSpan.Slice(0x1E, FILENAME_SIZE).ToArray();
            }
        }

        public DTAStruct(ReadOnlySpan<byte> value)
        {
            Data = value.ToArray();
        }
    }
}
