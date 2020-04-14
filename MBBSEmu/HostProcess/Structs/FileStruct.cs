using MBBSEmu.Memory;
using System;

namespace MBBSEmu.HostProcess.Structs
{
    /// <summary>
    ///     Representation of the C++ FILE Struct
    ///
    ///     More Info: See STDIO.H in Borland C++ INCLUDE folder
    /// </summary>
    public class FileStruct
    {
        /// <summary>
        ///     File Access Flags passed into C++ FOPEN method
        ///
        ///     Reference: http://www.cplusplus.com/reference/cstdio/fopen/
        /// </summary>
        [Flags]
        public enum EnumFileAccessFlags : byte
        {
            Read = 1,
            Write = 1 << 1,
            Append = 1 << 2,
            Update = 1 << 3,
            Binary = 1 << 4,
            Text = 1 << 5
        }

        [Flags]
        public enum EnumFileFlags : ushort
        {
            Read = 1,
            Write = 1 << 1,
            ReadWrite = 1 << 2,
            Buffered = 1 << 3,
            Error = 1 << 4,
            EOF = 1 << 5,
            Binary = 1 << 6,
            DataIncoming = 1 << 7,
            DataOutgoing = 1 << 8,
            FileIsATerminal = 1 << 9
        }

        //fill/empty level of buffer [0-1]
        public ushort level
        {
            get => BitConverter.ToUInt16(_fileStruct, 0);
            set => Array.Copy(BitConverter.GetBytes(value), 0, _fileStruct, 0, 2);
        }

        //File status flags [2-3]
        public ushort flags
        {
            get => BitConverter.ToUInt16(_fileStruct, 2);
            set => Array.Copy(BitConverter.GetBytes(value), 0, _fileStruct, 2, 2);
        }

        //File descriptor [4]
        public byte fd
        {
            get => _fileStruct[4];
            set => _fileStruct[4] = value;
        }

        //Ungetc char if no buffer [5]
        public byte hold {
            get => _fileStruct[5];
            set => _fileStruct[5] = value;
        }

        //Buffer size [6-7]
        public ushort bsize {
            get => BitConverter.ToUInt16(_fileStruct, 6);
            set => Array.Copy(BitConverter.GetBytes(value), 0, _fileStruct, 6, 2);
        }

        //Data transfer buffer [8-11]
        public IntPtr16 buffer
        {
            get
            {
                ReadOnlySpan<byte> fileSpan = _fileStruct;
                return new IntPtr16(fileSpan.Slice(8,4));
            }
            set => Array.Copy(value.ToArray(), 0, _fileStruct, 8, 4);
        }

        //Current active pointer [12-15]
        public IntPtr16 curp {
            get
            {
                ReadOnlySpan<byte> fileSpan = _fileStruct;
                return new IntPtr16(fileSpan.Slice(12, 4));
            }
            set => Array.Copy(value.ToArray(), 0, _fileStruct, 12, 4);
        }

        //Temporary file indicator [16]
        public byte istemp {
            get => _fileStruct[16];
            set => _fileStruct[16] = value;
        }

        //Used for validity checking [17-18]
        public short token
        {
            get => BitConverter.ToInt16(_fileStruct, 17);
            set => Array.Copy(BitConverter.GetBytes(value), 0, _fileStruct, 17, 2);
        }

        public const ushort Size = 19;

        private byte[] _fileStruct = new byte[19];

        public FileStruct() { }

        public FileStruct(ReadOnlySpan<byte> structData) => _fileStruct = structData.ToArray();

        public ReadOnlySpan<byte> ToSpan() => _fileStruct;

        public void FromSpan(ReadOnlySpan<byte> fileSpan) => _fileStruct = fileSpan.ToArray();

        /// <summary>
        ///     Sets the Initial FLAGS value on the FILE struct using the flags passed into FOPEN
        /// </summary>
        /// <param name="filesAccessFlags"></param>
        public void SetFlags(EnumFileAccessFlags filesAccessFlags)
        {
            if (filesAccessFlags.HasFlag(EnumFileAccessFlags.Read) &&
                filesAccessFlags.HasFlag(EnumFileAccessFlags.Write))
                flags |= (ushort) EnumFileFlags.ReadWrite;
            else if (filesAccessFlags.HasFlag(EnumFileAccessFlags.Read))
                flags |= (ushort) EnumFileFlags.Read;
            else if (filesAccessFlags.HasFlag(EnumFileAccessFlags.Write))
                flags |= (ushort) EnumFileFlags.Write;
        }

        /// <summary>
        ///     Parses File Access characters passed into FOPEN
        /// </summary>
        /// <param name="flags"></param>
        /// <returns></returns>
        public static EnumFileAccessFlags CreateFlagsEnum(ReadOnlySpan<byte> flags)
        {
            var result = EnumFileAccessFlags.Text;

            foreach (var f in flags)
            {
                switch ((char)f)
                {
                    case 'r':
                        result |= EnumFileAccessFlags.Read;
                        break;
                    case 'w':
                        result |= EnumFileAccessFlags.Write;
                        break;
                    case 'a':
                        result |= EnumFileAccessFlags.Append;
                        break;
                    case '+':
                        result |= EnumFileAccessFlags.Update;
                        break;
                    case 'b':
                    {
                        result &= ~EnumFileAccessFlags.Text;
                        result |= EnumFileAccessFlags.Binary;
                        break;
                    }
                    case 't':
                    {
                        result &= ~EnumFileAccessFlags.Binary;
                        result |= EnumFileAccessFlags.Text;
                        break;
                    }
                    default:
                        throw new ArgumentOutOfRangeException($"Unknown File Access Flag: {(char)f}");
                }
            }
            return result;
        }
    }
}