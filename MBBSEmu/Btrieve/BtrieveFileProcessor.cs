using MBBSEmu.Btrieve.Enums;
using MBBSEmu.Disassembler.Artifacts;
using MBBSEmu.IO;
using MBBSEmu.Logging;
using MBBSEmu.Util;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using SQLitePCL;
using System;
using System.CodeDom;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Threading.Channels;
using Terminal.Gui;

namespace MBBSEmu.Btrieve {
  /// <summary>
  ///     The BtrieveFileProcessor class is used to abstract the loading, parsing, and querying of
  ///     legacy Btrieve Files.
  ///
  ///     Legacy Btrieve files (.DAT) are converted on load to MBBSEmu format files (.DB), which
  ///     are Sqlite representations of the underlying Btrieve Data. This means the legacy .DAT
  ///     files are only used once on initial load and are not modified. All Inserts & Updates
  ///     happen within the new .DB Sqlite files.
  ///
  ///     These .DB files can be inspected and modified/edited cleanly once MBBSEmu has exited.
  ///     Attempting to modify the files during runtime is unsupported and will likely cause
  ///     concurrent access exceptions to fire in MBBSEmu.
  /// </summary>
  public class BtrieveFileProcessor : IDisposable {
    // const int ACS_LENGTH = 256;

    protected static readonly IMessageLogger _logger = new LogFactory().GetLogger<MessageLogger>();

    // private readonly IFileUtility _fileFinder;

    /// <summary>
    ///     The active connection to the Sqlite database.
    /// </summary>
    private IntPtr unmanagedPosBlock = Marshal.AllocHGlobal(128);

    public uint Position { get => GetPosition(); }

    public uint RecordLength { get; private set; }

    public uint PageLength { get => 512; }

    public bool VariableLengthRecords { get; private set; }

    private Dictionary<ushort, BtrieveKey> _keys;

    public Dictionary<ushort, BtrieveKey> Keys { get => _keys; }

    public int LastUsedKey { get; set; }

    public byte[] LastUsedKeyData { get; set; }

    /// <summary>
    ///     Closes all long lived resources, such as the Sqlite connection.
    /// </summary>
    public void Dispose() {
      Close();
      // close the database
      Marshal.FreeHGlobal(unmanagedPosBlock);
    }

    /// <summary>
    ///     Constructor to load the specified Btrieve File at the given Path
    /// </summary>
    /// <param name="fileUtility"></param>
    /// <param name="path"></param>
    /// <param name="fileName"></param>
    public BtrieveFileProcessor(IFileUtility fileUtility, string path, string fileName,
                                int cacheSize) {
      // wbtrv32.dll resolves relative filenames against its own process's current
      // directory, not the module path, so we must hand it a fully qualified path.
      var fullPath = Path.Combine(path, fileUtility.FindFile(path, fileName));

      // wbtrv32.dll's Open treats the key buffer as a null-terminated C string, not
      // fixed-length key data like every other operation, so it must be null-terminated.
      int dwDataBufferLength = 0;
      int response = Wbtrv32.managedBtrcall((int)EnumBtrieveOperationCodes.Open, unmanagedPosBlock,
                                            null, ref dwDataBufferLength,
                                            System.Text.Encoding.ASCII.GetBytes(fullPath + "\0"), 0);
      if (response != 0) {
        throw new Exception("Can't open " + fullPath);
      }

      _keys = new Dictionary<ushort, BtrieveKey>();
      byte[] statData = Stat();
      LoadFileSpec(statData);
    }

    /// <summary>
    ///     Parses the FILESPEC + KEYSPEC data returned by wbtrv32.dll's Stat operation into
    ///     this instance's RecordLength, VariableLengthRecords, and Keys.
    /// </summary>
    private void LoadFileSpec(byte[] statData) {
      var span = statData.AsSpan();

      RecordLength = BitConverter.ToUInt16(span.Slice(0, 2));

      var fileFlags = BitConverter.ToUInt16(span.Slice(10, 2));
      VariableLengthRecords = (fileFlags & 0x1) != 0;

      var segmentCount = (statData.Length - 16) / 16;
      BtrieveKey currentKey = null;

      for (var i = 0; i < segmentCount; ++i) {
        var segment = span.Slice(16 + (i * 16), 16);

        var number = segment[14];
        var keyDefinition = new BtrieveKeyDefinition {
          Number = number,
          Length = BitConverter.ToUInt16(segment.Slice(2, 2)),
          Offset = (ushort)(BitConverter.ToUInt16(segment.Slice(0, 2)) - 1),
          DataType = (EnumKeyDataType)segment[10],
          Attributes = (EnumKeyAttributeMask)BitConverter.ToUInt16(segment.Slice(4, 2)),
          NullValue = segment[11],
          SegmentOf = number,
        };

        if (currentKey != null && currentKey.Number == number) {
          keyDefinition.Segment = true;
          keyDefinition.SegmentIndex = currentKey.Segments.Count;
          currentKey.Segments.Add(keyDefinition);
        } else {
          currentKey = new BtrieveKey(keyDefinition);
          _keys[number] = currentKey;
        }
      }
    }

    /*

#define Duplicates (1 << 0)
#define Modifiable (1 << 1)
#define OldStyleBinary (1 << 2)
#define NullAllSegments (1 << 3)
#define SegmentedKey (1 << 4)
#define NumberedACS (1 << 5)
#define DescendingKeySegment (1 << 6)
#define RepeatingDuplicatesKey (1 << 7)
#define UseExtendedDataType (1 << 8)
#define NullAnySegment (1 << 9)
#define MultipleACS ((1 << 10) | NumberedACS)

    typedef struct _tagFILESPEC {
    uint16_t logicalFixedRecordLength; 0
    uint16_t pageSize; 2
    uint8_t numberOfKeys; 4
    uint8_t fileVersion;  5 // not always set
    uint32_t recordCount; 6
    uint16_t fileFlags; 10
    uint8_t numExtraPointers; 12
    uint8_t physicalPageSize; 13
    uint16_t preallocatedPages; 14
  } FILESPEC, *LPFILESPEC;

  typedef struct _tagKEYSPEC {
    uint16_t position; 0
    uint16_t length; 2
    uint16_t attributes; 4
    uint32_t uniqueKeys; 6
    uint8_t extendedDataType; 10
    uint8_t nullValue; 11
    uint16_t reserved; 12
    uint8_t number; 14
    uint8_t acsNumber; 15
  } KEYSPEC, *LPKEYSPEC;

  typedef struct _tagACSCREATEDATA {
    uint8_t header;  // should be 0xAC
    char name[8];    8 // not necessarily null terminated
    char acs[256];   9 // the table itself
  } ACSCREATEDATA, *LPACSCREATEDATA;
*/
    public BtrieveFileProcessor(BtrieveFile btrieveFile) {
      int totalSegments = (byte)btrieveFile.Keys.Sum(x => x.Value.Segments.Count);
      var fileSpec = new byte[16 + (totalSegments * 16) + (btrieveFile.ACS != null ? 265 : 0)];
      var fileSpecSpan = fileSpec.AsSpan();

      BitConverter.TryWriteBytes(fileSpecSpan, btrieveFile.RecordLength);
      BitConverter.TryWriteBytes(fileSpecSpan.Slice(2), btrieveFile.PageLength);
      // numberOfKeys is the count of logical keys, not segments -- a composite key with
      // multiple segments still only counts once (wbtrv32.dll walks segments itself via
      // the SegmentedKey attribute).
      fileSpec[4] = (byte)btrieveFile.Keys.Count;

      byte flags = 0;
      if (btrieveFile.VariableLengthRecords)
        flags |= 0x1;

      fileSpec[10] = flags;

      // physicalPageSize == 0xFF tells wbtrv32.dll to create the database in-memory
      // rather than on disk; that path is also what avoids it dereferencing lpKeyBuffer
      // as a filename, which we don't pass one for.
      fileSpec[13] = 0xFF;

      // wbtrv32.dll only supports a single, shared ACS table (matching BtrieveFile's own
      // model -- one ACS applies to whichever keys require it), so any key needing ACS
      // always references table 0.
      const byte acsNumber = 0;

      var keyOffset = 16;
      foreach (var key in btrieveFile.Keys.OrderBy(x => x.Key)) {
        foreach (var segment in key.Value.Segments) {
          BitConverter.TryWriteBytes(fileSpecSpan.Slice(keyOffset), segment.Position);
          BitConverter.TryWriteBytes(fileSpecSpan.Slice(keyOffset + 2), segment.Length);
          BitConverter.TryWriteBytes(fileSpecSpan.Slice(keyOffset + 4), (ushort)segment.Attributes);
          fileSpecSpan[keyOffset + 10] = (byte)segment.DataType;
          fileSpecSpan[keyOffset + 11] = segment.NullValue;
          fileSpecSpan[keyOffset + 14] = (byte)segment.Number;
          fileSpecSpan[keyOffset + 15] = segment.RequiresACS ? acsNumber : (byte)0;

          keyOffset += 16;
        }
      }

      if (btrieveFile.ACS != null) {
        // ACSCREATEDATA: header (0xAC), 8-byte name, 256-byte table
        fileSpecSpan[keyOffset] = 0xAC;
        var nameBytes = Encoding.ASCII.GetBytes((btrieveFile.ACSName ?? string.Empty).PadRight(8).Substring(0, 8));
        nameBytes.CopyTo(fileSpecSpan.Slice(keyOffset + 1, 8));
        btrieveFile.ACS.AsSpan(0, 256).CopyTo(fileSpecSpan.Slice(keyOffset + 9, 256));
        keyOffset += 265;
      }

      int dwDataBufferLength = fileSpec.Length;

      var rc = Wbtrv32.managedBtrcall((ushort)EnumBtrieveOperationCodes.Create, unmanagedPosBlock,
                                      fileSpec, ref dwDataBufferLength, null, 0);

      if (rc != 0) {
        throw new Exception("Failed to create in memory database");
      }

      _keys = btrieveFile.Keys;

      foreach (var record in btrieveFile.Records) {
        Insert(record.Data);
      }
    }

    private void Close() {
      int dwDataBufferLength = 0;
      Wbtrv32.managedBtrcall((int)EnumBtrieveOperationCodes.Close, unmanagedPosBlock, null,
                             ref dwDataBufferLength, null, 0);
    }

    private byte[] Stat() {
      // wbtrv32.dll's Stat implementation returns DataBufferLengthOverrun without
      // reporting the required size when the buffer is too small, so there's no way
      // to size the buffer via a first probing call. Use a buffer generously large
      // enough to hold the FILESPEC plus every KEYSPEC entry instead.
      byte[] dataBuffer = new byte[readBufferSize];
      int dwDataBufferLength = dataBuffer.Length;
      if (Wbtrv32.managedBtrcall((int)EnumBtrieveOperationCodes.Stat, unmanagedPosBlock, dataBuffer,
                                 ref dwDataBufferLength, null, 0) != 0) {
        throw new Exception("Cannot stat db");
      }

      Array.Resize(ref dataBuffer, dwDataBufferLength);

      return dataBuffer;
    }

    public int GetRecordCount() => BitConverter.ToInt32(Stat().AsSpan().Slice(6));

    private const int readBufferSize = 4096;
    /// <summary>
    ///     Sets Position to the offset of the first Record in the loaded Btrieve File.
    /// </summary>
    private bool StepFirst() {
      byte[] data = new byte[readBufferSize];
      int dwDataBufferLength = data.Length;
      return Wbtrv32.managedBtrcall((int)EnumBtrieveOperationCodes.StepFirst, unmanagedPosBlock,
                                    data, ref dwDataBufferLength, null, 0) == 0;
    }

    /// <summary>
    ///     Sets Position to the offset of the next logical Record in the loaded Btrieve File.
    /// </summary>
    private bool StepNext() {
      byte[] data = new byte[readBufferSize];
      int dwDataBufferLength = data.Length;
      return Wbtrv32.managedBtrcall((int)EnumBtrieveOperationCodes.StepNext, unmanagedPosBlock,
                                    data, ref dwDataBufferLength, null, 0) == 0;
    }

    /// <summary>
    ///     Sets Position to the offset of the previous logical record in the loaded Btrieve File.
    /// </summary>
    private bool StepPrevious() {
      byte[] data = new byte[readBufferSize];
      int dwDataBufferLength = data.Length;
      return Wbtrv32.managedBtrcall((int)EnumBtrieveOperationCodes.StepPrevious, unmanagedPosBlock,
                                    data, ref dwDataBufferLength, null, 0) == 0;
    }

    /// <summary>
    ///     Sets Position to the offset of the last Record in the loaded Btrieve File.
    /// </summary>
    private bool StepLast() {
      byte[] data = new byte[readBufferSize];
      int dwDataBufferLength = data.Length;
      return Wbtrv32.managedBtrcall((int)EnumBtrieveOperationCodes.StepLast, unmanagedPosBlock,
                                    data, ref dwDataBufferLength, null, 0) == 0;
    }

    private uint GetPosition() {
      byte[] data = new byte[4];
      int dwDataBufferLength = data.Length;
      if (Wbtrv32.managedBtrcall((int)EnumBtrieveOperationCodes.GetPosition, unmanagedPosBlock,
                                 data, ref dwDataBufferLength, null, 0) != 0) {
        throw new Exception("Can't get position");
      }

      return BitConverter.ToUInt32(data.AsSpan());
    }

    /// <summary>
    ///     Returns the Record at the current Position
    /// </summary>
    /// <returns></returns>
    public byte[] GetRecord() => GetRecord(Position)?.Data;

    /// <summary>
    ///     Returns the Record at the specified physical offset, while also updating Position to
    ///     match.
    /// </summary>
    public BtrieveRecord GetRecord(uint offset) => GetRecord(offset, -1);

    /// <summary>
    ///     Returns the Record at the specified physical offset, while also updating Position to
    ///     match. If keyNumber is >= 0, also establishes that key's logical currency at this
    ///     position, so that a subsequent AcquireNext/QueryNext continues on from here rather
    ///     than from wherever the last key-based query left off.
    ///
    ///     Returns null if there's no record at that offset (e.g. an invalid/deleted position),
    ///     matching Btrieve's usual "not found" semantics rather than throwing.
    /// </summary>
    public BtrieveRecord GetRecord(uint offset, int keyNumber) {
      byte[] data = new byte[readBufferSize];
      int dwDataBufferLength = data.Length;

      if (!BitConverter.TryWriteBytes(data.AsSpan(), offset)) {
        throw new Exception("Can't write length");
      }

      byte[] keyBuffer = null;
      if (keyNumber >= 0 && Keys.TryGetValue((ushort)keyNumber, out var btrieveKey)) {
        keyBuffer = new byte[btrieveKey.Length];
        LastUsedKey = keyNumber;
      }

      if (Wbtrv32.managedBtrcall((int)EnumBtrieveOperationCodes.GetDirectChunkOrRecord,
                                 unmanagedPosBlock, data, ref dwDataBufferLength, keyBuffer,
                                 (byte)keyNumber) != 0) {
        return null;
      }

      Array.Resize(ref data, dwDataBufferLength);

      return new BtrieveRecord(offset, data);
    }

    /// <summary>
    ///     Updates the Record at the current Position.
    /// </summary>
    public BtrieveError Update(byte[] record) {
      int dwDataBufferLength = record.Length;
      return (BtrieveError)Wbtrv32.managedBtrcall((int)EnumBtrieveOperationCodes.Update,
                                                  unmanagedPosBlock, record, ref dwDataBufferLength,
                                                  null, 0xFF);
    }

    /// <summary>
    ///     Inserts a new Btrieve Record.
    /// </summary>
    /// <return>Position of the newly inserted item, or 0 on failure</return>
    public uint Insert(byte[] record) {
      int dwDataBufferLength = record.Length;
      if (Wbtrv32.managedBtrcall((int)EnumBtrieveOperationCodes.Insert, unmanagedPosBlock, record,
                                 ref dwDataBufferLength, null, 0xFF) != 0) {
        return 0;
      }

      return Position;
    }

    /// <summary>
    ///     Deletes the Btrieve Record at the Current Position within the File.
    /// </summary>
    public bool Delete() {
      int dwDataBufferLength = 0;
      return Wbtrv32.managedBtrcall((int)EnumBtrieveOperationCodes.Delete, unmanagedPosBlock, null,
                                    ref dwDataBufferLength, null, 0) == 0;
    }

    /// <summary>
    ///     Deletes all records within the current Btrieve File.
    /// </summary>
    public bool DeleteAll() {
      // Deleting invalidates the current position, so we have to re-establish it via
      // StepFirst before every delete rather than just stepping through once.
      while (StepFirst()) {
        if (!Delete())
          return false;
      }
      return true;
    }

    /// <summary>
    ///     Performs a Key Based Query on the loaded Btrieve File
    /// </summary>
    /// <param name="keyNumber">Which key to query against</param>
    /// <param name="key">The key data to query against</param>
    /// <param name="btrieveOperationCode">Which query to perform</param>
    /// <param name="newQuery">true to start a new query, false to continue a prior one</param>
    public bool PerformOperation(int keyNumber, ReadOnlySpan<byte> key,
                                 EnumBtrieveOperationCodes btrieveOperationCode) {
      byte[] data = new byte[readBufferSize];
      int dwDataBufferLength = data.Length;

      // Operations that continue a previous query (e.g. AcquireNext) don't get a
      // meaningful key number from their caller -- wbtrv32.dll requires the key
      // number to match the one used to establish that query, so reuse it here
      // rather than whatever placeholder the caller passed in.
      if (btrieveOperationCode.UsesPreviousQuery())
        keyNumber = LastUsedKey;
      else
        LastUsedKey = keyNumber;

      // wbtrv32.dll always writes the found record's key back into the key buffer
      // for Acquire/Query operations, even ones (like AcquireNext) that don't need
      // key criteria to perform the search, so the buffer must be large enough to
      // hold a full key value rather than whatever (possibly empty) key data the
      // caller supplied.
      if (Keys.TryGetValue((ushort)keyNumber, out var btrieveKey) && key.Length < btrieveKey.Length) {
        var keyBuffer = new byte[btrieveKey.Length];
        key.CopyTo(keyBuffer);
        LastUsedKeyData = keyBuffer;
      } else {
        LastUsedKeyData = key.ToArray();
      }

      return Wbtrv32.managedBtrcall((ushort)btrieveOperationCode, unmanagedPosBlock, data,
                                    ref dwDataBufferLength, LastUsedKeyData, (byte)keyNumber) == 0;
    }
  }
}
