using MBBSEmu.Btrieve;
using MBBSEmu.Btrieve.Enums;
using MBBSEmu.CPU;
using MBBSEmu.IO;
using MBBSEmu.Logging;
using MBBSEmu.Memory;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace MBBSEmu.DOS.Interrupts {
  enum BtrieveOpenMode : short {
    Normal = 0,
    Accelerated = -1,
    ReadOnly = -2,
    VerifyWriteOperations = -3,
    ExclusiveAccess = -4
  }

  public struct BtrieveCommand {
    public EnumBtrieveOperationCodes operation;

    public ushort position_block_offset;
    public ushort position_block_segment;

    public ushort data_buffer_offset;
    public ushort data_buffer_segment;

    public ushort data_buffer_length;

    public ushort key_buffer_offset;
    public ushort key_buffer_segment;

    public ushort key_buffer_length;

    public short key_number;
  }

  /// <summary>
  /// In-memory layout of the command received by the int7b Btrieve interrupt handler.
  ///
  /// Do not change this structure in any way.
  /// </summary>
  [StructLayout(LayoutKind.Sequential, Pack = 1)]
  public struct DOSInterruptBtrieveCommand {
    public ushort data_buffer_offset;
    public ushort data_buffer_segment;

    public ushort data_buffer_length;

    public ushort position_block_offset;
    public ushort position_block_segment;

    public ushort fcb_offset;
    public ushort fcb_segment;

    public EnumBtrieveOperationCodes operation;

    public ushort key_buffer_offset;
    public ushort key_buffer_segment;

    public byte key_buffer_length;

    public sbyte key_number;

    public ushort status_code_pointer_offset;
    public ushort status_code_pointer_segment;

    public ushort interface_id;  // should always be EXPECTED_INTERFACE_ID
  }

  // https://docs.actian.com/psql/psqlv13/index.html#page/btrieveapi/btrintro.htm
  // http://www.nomad.ee/btrieve/errors/index.shtml

  /// <summary>
  ///     Btrieve interrupt handler
  /// </summary>
  public class Int7Bh : IInterruptHandler, IDisposable {
    public const int BTRIEVE_COMMAND_STRUCT_LENGTH = 28;
    public const ushort EXPECTED_INTERFACE_ID = 0x6176;

    private readonly IMessageLogger _logger;
    private readonly IFileUtility _fileUtility;
    private readonly IMemoryCore _memory;
    private readonly ICpuRegisters _registers;
    private readonly Dictionary<Guid, BtrieveFileProcessor> _openFiles = new();
    private readonly string _path;

    public byte Vector => 0x7B;

    public Int7Bh(IFileUtility fileUtility, IMemoryCore memoryCore)
        : this(null, Directory.GetCurrentDirectory(), fileUtility, null, memoryCore) {}

    public Int7Bh(IMessageLogger logger, string path, IFileUtility fileUtility,
                  ICpuRegisters registers, IMemoryCore memory) {
      _logger = logger;
      _path = path;
      _fileUtility = fileUtility;
      _registers = registers;
      _memory = memory;
    }

    public void Dispose() {
      foreach (var db in _openFiles.Values) db.Dispose();

      _openFiles.Clear();
    }

    public void Handle() {
      /*
      // DS:DX is argument
      var command = ByteArrayToStructure<DOSInterruptBtrieveCommand>(_memory.GetArray(_registers.DS,
      _registers.DX, BTRIEVE_COMMAND_STRUCT_LENGTH).ToArray()); var status =
      BtrieveError.InvalidInterface; var data_buffer_length = command.data_buffer_length;

      if (command.interface_id != EXPECTED_INTERFACE_ID)
          _logger.Warn($"Client specified invalid interface_id {command.interface_id:X4}");
      else
          (status, data_buffer_length) = Handle(command);

      // return status code back to program
      _memory.SetWord(command.status_code_pointer_segment, command.status_code_pointer_offset,
      (ushort)status);
      // and update data_buffer_length if it was updated in Handle
      _memory.SetWord(_registers.DS, (ushort)(_registers.DX + 4), data_buffer_length);
      */
    }

    private (BtrieveError, ushort) Handle(DOSInterruptBtrieveCommand command) {
      return (BtrieveError.InvalidOperation, command.data_buffer_length);
      /*var actualCommand =
          new BtrieveCommand() { operation = command.operation,
                                 position_block_segment = command.position_block_segment,
                                 position_block_offset = command.position_block_offset,
                                 data_buffer_segment = command.data_buffer_segment,
                                 data_buffer_offset = command.data_buffer_offset,
                                 data_buffer_length = command.data_buffer_length,
                                 key_buffer_segment = command.key_buffer_segment,
                                 key_buffer_offset = command.key_buffer_offset,
                                 key_buffer_length = command.key_buffer_length,
                                 key_number = command.key_number };

      return Handle(actualCommand);
    }

    /// <summary>
    ///     Handles the btrieve command
    /// </summary>
    /// <returns>BtrieveError to return to the caller, as well as the data length returned from any
    /// operation returning data</returns>
    public (BtrieveError, ushort) Handle(BtrieveCommand command) {
      switch (command.operation) {
        case EnumBtrieveOperationCodes.Open:
          return (Open(command), command.data_buffer_length);
        case EnumBtrieveOperationCodes.Close:
          return (Close(command), command.data_buffer_length);
        case EnumBtrieveOperationCodes.Stat:
          return Stat(command);
        case EnumBtrieveOperationCodes.Delete:
          return (Delete(command), command.data_buffer_length);
        case EnumBtrieveOperationCodes.StepFirst:
        case EnumBtrieveOperationCodes.StepLast:
        case EnumBtrieveOperationCodes.StepNext:
        case EnumBtrieveOperationCodes.StepPrevious:
          return Step(command);
        case EnumBtrieveOperationCodes.AcquireFirst:
        case EnumBtrieveOperationCodes.AcquireLast:
        case EnumBtrieveOperationCodes.AcquireNext:
        case EnumBtrieveOperationCodes.AcquirePrevious:
        case EnumBtrieveOperationCodes.AcquireEqual:
        case EnumBtrieveOperationCodes.AcquireGreater:
        case EnumBtrieveOperationCodes.AcquireGreaterOrEqual:
        case EnumBtrieveOperationCodes.AcquireLess:
        case EnumBtrieveOperationCodes.AcquireLessOrEqual:
        case EnumBtrieveOperationCodes.QueryFirst:
        case EnumBtrieveOperationCodes.QueryLast:
        case EnumBtrieveOperationCodes.QueryNext:
        case EnumBtrieveOperationCodes.QueryPrevious:
        case EnumBtrieveOperationCodes.QueryEqual:
        case EnumBtrieveOperationCodes.QueryGreater:
        case EnumBtrieveOperationCodes.QueryGreaterOrEqual:
        case EnumBtrieveOperationCodes.QueryLess:
        case EnumBtrieveOperationCodes.QueryLessOrEqual:
          return Query(command);
        case EnumBtrieveOperationCodes.GetPosition:
          return GetPosition(command);
        case EnumBtrieveOperationCodes.GetDirectChunkOrRecord:
          return GetDirectRecord(command);
        case EnumBtrieveOperationCodes.Update:
          return (Update(command), command.data_buffer_length);
        case EnumBtrieveOperationCodes.Insert:
          return (Insert(command), command.data_buffer_length);
        default:
          _logger.Error($"Unsupported Btrieve operation {command.operation}");
          return (BtrieveError.InvalidOperation, command.data_buffer_length);
      }
    }

    private BtrieveError Open(BtrieveCommand command) {
      var file = Encoding.ASCII.GetString(_memory.GetString(
          command.key_buffer_segment, command.key_buffer_offset, stripNull: true));
      var openMode = (BtrieveOpenMode)command.key_number;
      // have to do a dance where we split up path + file since that's what
      // the processor wants
      string path = _path;

      if (Path.IsPathFullyQualified(file)) {
        path = Path.GetDirectoryName(file);
        file = Path.GetFileName(file);
      }

      BtrieveFileProcessor db;
      try {
        db = new(_fileUtility, path, file, cacheSize: 8) {
          BtrieveDriverMode = true,
        };
        // add to my list of open files
        var guid = Guid.NewGuid();
        _openFiles[guid] = db;

        // write the GUID in the pos block for other calls
        _memory.SetArray(command.position_block_segment, command.position_block_offset,
                         guid.ToByteArray());

        return BtrieveError.Success;
      } catch (FileNotFoundException) {
        _logger.Error($"Can't open btrieve file {file} with openMode {openMode}");
        return BtrieveError.FileNotFound;
      }
    }

    private BtrieveError Close(BtrieveCommand command) {
      var db = GetOpenDatabase(command);
      if (db == null)
        return BtrieveError.FileNotOpen;

      db.Dispose();
      _openFiles.Remove(GetGUIDFromPosBlock(command));

      return BtrieveError.Success;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct BtrieveFileSpec {
      public ushort record_length;
      public ushort page_size;
      public ushort number_of_keys;
      public uint number_of_records;
      public ushort flags;
      public ushort reserved;  // actually (byte) duplicate_pointers | (byte) unused
      public ushort unused_pages;

      public int WriteTo(IMemoryCore memoryCore, FarPtr ptr) {
        memoryCore.SetWord(ptr, record_length);
        memoryCore.SetWord(ptr + 2, page_size);
        memoryCore.SetWord(ptr + 4, number_of_keys);
        memoryCore.SetDWord(ptr + 6, number_of_records);
        memoryCore.SetWord(ptr + 10, flags);
        memoryCore.SetWord(ptr + 12, reserved);
        memoryCore.SetWord(ptr + 14, unused_pages);
        return 16;
      }

      public BtrieveFileSpec(BtrieveFileProcessor db) {
        record_length = (ushort)db.RecordLength;
        page_size = (ushort)db.PageLength;
        number_of_keys = (ushort)db.Keys.Count;
        number_of_records = (uint)db.GetRecordCount();
        flags = db.VariableLengthRecords ? (ushort)0x1 : (ushort)0x0;
        reserved = 0;
        unused_pages = 0;
      }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct BtrieveKeySpec {
      public ushort position;
      public ushort length;
      public ushort flags;
      public uint number_of_keys;
      public byte data_type;
      public byte null_value;
      public ushort unused;
      public byte number_only_if_explicit_key_flag_is_set;
      public byte acs_number;

      public BtrieveKeySpec(BtrieveKeyDefinition db) {
        position = db.Position;
        length = db.Length;
        flags = (ushort)db.Attributes;
        number_of_keys = 0;
        data_type = (byte)db.DataType;
        null_value = db.NullValue;
        unused = 0;
        number_only_if_explicit_key_flag_is_set = 0;
        acs_number = db.RequiresACS ? (byte)1 : (byte)0;
      }

      public int WriteTo(IMemoryCore memoryCore, FarPtr ptr) {
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

    private (BtrieveError, ushort) Stat(BtrieveCommand command) {
      var db = GetOpenDatabase(command);
      if (db == null)
        return (BtrieveError.FileNotOpen, command.data_buffer_length);

      // if they specify space for the expanded file name, null it out since we
      // don't support spanning
      if (command.key_buffer_length > 0)
        _memory.SetByte(command.key_buffer_segment, command.key_buffer_offset, 0);

      var requiredSize = (ushort)(Marshal.SizeOf(typeof(BtrieveFileSpec)) +
                                  (db.Keys.Count * Marshal.SizeOf(typeof(BtrieveKeySpec))));
      if (command.data_buffer_length < requiredSize)
        return (BtrieveError.DataBufferLengthOverrun, command.data_buffer_length);

      // now write all this data
      var ptr = new FarPtr(command.data_buffer_segment, command.data_buffer_offset);
      ptr += new BtrieveFileSpec(db).WriteTo(_memory, ptr);
      for (var i = (ushort)0; i < db.Keys.Count; ++i) {
        ptr += new BtrieveKeySpec(db.Keys[i].PrimarySegment).WriteTo(_memory, ptr);
      }

      return (BtrieveError.Success, requiredSize);
    }

    private BtrieveError Delete(BtrieveCommand command) {
      var db = GetOpenDatabase(command);
      if (db == null)
        return BtrieveError.FileNotOpen;

      if (!db.PerformOperation(-1, ReadOnlySpan<byte>.Empty, command.operation))
        return BtrieveError.InvalidPositioning;

      return BtrieveError.Success;
    }

    private BtrieveError Update(BtrieveCommand command) {
      var db = GetOpenDatabase(command);
      if (db == null)
        return BtrieveError.FileNotOpen;

      if (command.key_number >= 0 &&
          db.Keys[(ushort)command.key_number].Length > command.key_buffer_length)
        return BtrieveError.KeyBufferTooShort;

      var record = _memory
                       .GetArray(command.data_buffer_segment, command.data_buffer_offset,
                                 command.data_buffer_length)
                       .ToArray();
      var errorCode = db.Update(record);
      if (errorCode != BtrieveError.Success)
        return errorCode;

      // copy back the key if specified
      if (command.key_number >= 0)
        _memory.SetArray(command.key_buffer_segment, command.key_buffer_offset,
                         db.Keys[(ushort)command.key_number].ExtractKeyDataFromRecord(record));

      return BtrieveError.Success;
    }

    private BtrieveError Insert(BtrieveCommand command) {
      var db = GetOpenDatabase(command);
      if (db == null)
        return BtrieveError.FileNotOpen;

      if (command.key_number >= 0 &&
          db.Keys[(ushort)command.key_number].Length > command.key_buffer_length)
        return BtrieveError.KeyBufferTooShort;

      var record = _memory
                       .GetArray(command.data_buffer_segment, command.data_buffer_offset,
                                 command.data_buffer_length)
                       .ToArray();
      if (db.Insert(record, LogLevel.Error) == 0)
        return BtrieveError.DuplicateKeyValue;

      // copy back the key if specified
      if (command.key_number >= 0)
        _memory.SetArray(command.key_buffer_segment, command.key_buffer_offset,
                         db.Keys[(ushort)command.key_number].ExtractKeyDataFromRecord(record));

      return BtrieveError.Success;
    }

    private (BtrieveError, ushort) Step(BtrieveCommand command) {
      var db = GetOpenDatabase(command);
      if (db == null)
        return (BtrieveError.FileNotOpen, command.data_buffer_length);

      if (!db.PerformOperation(-1, ReadOnlySpan<byte>.Empty, command.operation))
        return (BtrieveError.EOF, command.data_buffer_length);

      var data = db.GetRecord();
      if (data.Length > command.data_buffer_length)
        return (BtrieveError.DataBufferLengthOverrun, command.data_buffer_length);

      _memory.SetArray(command.data_buffer_segment, command.data_buffer_offset, data);

      return (BtrieveError.Success, (ushort)data.Length);
    }

    private (BtrieveError, ushort) Query(BtrieveCommand command) {
      var length = command.data_buffer_length;

      var db = GetOpenDatabase(command);
      if (db == null)
        return (BtrieveError.FileNotOpen, length);

      var key = ReadOnlySpan<byte>.Empty;
      if (command.operation.RequiresKey())
        key = _memory.GetArray(command.key_buffer_segment, command.key_buffer_offset,
                               command.key_buffer_length);

      if (!db.PerformOperation(command.key_number, key, command.operation))
        return (command.operation.RequiresKey() ? BtrieveError.KeyValueNotFound : BtrieveError.EOF,
                length);

      var data = db.GetRecord();

      if (db.Keys[(ushort)command.key_number].Length > command.key_buffer_length)
        return (BtrieveError.KeyBufferTooShort, length);

      if (command.operation.AcquiresData()) {
        if (data.Length > command.data_buffer_length)
          return (BtrieveError.DataBufferLengthOverrun, length);

        // copy data
        _memory.SetArray(command.data_buffer_segment, command.data_buffer_offset, data);
        length = (ushort)data.Length;
      }

      // copy key
      _memory.SetArray(command.key_buffer_segment, command.key_buffer_offset,
                       db.Keys[(ushort)command.key_number].ExtractKeyDataFromRecord(data));

      return (BtrieveError.Success, length);
    }

    private (BtrieveError, ushort) GetDirectRecord(BtrieveCommand command) {
      var length = command.data_buffer_length;
      var db = GetOpenDatabase(command);
      if (db == null)
        return (BtrieveError.FileNotOpen, length);

      if (command.key_number == -2) {
        _logger.Warn("GetChunk - not supported");
        return (BtrieveError.InvalidOperation, length);
      }

      if (command.key_number >= 0 &&
          db.Keys[(ushort)command.key_number].Length > command.key_buffer_length)
        return (BtrieveError.KeyBufferTooShort, length);

      var offset = _memory.GetDWord(command.data_buffer_segment, command.data_buffer_offset);
      var record = db.GetRecord(offset, command.key_number);
      if (record == null)
        return (BtrieveError.InvalidPositioning, length);

      if (record.Data.Length > command.data_buffer_length)
        return (BtrieveError.DataBufferLengthOverrun, length);

      // copy data
      _memory.SetArray(command.data_buffer_segment, command.data_buffer_offset, record.Data);
      length = (ushort)record.Data.Length;

      // copy key
      if (command.key_number >= 0)
        _memory.SetArray(command.key_buffer_segment, command.key_buffer_offset,
                         db.Keys[(ushort)command.key_number].ExtractKeyDataFromRecord(record.Data));

      return (BtrieveError.Success, length);
    }

    private (BtrieveError, ushort) GetPosition(BtrieveCommand command) {
      var db = GetOpenDatabase(command);
      if (db == null)
        return (BtrieveError.FileNotOpen, command.data_buffer_length);

      if (command.data_buffer_length < 4)
        return (BtrieveError.DataBufferLengthOverrun, command.data_buffer_length);

      _memory.SetDWord(command.data_buffer_segment, command.data_buffer_offset, db.Position);
      return (BtrieveError.Success, 4);
    }

    private Guid GetGUIDFromPosBlock(BtrieveCommand command) => new Guid(
        _memory.GetArray(command.position_block_segment, command.position_block_offset, 16));

    /// <summary>
    /// Returns the open database from the given command's position block.
    /// </summary>
    /// <returns>A valid processor if already opened, or null if not found/not open</returns>
    private BtrieveFileProcessor GetOpenDatabase(BtrieveCommand command) {
      if (!_openFiles.TryGetValue(GetGUIDFromPosBlock(command), out var processor)) {
        processor = null;
      }
      return processor;
    }

    /// <summary>
    /// Unmarshals bytes into the appropriate structure
    /// </summary>
    public static unsafe T ByteArrayToStructure<T>(byte[] bytes)
        where T : struct {
      fixed(byte* ptr = &bytes[0]) {
        return (T)Marshal.PtrToStructure((IntPtr)ptr, typeof(T));
      }
    }

    /// <summary>
    /// Retrieves the BtrieveFileProcess from the specific guid - meant to be used only in tests.
    /// </summary>
    public BtrieveFileProcessor GetFromGUID(Guid guid) => _openFiles[guid];
  }*/
    }
  }
}
