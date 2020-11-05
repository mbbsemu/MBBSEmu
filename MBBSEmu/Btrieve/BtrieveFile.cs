using MBBSEmu.Btrieve.Enums;
using MBBSEmu.Extensions;
using NLog;
using System;
using System.Collections.Generic;
using System.IO;

namespace MBBSEmu.Btrieve
{
    /// <summary>
    ///     Represents an instance of a Btrieve File .DAT
    /// </summary>
    public class BtrieveFile
    {
        /// <summary>
        ///     Filename of Btrieve File
        /// </summary>
        public string FileName { get; set; }

        /// <summary>
        ///     Number of Pages within the Btrieve File
        /// </summary>
        public ushort PageCount => (ushort) (Data.Length / PageLength - 1);

        private ushort _recordCount;
        /// <summary>
        ///     Total Number of Records in the specified Btrieve File
        /// </summary>
        public ushort RecordCount
        {
            get
            {
                if (Data?.Length > 0)
                    return BitConverter.ToUInt16(Data, 0x1C);

                return _recordCount;
            }
            set
            {
                if (Data?.Length > 0)
                    Array.Copy(BitConverter.GetBytes(value), 0, Data, 0x1C, sizeof(ushort));

                _recordCount = value;
            }
        }

        /// <summary>
        ///     Whether the records are variable length
        /// </summary>
        public bool VariableLengthRecords { get; set; }

        private ushort _recordLength;
        /// <summary>
        ///     Defined Length of the records within the Btrieve File
        /// </summary>
        public ushort RecordLength
        {
            get
            {
                if (Data?.Length > 0)
                    return BitConverter.ToUInt16(Data, 0x16);

                return _recordLength;
            }
            set
            {
                if (Data?.Length > 0)
                    Array.Copy(BitConverter.GetBytes(value), 0, Data, 0x16, sizeof(ushort));

                _recordLength = value;
            }
        }

        private ushort _physicalRecordLength;
        /// <summary>
        ///     Actual Length of the records within the Btrieve File, including additional padding.
        /// </summary>
        public ushort PhysicalRecordLength
        {
            get
            {
                if (Data?.Length > 0)
                    return BitConverter.ToUInt16(Data, 0x18);

                return _physicalRecordLength;
            }
            set
            {
                if (Data?.Length > 0)
                    Array.Copy(BitConverter.GetBytes(value), 0, Data, 0x18, sizeof(ushort));

                _physicalRecordLength = value;
            }
        }

        private ushort _pageLength;
        /// <summary>
        ///     Defined length of each Page within the Btrieve File
        /// </summary>
        public ushort PageLength
        {
            get
            {
                if (Data?.Length > 0)
                    return BitConverter.ToUInt16(Data, 0x08);

                return _pageLength;
            }
            set
            {
                if (Data?.Length > 0)
                    Array.Copy(BitConverter.GetBytes(value), 0, Data, 0x08, sizeof(ushort));

                _pageLength = value;
            }
        }


        private ushort _keyCount;
        /// <summary>
        ///     Number of Keys defined within the Btrieve File
        /// </summary>
        public ushort KeyCount
        {
            get
            {
                if (Data?.Length > 0)
                    return BitConverter.ToUInt16(Data, 0x14);

                return _keyCount;
            }
            set
            {
                if (Data?.Length > 0)
                    Array.Copy(BitConverter.GetBytes(value), 0, Data, 0x14, sizeof(ushort));

                _keyCount = value;
            }
        }

        /// <summary>
        ///     Raw contents of Btrieve File
        /// </summary>
        private byte[] Data { get; set; }

        /// <summary>
        ///     Btrieve Records
        /// </summary>
        public List<BtrieveRecord> Records { get; set; }

        /// <summary>
        ///     Btrieve Keys
        /// </summary>
        public Dictionary<ushort, BtrieveKey> Keys { get; set; }

        private ILogger _logger;

        /// <summary>
        ///     Log Key is an internal value used by the Btrieve engine to track unique
        ///     records -- it adds 8 bytes to the end of the record that's not accounted for
        ///     in the RecordLength definition (but it is accounted for in PhysicalRecordLength).
        ///
        ///     <para/>This data is for completion purposes and not currently used.
        /// </summary>
        public bool LogKeyPresent { get; set; }

        /// <summary>
        ///     Set of absolute file position record offsets that are marked as deleted, and
        ///     therefore not loaded during initial load.
        /// </summary>
        public HashSet<uint> DeletedRecordOffsets { get; set; }

        public BtrieveFile()
        {
            Records = new List<BtrieveRecord>();
            Keys = new Dictionary<ushort, BtrieveKey>();
            DeletedRecordOffsets = new HashSet<uint>();
        }

        /// <summary>
        ///     Loads a Btrieve .DAT File
        /// </summary>
        public void LoadFile(ILogger logger, string path, string fileName)
        {
            //Sanity Check if we're missing .DAT files and there are available .VIR files that can be used
            var virginFileName = fileName.ToUpper().Replace(".DAT", ".VIR");
            if (!File.Exists(Path.Combine(path, fileName)) && File.Exists(Path.Combine(path, virginFileName)))
            {
                File.Copy(Path.Combine(path, virginFileName), Path.Combine(path, fileName));
                logger.Warn($"Created {fileName} by copying {virginFileName} for first use");
            }

            //If we're missing a DAT file, just bail. Because we don't know the file definition, we can't just create a "blank" one.
            if (!File.Exists(Path.Combine(path, fileName)))
            {
                logger.Error($"Unable to locate existing btrieve file {fileName}");
                throw new FileNotFoundException($"Unable to locate existing btrieve file {fileName}");
            }

            LoadFile(logger, Path.Combine(path, fileName));
        }

        public void LoadFile(ILogger logger, string fullPath)
        {
            _logger = logger;

            var fileName = Path.GetFileName(fullPath);
            var fileData = File.ReadAllBytes(fullPath);

            FileName = fullPath;
            Data = fileData;

            var (valid, errorMessage) = ValidateDatabase();
            if (!valid)
                throw new ArgumentException($"Failed to load database {FileName}: {errorMessage}");

#if DEBUG
            logger.Info($"Opened {fileName} and read {Data.Length} bytes");
#endif
            DeletedRecordOffsets = GetRecordPointerList(GetRecordPointer(0x10));

            LoadBtrieveKeyDefinitions(logger);
            //Only load records if there are any present
            if (RecordCount > 0)
                LoadBtrieveRecords(logger);
        }

        /// <summary>
        ///     Validates the Btrieve database being loaded
        /// </summary>
        /// <returns>True if valid. If false, the string is the error message.</returns>
        private (bool, string) ValidateDatabase()
        {
            if (Data[0] == 'F' && Data[1] == 'C')
                return (false, $"Cannot import v6 Btrieve database {FileName} - only v5 databases are supported for now. Please contact your ISV for a downgraded database.");
            if (Data[0] != 0 && Data[1] != 0 && Data[2] != 0 && Data[3] != 0)
                return (false, $"Doesn't appear to be a v5 Btrieve database {FileName}");

            var versionCode = Data[6] << 16 | Data[7];
            switch (versionCode)
            {
                case 3:
                case 4:
                case 5:
                    break;
                default:
                    return (false, $"Invalid version code [{versionCode}] in v5 Btrieve database {FileName}");
            }

            var needsRecovery = (Data[0x22] == 0xFF && Data[0x23] == 0xFF);
            if (needsRecovery)
                return (false, $"Cannot import Btrieve database {FileName} since it's marked inconsistent and needs recovery.");

            if (PageLength < 512 || (PageLength & 0x1FF) != 0)
                return (false, $"Invalid PageLength, must be multiple of 512 {FileName}");

            if (KeyCount <= 0)
                return (false, $"NO KEYS defined in {FileName}");

            var accelFlags = BitConverter.ToUInt16(Data.AsSpan().Slice(0xA, 2));
            if (accelFlags != 0)
                return (false, $"Valid accel flags, expected 0, got {accelFlags}! {FileName}");

            var usrflgs = BitConverter.ToUInt16(Data.AsSpan().Slice(0x106, 2));
            if ((usrflgs & 0x8) != 0)
                return (false, $"Data is compressed, cannot handle {FileName}");

            VariableLengthRecords = ((usrflgs & 0x1) != 0);
            var recordsContainVariableLength = (Data[0x38] == 0xFF);

            if (VariableLengthRecords ^ recordsContainVariableLength)
                return (false, "Mismatched variable length fields");

            return (true, "");
        }

        /// <summary>
        ///     Gets a record pointer offset at <paramref name="first"/> and then continues to walk
        ///     the chain of pointers until the end, returning all the offsets.
        /// </summary>
        /// <param name="first">Record pointer offset to start scanning from.</param>
        HashSet<uint> GetRecordPointerList(uint first)
        {
            HashSet<uint> ret = new HashSet<uint>();
            while (first != 0xFFFFFFFF)
            {
                ret.Add(first);

                first = GetRecordPointer(first);
            }

            return ret;
        }

        /// <summary>
        ///     Returns the record pointer located at absolute file offset <paramref name="offset"/>.
        /// </summary>
        private uint GetRecordPointer(uint offset) =>
            GetRecordPointer(Data.AsSpan().Slice((int)offset, 4));

        /// <summary>
        ///     Returns the record pointer located within the span starting at offset 0
        /// </summary>
        private uint GetRecordPointer(ReadOnlySpan<byte> data)
        {
            // 2 byte high word -> 2 byte low word
            return (uint)BitConverter.ToUInt16(data.Slice(0, 2)) << 16 | (uint)BitConverter.ToUInt16(data.Slice(2, 2));
        }

        /// <summary>
        ///     Loads Btrieve Key Definitions from the Btrieve DAT File Header
        /// </summary>
        private void LoadBtrieveKeyDefinitions(ILogger logger)
        {
            ushort keyDefinitionBase = 0x110;
            const ushort keyDefinitionLength = 0x1E;
            ReadOnlySpan<byte> btrieveFileContentSpan = Data;

            LogKeyPresent = (btrieveFileContentSpan[0x10C] == 1);

            ushort totalKeys = KeyCount;
            ushort currentKeyNumber = 0;
            while (currentKeyNumber < totalKeys)
            {
                var data = btrieveFileContentSpan.Slice(keyDefinitionBase, keyDefinitionLength).ToArray();

                EnumKeyDataType dataType;
                var attributes = (EnumKeyAttributeMask) BitConverter.ToUInt16(data, 0x8);
                if (attributes.HasFlag(EnumKeyAttributeMask.UseExtendedDataType))
                    dataType = (EnumKeyDataType) data[0x1C];
                else
                    dataType = attributes.HasFlag(EnumKeyAttributeMask.OldStyleBinary) ? EnumKeyDataType.OldBinary : EnumKeyDataType.OldAscii;

                var keyDefinition = new BtrieveKeyDefinition {
                    Number = currentKeyNumber,
                    Attributes = attributes,
                    DataType = dataType,
                    Offset = BitConverter.ToUInt16(data, 0x14),
                    Length = BitConverter.ToUInt16(data, 0x16),
                    Segment = attributes.HasFlag(EnumKeyAttributeMask.SegmentedKey),
                    SegmentOf = attributes.HasFlag(EnumKeyAttributeMask.SegmentedKey) ? currentKeyNumber : (ushort)0,
                    NullValue = data[0x1D],
                  };

                //If it's a segmented key, don't increment so the next key gets added to the same ordinal as an additional segment
                if (!keyDefinition.Segment)
                    currentKeyNumber++;

#if DEBUG
                logger.Info("----------------");
                logger.Info("Loaded Key Definition:");
                logger.Info("----------------");
                logger.Info($"Number: {keyDefinition.Number}");
                logger.Info($"Data Type: {keyDefinition.DataType}");
                logger.Info($"Attributes: {keyDefinition.Attributes}");
                logger.Info($"Offset: {keyDefinition.Offset}");
                logger.Info($"Length: {keyDefinition.Length}");
                logger.Info($"Segment: {keyDefinition.Segment}");
                logger.Info($"SegmentOf: {keyDefinition.SegmentOf}");
                logger.Info("----------------");
#endif
                if (!Keys.TryGetValue(keyDefinition.Number, out var key))
                {
                    key = new BtrieveKey(keyDefinition);
                    Keys.Add(keyDefinition.Number, key);
                }
                else
                {
                    key.Segments.Add(keyDefinition);
                }

                keyDefinitionBase += keyDefinitionLength;
            }

            // update segment indices
            foreach (var key in Keys)
            {
                var i = 0;
                foreach (var segment in key.Value.Segments)
                {
                    segment.SegmentIndex = i++;
                }
            }
        }

        /// <summary>
        ///     Loads Btrieve Records from Data Pages
        /// </summary>
        private void LoadBtrieveRecords(ILogger logger)
        {
            var recordsLoaded = 0;

            //Starting at 1, since the first page is the header
            for (var i = 1; i <= PageCount; i++)
            {
                var pageOffset = (uint)(PageLength * i);
                var recordsInPage = ((PageLength - 6) / PhysicalRecordLength);

                //Verify Data Page, high bit set on byte 5 (usage count)
                if ((Data[pageOffset + 0x5] & 0x80) == 0)
                    continue;

                //Page data starts 6 bytes in
                pageOffset += 6;
                for (var j = 0; j < recordsInPage; j++)
                {
                    if (recordsLoaded == RecordCount)
                        break;

                    var recordOffset = (uint)pageOffset + (uint)(PhysicalRecordLength * j);
                    // Marked for deletion? Skip
                    if (DeletedRecordOffsets.Contains(recordOffset))
                        continue;

                    var record = Data.AsSpan().Slice((int)recordOffset, PhysicalRecordLength);
                    if (IsUnusedRecord(record))
                        break;

                    var recordArray = new byte[RecordLength];
                    Array.Copy(Data, recordOffset, recordArray, 0, RecordLength);

                    if (VariableLengthRecords)
                    {
                        using var stream = new MemoryStream();
                        stream.Write(recordArray);

                        Records.Add(new BtrieveRecord((uint)recordOffset, GetVariableLengthData(recordOffset, stream)));
                    }
                    else
                        Records.Add(new BtrieveRecord((uint)recordOffset, recordArray));

                    recordsLoaded++;
                }
            }

            if (recordsLoaded != RecordCount)
            {
                logger.Warn($"Database {FileName} contains {RecordCount} records but only read {recordsLoaded}!");
            }
#if DEBUG
            logger.Info($"Loaded {recordsLoaded} records from {FileName}. Resetting cursor to 0");
#endif
        }

        /// <summary>
        ///     Returns true if the fixed record appears to be unused and should be skipped.
        ///
        ///     <para/>Fixed length records are contiguous in the page, and unused records are all zero except
        ///     for the first 4 bytes, which is a record pointer to the next free page.
        private bool IsUnusedRecord(ReadOnlySpan<byte> fixedRecordData)
        {
            if (fixedRecordData.Slice(4).ContainsOnly(0))
            {
                // additional validation, to ensure the record pointer is valid
                uint offset = GetRecordPointer(fixedRecordData);
                if (offset < Data.Length)
                    return true;
            }

            return false;
        }

        /// <summary>
        ///     Gets the complete variable length data from the specified <paramref name="recordOffset"/>,
        ///     walking through all data pages and returning the concatenated data.
        /// </summary>
        /// <param name="first">Fixed record pointer offset of the record from a data page</param>
        /// <param name="stream">MemoryStream containing the fixed record data already read.</param>
        private byte[] GetVariableLengthData(uint recordOffset, MemoryStream stream) {
            var variableData = Data.AsSpan().Slice((int)recordOffset + RecordLength, PhysicalRecordLength - RecordLength);
            var vrecPage = GetPageFromVariableLengthRecordPointer(variableData);
            var vrecFragment = variableData[3];

            while (true) {
                // invalid page? abort and return what we have
                if (vrecPage == 0xFFFFFF && vrecFragment == 0xFF)
                    return stream.ToArray();

                // jump to that page
                var vpage = Data.AsSpan().Slice((int)vrecPage * PageLength, PageLength);
                var numFragmentsInPage = BitConverter.ToUInt16(vpage.Slice(0xA, 2));
                // grab the fragment pointer
                var (offset, length, nextPointerExists) = GetFragment(vpage, vrecFragment, numFragmentsInPage);
                // now finally read the data!
                variableData = vpage.Slice((int)offset, (int)length);
                if (!nextPointerExists)
                {
                    // read all the data and reached the end!
                    stream.Write(variableData);
                    return stream.ToArray();
                }

                // keep going through more pages!
                vrecPage = GetPageFromVariableLengthRecordPointer(variableData);
                vrecFragment = variableData[3];

                stream.Write(variableData.Slice(4));
            }
        }

        /// <summary>
        ///     Returns data about the specified fragment.
        /// </summary>
        /// <param name="page">The entire page's data, will be PageLength in size</param>
        /// <param name="fragment">The fragment to lookup, 0 based</param>
        /// <param name="numFragments">The maximum number of fragments in the page.</param>
        /// <returns>Three items: 1) the offset within the page where the fragment data resides, 2)
        ///     the length of data contained in the fragment, and 3) a boolean indicating the fragment
        ///     has a "next pointer", meaning the fragment data is prefixed with 4 bytes of another
        ///     data page to continue reading from.
        /// </returns>
        private (uint, uint, bool) GetFragment(ReadOnlySpan<byte> page, uint fragment, uint numFragments)
        {
            var offsetPointer = (uint)PageLength - 2u * (fragment + 1u);
            var (offset, nextPointerExists) = GetPageOffsetFromFragmentArray(page.Slice((int)offsetPointer, 2));

            // to compute length, keep going until I read the next valid fragment and get its offset
            // then we subtract the two offets to compute length
            var nextFragmentOffset = offsetPointer;
            uint nextOffset = 0xFFFFFFFF;
            for (var i = fragment + 1; i <= numFragments; ++i)
            {
                nextFragmentOffset -= 2; // fragment array is at end of page and grows downward
                (nextOffset, _) = GetPageOffsetFromFragmentArray(page.Slice((int)nextFragmentOffset, 2));
                if (nextOffset == 0x7FFF)
                    continue;
                // valid offset, break now
                break;
            }

            // some sanity checks
            if (nextOffset == 0xFFFFFFFF)
                throw new ArgumentException($"Can't find next fragment offset {fragment} numFragments:{numFragments} {FileName}");

            var length = nextOffset - offset;
            // final sanity check
            if (offset < 0xC || (offset + length) > (PageLength - 2 * (numFragments + 1)))
                throw new ArgumentException($"Variable data overflows page {fragment} numFragments:{numFragments} {FileName}");

            return (offset, length, nextPointerExists);
        }

        /// <summary>
        ///     Reads the page offset from the fragment array
        /// </summary>
        /// <param name="arrayEntry">Fragment arran entry, size of 2 bytes</param>
        /// <returns>The offset and a boolean indicating the offset contains a next pointer</returns>
        private static (uint, bool) GetPageOffsetFromFragmentArray(ReadOnlySpan<byte> arrayEntry)
        {
            var offset = (uint)arrayEntry[0] | ((uint)arrayEntry[1] & 0x7F) << 8;
            var nextPointerExists = (arrayEntry[1] & 0x80) != 0;
            return (offset, nextPointerExists);
        }

        /// <summary>
        ///     Reads the variable length record pointer, which is contained in the first 4 bytes
        ///     of the footer after each fixed length record, and returns the page it points to.
        /// </summary>
        /// <param name="data">footer of the fixed record, at least 4 bytes in length</param>
        /// <returns>The page that this variable length record pointer points to</returns>
        private static uint GetPageFromVariableLengthRecordPointer(ReadOnlySpan<byte> data) {
            // high low mid, yep it's stupid
            return (uint)data[0] << 16 | (uint)data[1] | (uint)data[2] << 8;
        }
    }
}
