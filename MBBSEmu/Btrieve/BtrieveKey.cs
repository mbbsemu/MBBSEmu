using MBBSEmu.Btrieve.Enums;
using MBBSEmu.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MBBSEmu.Btrieve
{
    /// <summary>
    ///     Represents a defined Btrieve Key entity
    ///
    ///     Btrieve Keys can contain N segments. By default Keys have one segment
    /// </summary>
    public class BtrieveKey
    {
        protected static readonly IMessageLogger _logger = new LogFactory().GetLogger<MessageLogger>();

        public List<BtrieveKeyDefinition> Segments { get; set; }

        /// <summary>
        ///     Represents the key number, starting from 0. Each database has at least one key.
        /// </summary>
        public ushort Number => PrimarySegment.Number;

        /// <summary>
        ///     The primary segment in a key. Always first in the list of Segments.
        /// </summary>
        public BtrieveKeyDefinition PrimarySegment => Segments[0];

        /// <summary>
        ///     Whether the key is a composite key - composed of two or more segments.
        /// </summary>
        public bool IsComposite => Segments.Count > 1;

        /// <summary>
        ///     Whether the key data in the record can be modified once inserted.
        ///     <para/>All segmented keys in a composite key must have the same value.
        /// </summary>
        public bool IsModifiable => PrimarySegment.IsModifiable;

        /// <summary>
        ///     Whether the key data in the record is unique (no duplicates allowed).
        ///     <para/>All segmented keys in a composite key must have the same value.
        /// </summary>
        public bool IsUnique => PrimarySegment.IsUnique;

        /// <summary>
        ///     Whether the key data in the record is nullable.
        ///     <para/>All segmented keys in a composite key must have the same value.
        /// </summary>
        public bool IsNullable => PrimarySegment.IsNullable;

        /// <summary>
        ///     Whether this key requires ACS.
        /// </summary>
        public bool RequiresACS => Segments.Any(segment => segment.RequiresACS);

        /// <summary>
        ///     The ACS table of this key.
        /// </summary>
        public byte[] ACS => Segments.Where(segment => segment.ACS != null).DefaultIfEmpty(null).Select(segment => segment.ACS).First();

        /// <summary>
        ///     The total length in bytes of the key.
        /// </summary>
        public int Length => Segments.Sum(segment => segment.Length);

        /// <summary>
        ///     The key name used in the SQLite data_t table.
        /// </summary>
        public string SqliteKeyName => $"key_{PrimarySegment.Number}";

        /// <summary>
        ///     Returns a span of bytes containing the key value from record.
        /// </summary>
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

        /// <summary>
        ///      Returns true if data contains all of value.
        /// </summary>
        private static bool IsAllSameByteValue(ReadOnlySpan<byte> data, byte value)
        {
            foreach (byte b in data)
                if (b != value)
                    return false;

            return true;
        }

        /// <summary>
        ///     Returns true if the key data inside record contains all of b.
        /// </summary>
        public bool KeyInRecordIsAllSameByte(ReadOnlySpan<byte> record, byte b) => IsAllSameByteValue(ExtractKeyDataFromRecord(record), b);

        /// <summary>
        ///     Returns true if the key data inside record is all zero.
        /// </summary>
        public bool KeyInRecordIsAllZero(ReadOnlySpan<byte> record) => KeyInRecordIsAllSameByte(record, 0);

        /// <summary>
        ///     Returns an object that can be used for inserting into the data_t key column based on
        ///     the type of this key, extracting from data.
        /// </summary>
        public object ExtractKeyInRecordToSqliteObject(ReadOnlySpan<byte> data) => KeyDataToSqliteObject(ExtractKeyDataFromRecord(data));

        private ReadOnlySpan<byte> ApplyACS(ReadOnlySpan<byte> keyData)
        {
            if (!RequiresACS)
                return keyData;

            var dst = new byte[Length];
            var offset = 0;
            foreach (var segment in Segments)
            {
                var dstSpan = dst.AsSpan().Slice(offset, segment.Length);
                var key = keyData.Slice(offset, segment.Length);
                if (segment.RequiresACS)
                {
                    for (var i = 0; i < segment.Length; ++i)
                    {
                        dstSpan[i] = segment.ACS[key[i]];
                    }
                }
                else
                {
                    // simple copy
                    key.CopyTo(dstSpan);
                }

                offset += segment.Length;
            }

            return dst;
        }

        /// <summary>
        ///     Returns an object that can be used for inserting into the data_t key column based on
        ///     the type of this key from keyData.
        /// </summary>
        public object KeyDataToSqliteObject(ReadOnlySpan<byte> keyData)
        {
            if (IsNullable && IsAllSameByteValue(keyData, PrimarySegment.NullValue))
            {
                return DBNull.Value;
            }

            keyData = ApplyACS(keyData);

            if (IsComposite)
                return keyData.ToArray();

            switch (PrimarySegment.DataType)
            {
                case EnumKeyDataType.Unsigned:
                case EnumKeyDataType.UnsignedBinary:
                case EnumKeyDataType.OldBinary:
                    switch (PrimarySegment.Length)
                    {
                        case 2:
                            return BitConverter.ToUInt16(keyData);
                        case 4:
                            return BitConverter.ToUInt32(keyData);
                        case 6:
                            return (ulong)BitConverter.ToUInt32(keyData.Slice(0, 4)) | (((ulong)BitConverter.ToUInt16(keyData.Slice(4, 2))) << 32);
                        case 8:
                            return BitConverter.ToUInt64(keyData);
                        default:
                            // data is LSB, sqlite blobs compare msb (using memcmp), so swap bytes prior to insert
                            var copy = keyData.ToArray();
                            Array.Reverse<byte>(copy);
                            return copy;
                    }
                case EnumKeyDataType.AutoInc:
                case EnumKeyDataType.Integer:
                    switch (PrimarySegment.Length)
                    {
                        case 2:
                            return BitConverter.ToInt16(keyData);
                        case 4:
                            return BitConverter.ToInt32(keyData);
                        case 6:
                            return (long)BitConverter.ToUInt32(keyData.Slice(0, 4)) | (((long)BitConverter.ToInt16(keyData.Slice(4, 2))) << 32);
                        case 8:
                            return BitConverter.ToInt64(keyData);
                        default:
                            throw new ArgumentException($"Bad integer key length {PrimarySegment.Length}");
                    }
                case EnumKeyDataType.String:
                case EnumKeyDataType.Lstring:
                case EnumKeyDataType.Zstring:
                case EnumKeyDataType.OldAscii:
                    return ExtractNullTerminatedString(keyData);
                default:
                    return keyData.ToArray();
            }
        }

        /// <summary>
        ///     Returns a null terminated string from b. Length will be between 0 and b.Length.
        /// </summary>
        public static string ExtractNullTerminatedString(ReadOnlySpan<byte> b)
        {
            var strlen = b.IndexOf((byte)0);
            if (strlen <= 0)
                strlen = b.Length;

            return Encoding.ASCII.GetString(b.Slice(0, strlen));
        }

        /// <summary>
        ///     Returns the SQLite column type when creating the initial database.
        /// </summary>
        public string SqliteColumnType()
        {
            string type;

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
                    case EnumKeyDataType.Integer when PrimarySegment.Length <= 8:
                    case EnumKeyDataType.Unsigned when PrimarySegment.Length <= 8:
                    case EnumKeyDataType.UnsignedBinary when PrimarySegment.Length <= 8:
                    case EnumKeyDataType.OldBinary when PrimarySegment.Length <= 8:
                        type = "INTEGER";
                        break;
                    case EnumKeyDataType.String:
                    case EnumKeyDataType.Lstring:
                    case EnumKeyDataType.Zstring:
                    case EnumKeyDataType.OldAscii:
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
            Segments = new List<BtrieveKeyDefinition> { keyDefinition };
        }
    }
}
