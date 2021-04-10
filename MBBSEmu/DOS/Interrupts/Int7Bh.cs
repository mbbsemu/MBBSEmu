
using MBBSEmu.Btrieve;
using MBBSEmu.CPU;
using MBBSEmu.IO;
using MBBSEmu.Memory;
using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace MBBSEmu.DOS.Interrupts
{
    public enum BtrieveError
    {
        Success = 0,
        InvalidOperation = 1,
        IOError = 2,
        FileNotOpen = 3,
        KeyValueNotFound = 4,
        DuplicateKeyValue = 5,
        InvalidKeyNumber = 6,
        DifferentKeyNumber = 7,
        InvalidPositioning = 8,
        EOF = 9 ,
        NonModifiableKeyValue = 10,
        InvalidFileName = 11,
        FileNotFound = 12,
        ExtendedFileError = 13,
        PreImageOpenError = 14,
        PreImageIOError = 15,
        ExpansionError = 16,
        CloseError = 17,
        DiskFull = 18,
        UnrecoverableError = 19,
        RecordManagerInactive = 20,
        KeyBufferTooShort = 21,
        DataBufferLengthOverrun = 22,
        PositionBlockLength = 23,
        PageSizeError = 24,
        CreateIOError = 25,
        InvalidNumberOfKeys = 26,
        InvalidKeyPosition = 27,
        BadRecordLength = 28,
        BadKeyLength = 29,
        NotBtrieveFile = 30,
        TransactionIsActive = 37,
        /* Btrieve version 5.x returns this status code
if you attempt to perform a Step, Update, or Delete operation on a
key-only file or a Get operation on a data only file */
        OperationNotAllowed = 41,
        AccessDenied = 46,
        InvalidInterface = 53,
    }

    enum BtrieveOpenMode : short
    {
        Normal = 0,
        Accelerated = -1,
        ReadOnly = -2,
        VerifyWriteOperations = -3,
        ExclusiveAccess = -4
    }

    public enum BtrieveOperation : ushort
    {
        Open = 0,
        Close = 1,
        Stat = 15,
    }

    public struct BtrieveCommand
    {
        public ushort data_buffer_offset;
        public ushort data_buffer_segment;

        public ushort data_buffer_length;

        public ushort position_block_offset;
        public ushort position_block_segment;

        public ushort fcb_offset;
        public ushort fcb_segment;

        public BtrieveOperation operation;

        public ushort key_buffer_offset;
        public ushort key_buffer_segment;

        public byte key_buffer_length;

        public byte key_number;

        public ushort status_code_pointer_offset;
        public ushort status_code_pointer_segment;

        public ushort interface_id; // should always be 0x6176
    }

    // https://docs.actian.com/psql/psqlv13/index.html#page/btrieveapi/btrintro.htm
    // http://www.nomad.ee/btrieve/errors/index.shtml

    /// <summary>
    ///     Btrieve interrupt handler
    /// </summary>
    public class Int7Bh : IInterruptHandler, IDisposable
    {
        public const int BTRIEVE_COMMAND_STRUCT_LENGTH = 28;

        private readonly ILogger _logger;
        private readonly IFileUtility _fileUtility;
        private readonly IMemoryCore _memory;
        private readonly ICpuRegisters _registers;
        private readonly Dictionary<Guid, BtrieveFileProcessor> _openFiles = new();

        public byte Vector => 0x7B;

        public Int7Bh(ILogger logger, IFileUtility fileUtility, ICpuRegisters registers, IMemoryCore memory)
        {
            _logger = logger;
            _fileUtility = fileUtility;
            _registers = registers;
            _memory = memory;
        }

        public void Dispose()
        {
            foreach (var db in _openFiles.Values)
                db.Dispose();

            _openFiles.Clear();
        }

        public void Handle()
        {
            // DS:DX is argument
            var command = ByteArrayToStructure<BtrieveCommand>(_memory.GetArray(_registers.DS, _registers.DX, BTRIEVE_COMMAND_STRUCT_LENGTH).ToArray());
            var status = BtrieveError.InvalidInterface;

            if (command.interface_id != 0x6176)
                _logger.Warn($"Client specified invalid interface_id {command.interface_id:X4}");
            else
                status = Handle(command);

            _memory.SetWord(command.status_code_pointer_segment, command.status_code_pointer_offset, (ushort)status);
        }

        /// <summary>
        ///     Handles the btrieve command
        /// </summary>
        /// <returns>BtrieveError to return to the caller</returns>
        private BtrieveError Handle(BtrieveCommand command)
        {
            switch (command.operation)
            {
                case BtrieveOperation.Open:
                    return Open(command);
                case BtrieveOperation.Close:
                    return Close(command);
                case BtrieveOperation.Stat:
                    return Stat(command);
                default:
                    return BtrieveError.InvalidOperation;
            }
        }

        private BtrieveError Open(BtrieveCommand command)
        {
            var file = Encoding.ASCII.GetString(_memory.GetString(command.key_buffer_segment, command.key_buffer_offset, stripNull: true));

            // have to do a dance where we split up path + file since that's what
            // the processor wants
            string path = null;

            if (Path.IsPathFullyQualified(file))
            {
                path = Path.GetDirectoryName(file);
                file = Path.GetFileName(file);
            }

            BtrieveFileProcessor db;
            try
            {
                db = new(_fileUtility, path, file, cacheSize: 8);
                // add to my list of open files
                var guid = Guid.NewGuid();
                _openFiles[guid] = db;

                // write the GUID in the pos block for other calls
                _memory.SetArray(command.position_block_segment, command.position_block_offset, guid.ToByteArray());

                return BtrieveError.Success;
            }
            catch (FileNotFoundException) {
                _logger.Error($"Can't open btrieve file {file}");
                return BtrieveError.FileNotFound;
            }
        }

        private BtrieveError Close(BtrieveCommand command)
        {
            var db = GetOpenDatabase(command);
            if (db == null)
                return BtrieveError.FileNotOpen;

            db.Dispose();
            _openFiles.Remove(GetGUIDFromPosBlock(command));

            return BtrieveError.Success;
        }

        public struct BtrieveFileSpec
        {
            public ushort record_length;
            public ushort page_size;
            public ushort number_of_keys;
            public uint number_of_records;
            public ushort flags;
            public ushort reserved; // actually (byte) duplicate_pointers | (byte) unused
            public ushort unused_pages;

            public int WriteTo(IMemoryCore memoryCore, FarPtr ptr)
            {
                memoryCore.SetWord(ptr, record_length);
                memoryCore.SetWord(ptr + 2, page_size);
                memoryCore.SetWord(ptr + 4, number_of_keys);
                memoryCore.SetDWord(ptr + 6, number_of_records);
                memoryCore.SetWord(ptr + 10, flags);
                memoryCore.SetWord(ptr + 12, reserved);
                memoryCore.SetWord(ptr + 14, unused_pages);
                return 16;
            }

            public BtrieveFileSpec(BtrieveFileProcessor db)
            {
                record_length = (ushort)db.RecordLength;
                page_size = (ushort)db.PageLength;
                number_of_keys = (ushort)db.Keys.Count;
                number_of_records = (uint)db.GetRecordCount();
                flags = db.VariableLengthRecords ? (ushort)0x1 : (ushort)0x0;
                reserved = 0;
                unused_pages = 0;
            }
        }

        public struct BtrieveKeySpec
        {
            public ushort position;
            public ushort length;
            public ushort flags;
            public uint number_of_keys;
            public byte data_type;
            public byte null_value;
            public ushort unused;
            public byte number_only_if_explicit_key_flag_is_set;
            public byte acs_number;

            public BtrieveKeySpec(BtrieveKey db)
            {
                position = db.PrimarySegment.Position;
                length = db.PrimarySegment.Length;
                flags = (ushort)db.PrimarySegment.Attributes;
                number_of_keys = 0;
                data_type = (byte)db.PrimarySegment.DataType;
                null_value = db.PrimarySegment.NullValue;
                unused = 0;
                number_only_if_explicit_key_flag_is_set = 0;
                acs_number = db.RequiresACS ? (byte)1 : (byte)0;
            }

            public int WriteTo(IMemoryCore memoryCore, FarPtr ptr)
            {
                memoryCore.SetWord(ptr, position);
                memoryCore.SetWord(ptr + 2, length);
                memoryCore.SetWord(ptr + 4, flags);
                memoryCore.SetDWord(ptr + 6, number_of_keys);
                memoryCore.SetByte(ptr + 10, data_type);
                memoryCore.SetByte(ptr + 11, null_value);
                memoryCore.SetWord(ptr + 12, unused);
                memoryCore.SetByte(ptr + 14, number_only_if_explicit_key_flag_is_set);
                memoryCore.SetByte(ptr + 15, acs_number);
                return 16;
            }
        }

        private BtrieveError Stat(BtrieveCommand command)
        {
            var db = GetOpenDatabase(command);
            if (db == null)
                return BtrieveError.FileNotOpen;

            // if they specify space for the expanded file name, null it out since we
            // don't support spanning
            if (command.key_buffer_length > 0)
                _memory.SetByte(command.key_buffer_segment, command.key_buffer_offset, 0);

            var requiredSize = Marshal.SizeOf(typeof(BtrieveFileSpec)) + (db.Keys.Count * Marshal.SizeOf(typeof(BtrieveKeySpec)));
            if (command.data_buffer_length < requiredSize)
                return BtrieveError.DataBufferLengthOverrun;
            // now write all this data
            BtrieveFileSpec fileSpec = new BtrieveFileSpec(db);

            var ptr = new FarPtr(command.data_buffer_segment, command.data_buffer_offset);
            ptr += fileSpec.WriteTo(_memory, ptr);
            for (var i = 0; i < db.Keys.Count; ++i)
            {
                ptr += new BtrieveKeySpec(db.Keys[(ushort)i]).WriteTo(_memory, ptr);
            }

            return BtrieveError.Success;
        }
        private Guid GetGUIDFromPosBlock(BtrieveCommand command) => new Guid(_memory.GetArray(command.position_block_segment, command.position_block_offset, 16));

        private BtrieveFileProcessor GetOpenDatabase(BtrieveCommand command) => _openFiles[GetGUIDFromPosBlock(command)];

        unsafe T ByteArrayToStructure<T>(byte[] bytes) where T : struct
        {
            fixed (byte* ptr = &bytes[0])
            {
                return (T)Marshal.PtrToStructure((IntPtr)ptr, typeof(T));
            }
        }
    }
}
