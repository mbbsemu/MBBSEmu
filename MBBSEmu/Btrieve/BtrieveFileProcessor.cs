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

    public uint RecordLength { get => 512; }

    public uint PageLength { get => 512; }

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
      int dwDataBufferLength = 0;
      int response = Wbtrv32.managedBtrcall((int)EnumBtrieveOperationCodes.Open, unmanagedPosBlock,
                                            null, ref dwDataBufferLength,
                                            System.Text.Encoding.ASCII.GetBytes(fileName), 0);
      if (response != 0) {
        throw new Exception("Can't open " + fileName);
      }

      _keys = new Dictionary<ushort, BtrieveKey>();
      byte[] statData = Stat();
      // TODO load data here
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
      fileSpec[4] = (byte)totalSegments;

      byte flags = 0;
      if (btrieveFile.VariableLengthRecords)
        flags |= 0x1;

      fileSpec[10] = flags;

      var keyOffset = 16;
      foreach (var key in btrieveFile.Keys.OrderBy(x => x.Key)) {
        foreach (var segment in key.Value.Segments) {
          BitConverter.TryWriteBytes(fileSpecSpan.Slice(keyOffset), segment.Position);
          BitConverter.TryWriteBytes(fileSpecSpan.Slice(keyOffset + 2), segment.Length);
          BitConverter.TryWriteBytes(fileSpecSpan.Slice(keyOffset + 4), (ushort)segment.Attributes);
          fileSpecSpan[keyOffset + 10] = (byte)segment.DataType;
          fileSpecSpan[keyOffset + 14] = (byte)segment.Number;
          // fileSpecSpan[keyOffset + 15] = segment.ACS != null ? (byte) 1 : (byte) 0;

          keyOffset += 16;
          // BitConverter.TryWriteBytes(fileSpecSpan.Slice(keyOffset + 6), segment.UniqueKeys);

          // BitConverter.TryWriteBytes(fileSpecSpan.Slice(keyOffset + 10), (byte)
          // segment.DataType);

          /*BitConverter.GetBytes(segment.Position).CopyTo(fileSpec, keyOffset);
BitConverter.GetBytes(segment.Length).CopyTo(fileSpec, keyOffset + 2);
BitConverter.GetBytes((ushort)segment.Attributes).CopyTo(fileSpec, keyOffset + 4);
fileSpec[keyOffset + 10] = (byte)segment.DataType;
fileSpec[keyOffset + 11] = segment.NullValue;
keyOffset += 16;*/
        }
      }

      // TODO write the ACS
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
      byte[] dataBuffer = new byte[0];
      int dwDataBufferLength = dataBuffer.Length;
      if (Wbtrv32.managedBtrcall((int)EnumBtrieveOperationCodes.Stat, unmanagedPosBlock, dataBuffer,
                                 ref dwDataBufferLength, null,
                                 0) != (int)BtrieveError.DataBufferLengthOverrun) {
        throw new Exception("Cannot stat db");
      }

      dataBuffer = new byte[dwDataBufferLength];

      if (Wbtrv32.managedBtrcall((int)EnumBtrieveOperationCodes.Stat, unmanagedPosBlock, dataBuffer,
                                 ref dwDataBufferLength, null, 0) != 0) {
        throw new Exception("Cannot stat db");
      }

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
                                 data, ref dwDataBufferLength, null, 0) == 0) {
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
    public BtrieveRecord GetRecord(uint offset) {
      byte[] data = new byte[readBufferSize];
      int dwDataBufferLength = data.Length;

      if (!BitConverter.TryWriteBytes(data.AsSpan(), offset)) {
        throw new Exception("Can't write length");
      }

      if (Wbtrv32.managedBtrcall((int)EnumBtrieveOperationCodes.GetDirectChunkOrRecord,
                                 unmanagedPosBlock, data, ref dwDataBufferLength, null, 0) != 0) {
        throw new Exception("Can't direct read");
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

      return Position;  // TODO do we need to return this value, or do clients care?
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
      // TODO implement this better
      StepFirst();
      while (Delete()) {
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

      LastUsedKey = keyNumber;
      LastUsedKeyData = key.ToArray();

      return Wbtrv32.managedBtrcall((ushort)btrieveOperationCode, unmanagedPosBlock, data,
                                    ref dwDataBufferLength, LastUsedKeyData, (byte)keyNumber) == 0;
    }
  }
}
