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

        public bool IsUnique
        {
            get
            {
                var unique = true;
                Segments.ForEach(segment => unique &= segment.IsUnique);
                return unique;
            }
        }

        public bool IsNumeric
        {
            get
            {
                var numeric = true;
                Segments.ForEach(segment => numeric &= segment.IsNumeric);
                return numeric;
            }
        }

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

        public bool IsZeroValue(ReadOnlySpan<byte> record)
        {
            var convertible = (IConvertible) ToSQLiteObject(ExtractKeyDataFromRecord(record));
            return convertible.ToInt64(null) == 0;
        }

        public object ExtractToSQLiteObject(ReadOnlySpan<byte> data)
        {
            return ToSQLiteObject(ExtractKeyDataFromRecord(data));
        }

        /// <summary>
        ///     Returns an object suitable for inserting into sqlite for the specified
        ///     data.
        /// </summary>
        public object ToSQLiteObject(ReadOnlySpan<byte> data)
        {
            if (IsComposite)
                return data.ToArray();

            switch (PrimarySegment.DataType)
            {
                case EnumKeyDataType.Unsigned:
                case EnumKeyDataType.UnsignedBinary:
                    switch (PrimarySegment.Length)
                    {
                        case 2:
                            return BitConverter.ToUInt16(data);
                        case 4:
                            return BitConverter.ToUInt32(data);
                        case 8:
                            return BitConverter.ToUInt64(data);
                        default:
                            throw new ArgumentException($"Bad unsigned integer key length {PrimarySegment.Length}");
                    }
                case EnumKeyDataType.AutoInc:
                case EnumKeyDataType.Integer:
                    switch (PrimarySegment.Length)
                    {
                        case 2:
                            return BitConverter.ToInt16(data);
                        case 4:
                            return BitConverter.ToInt32(data);
                        case 8:
                            return BitConverter.ToInt64(data);
                        default:
                            throw new ArgumentException($"Bad integer key length {PrimarySegment.Length}");
                    }
                case EnumKeyDataType.String:
                case EnumKeyDataType.Lstring:
                case EnumKeyDataType.Zstring:
                    // very important to trim trailing nulls/etc
                    return ExtractNullTerminatedString(data);
                default:
                    return data.ToArray();
            }
        }

        public static string ExtractNullTerminatedString(ReadOnlySpan<byte> b)
        {
            int strlen = b.IndexOf((byte) 0);
            if (strlen < 0)
                strlen = b.Length;

            return Encoding.ASCII.GetString(b.Slice(0, strlen));
        }

        public string SqliteColumnType()
        {
            String type;

            if (IsComposite)
            {
                type = "BLOB NOT NULL";
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
                        type = "INTEGER NOT NULL";
                        break;
                    case EnumKeyDataType.String:
                    case EnumKeyDataType.Lstring:
                    case EnumKeyDataType.Zstring:
                        type = "TEXT NOT NULL";
                        break;
                    default:
                        type = "BLOB NOT NULL";
                        break;
                }
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
