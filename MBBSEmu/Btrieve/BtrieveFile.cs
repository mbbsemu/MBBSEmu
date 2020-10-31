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

        /// <summary>
        ///     Log Key is an internal value used by the Btrieve engine to track unique
        ///     records -- it adds 8 bytes to the end of the record that's not accounted for
        ///     in the RecordLength definition (but it is accounted for in PhysicalRecordLength).
        ///
        ///     <para/>This data is for completion purposes and not currently used.
        /// </summary>
        public bool LogKeyPresent { get; set; }

        public BtrieveFile()
        {
            Records = new List<BtrieveRecord>();
            Keys = new Dictionary<ushort, BtrieveKey>();
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

            var fullPath = Path.Combine(path, fileName);
            var fileData = File.ReadAllBytes(fullPath);

            if (fileData[0] == 'F' && fileData[1] == 'C' && fileData[2] == 0 && fileData[3] == 0)
                throw new ArgumentException($"Cannot import v6 Btrieve database {fileName} - only v5 databases are supported for now. Please contact your ISV for a downgraded database.");

            FileName = fullPath;
            Data = fileData;

#if DEBUG
            logger.Info($"Opened {fileName} and read {Data.Length} bytes");
#endif
            //Only Parse Keys if they are defined
            if (KeyCount > 0)
                LoadBtrieveKeyDefinitions(logger);
            else
                throw new ArgumentException("NO KEYS defined in {fileName}");

            //Only load records if there are any present
            if (RecordCount > 0)
                LoadBtrieveRecords(logger);
        }

        /// <summary>
        ///     Loads Btrieve Key Definitions from the Btrieve DAT File Header
        /// </summary>
        private void LoadBtrieveKeyDefinitions(ILogger logger)
        {
            ushort keyDefinitionBase = 0x110;
            const ushort keyDefinitionLength = 0x1E;
            ReadOnlySpan<byte> btrieveFileContentSpan = Data;

            //Check for Log Key
            if (btrieveFileContentSpan[0x10C] == 1)
            {
                logger.Warn($"Btrieve Log Key Present in {FileName}");
                LogKeyPresent = true;
            }

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
                    Segment = false,
                    NullValue = data[0x1D],
                  };

                //If it's a segmented key, don't increment so the next key gets added to the same ordinal as an additional segment
                if (!keyDefinition.Attributes.HasFlag(EnumKeyAttributeMask.SegmentedKey))
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
                var pageOffset = (PageLength * i);
                var recordsInPage = (PageLength / PhysicalRecordLength);

                //Key Page
                if (BitConverter.ToUInt32(Data, pageOffset + 0x8) == uint.MaxValue)
                    continue;

                //Key Constraint Page
                if (Data[pageOffset + 0x6] == 0xAC)
                    continue;

                //Verify Data Page
                if (!Data[pageOffset + 0x5].IsNegative())
                {
                    logger.Warn(
                        $"Skipping Non-Data Page, might have invalid data - Page Start: 0x{pageOffset + 0x5:X4}");
                    continue;
                }

                //Page data starts 6 bytes in
                pageOffset += 6;
                for (var j = 0; j < recordsInPage; j++)
                {
                    if (recordsLoaded == RecordCount)
                        break;

                    var recordArray = new byte[RecordLength];
                    Array.Copy(Data, pageOffset + (PhysicalRecordLength * j), recordArray, 0, RecordLength);

                    //End of Page 0xFFFFFFFF
                    if (BitConverter.ToUInt32(recordArray, 0) == uint.MaxValue)
                        continue;

                    Records.Add(new BtrieveRecord((uint)(pageOffset + (PhysicalRecordLength * j)), recordArray));
                    recordsLoaded++;
                }
            }
#if DEBUG
            logger.Info($"Loaded {recordsLoaded} records from {FileName}. Resetting cursor to 0");
#endif
        }
    }
}
