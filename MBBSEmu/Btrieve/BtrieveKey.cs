using MBBSEmu.Btrieve.Enums;
using MBBSEmu.Logging;
using NLog;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System;

namespace MBBSEmu.Btrieve
{
    /// <summary>
    ///     Represents a defined Btrieve Key entity
    ///
    ///     Btrieve Keys can contain N segments. By default Keys have one segment
    /// </summary>
    public class BtrieveKey
    {
        protected static readonly Logger _logger = LogManager.GetCurrentClassLogger(typeof(CustomLogger));

        public List<BtrieveKeyDefinition> Segments { get; set; }

        public ushort Number
        {
            get => PrimarySegment.Number;
        }

        public BtrieveKeyDefinition PrimarySegment
        {
            get => Segments[0];
        }

        public bool IsComposite
        {
            get => Segments.Count > 1;
        }

        public bool IsModifiable { get => PrimarySegment.IsModifiable; }

        public bool IsUnique { get => PrimarySegment.IsUnique; }

        public bool IsNullable { get => PrimarySegment.IsNullable; }

        public int Length
        {
            get => Segments.Sum(segment => segment.Length);
        }

        public string SqliteKeyName
        {
            get =>  $"key_{PrimarySegment.Number}";
        }

        public ReadOnlySpan<byte> ExtractKeyDataFromRecord(ReadOnlySpan<byte> record)
        {
            if (!IsComposite)
                return record.Slice(PrimarySegment.Offset, PrimarySegment.Length);

            var composite = new byte[Length];
            var i = 0;
            foreach (var segment in Segments)
            {
                var destSlice = composite.AsSpan(i, segment.Length);
                record.Slice(segment.Offset, segment.Length).CopyTo(destSlice);
                i += segment.Length;
            }

            return composite;
        }

        private static bool IsAllSameByteValue(ReadOnlySpan<byte> data, byte value)
        {
            foreach (byte b in data)
                if (b != value)
                    return false;

            return true;
        }

        public bool KeyInRecordIsAllSameByte(ReadOnlySpan<byte> record, byte b) => IsAllSameByteValue(ExtractKeyDataFromRecord(record), b);

        public bool KeyInRecordIsAllZero(ReadOnlySpan<byte> record) => KeyInRecordIsAllSameByte(record, 0);

        public object ExtractKeyInRecordToSQLiteObject(ReadOnlySpan<byte> data)
        {
            return KeyDataToSQLiteObject(ExtractKeyDataFromRecord(data));
        }

        /// <summary>
        ///     Returns an object suitable for inserting into sqlite for the specified
        ///     key data.
        /// </summary>
        public object KeyDataToSQLiteObject(ReadOnlySpan<byte> keyData)
        {
            if (IsNullable && IsAllSameByteValue(keyData, PrimarySegment.NullValue))
            {
                _logger.Info($"Returning a NULL value");
                return null;
            }

            if (IsComposite)
                return keyData.ToArray();

            switch (PrimarySegment.DataType)
            {
                case EnumKeyDataType.Unsigned:
                case EnumKeyDataType.UnsignedBinary:
                    switch (PrimarySegment.Length)
                    {
                        case 2:
                            return BitConverter.ToUInt16(keyData);
                        case 4:
                            return BitConverter.ToUInt32(keyData);
                        case 8:
                            return BitConverter.ToUInt64(keyData);
                        default:
                            throw new ArgumentException($"Bad unsigned integer key length {PrimarySegment.Length}");
                    }
                case EnumKeyDataType.AutoInc:
                case EnumKeyDataType.Integer:
                    switch (PrimarySegment.Length)
                    {
                        case 2:
                            return BitConverter.ToInt16(keyData);
                        case 4:
                            return BitConverter.ToInt32(keyData);
                        case 8:
                            return BitConverter.ToInt64(keyData);
                        default:
                            throw new ArgumentException($"Bad integer key length {PrimarySegment.Length}");
                    }
                case EnumKeyDataType.String:
                case EnumKeyDataType.Lstring:
                case EnumKeyDataType.Zstring:
                    return ExtractNullTerminatedString(keyData);
                default:
                    return keyData.ToArray();
            }
        }

        public static string ExtractNullTerminatedString(ReadOnlySpan<byte> b)
        {
            int strlen = b.IndexOf((byte) 0);
            if (strlen <= 0)
                strlen = b.Length;

            return Encoding.ASCII.GetString(b.Slice(0, strlen));
        }

        public string SqliteColumnType()
        {
            String type;

            if (IsComposite)
            {
                type = "BLOB";
            }
            else
            {
                switch (PrimarySegment.DataType)
                {
                    case EnumKeyDataType.AutoInc:
                        return "INTEGER NOT NULL UNIQUE";
                    case EnumKeyDataType.Integer:
                    case EnumKeyDataType.Unsigned:
                    case EnumKeyDataType.UnsignedBinary:
                        type = "INTEGER";
                        break;
                    case EnumKeyDataType.String:
                    case EnumKeyDataType.Lstring:
                    case EnumKeyDataType.Zstring:
                        type = "TEXT";
                        break;
                    default:
                        type = "BLOB";
                        break;
                }
            }

            if (!IsNullable)
            {
                type += " NOT NULL";
            }

            if (IsUnique)
            {
                type += " UNIQUE";
            }

            return type;
        }

        public BtrieveKey()
        {
            Segments = new List<BtrieveKeyDefinition>();
        }

        public BtrieveKey(BtrieveKeyDefinition keyDefinition)
        {
            Segments = new List<BtrieveKeyDefinition> {keyDefinition};
        }
    }
}
