using FluentAssertions;
using MBBSEmu.Btrieve;
using MBBSEmu.Btrieve.Enums;
using MBBSEmu.DependencyInjection;
using MBBSEmu.Resources;
using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Xunit;

namespace MBBSEmu.Tests.Btrieve
{
    public class BtrieveKey_Tests : TestBase
    {
        private static readonly byte[] DATA_NEGATIVE = { 0xF1, 0xF2, 0xF3, 0xF4, 0xF5, 0xF6, 0xF7, 0xF8};
        private static readonly byte[] DATA_POSITIVE = { 0x1, 0x2, 0x3, 0x4, 0x5, 0x6, 0x7, 0x8};
        private static readonly byte[] STRING_DATA = CreateNullPaddedString("Test", 32);

        private static byte[] CreateNullPaddedString(string s, int length)
        {
             var data = new byte[length];
             Array.Copy(Encoding.ASCII.GetBytes(s), data, s.Length);
             return data;
        }

        [Theory]
        [InlineData(2, EnumKeyDataType.Integer, -3343)]
        [InlineData(4, EnumKeyDataType.Integer, -185339151)]
        [InlineData(6, EnumKeyDataType.Integer, -9938739662095)]
        [InlineData(8, EnumKeyDataType.Integer, -506664896818842895)]
        [InlineData(2, EnumKeyDataType.AutoInc, -3343)]
        [InlineData(4, EnumKeyDataType.AutoInc, -185339151)]
        [InlineData(6, EnumKeyDataType.AutoInc, -9938739662095)]
        [InlineData(8, EnumKeyDataType.AutoInc, -506664896818842895)]
        [InlineData(2, EnumKeyDataType.Unsigned, 0xF2F1)]
        [InlineData(4, EnumKeyDataType.Unsigned, 0xF4F3F2F1)]
        [InlineData(6, EnumKeyDataType.Unsigned, 0xF6F5F4F3F2F1)]
        [InlineData(8, EnumKeyDataType.Unsigned, 0xF8F7F6F5F4F3F2F1)]
        [InlineData(2, EnumKeyDataType.UnsignedBinary, 0xF2F1)]
        [InlineData(4, EnumKeyDataType.UnsignedBinary, 0xF4F3F2F1)]
        [InlineData(6, EnumKeyDataType.UnsignedBinary, 0xF6F5F4F3F2F1)]
        [InlineData(8, EnumKeyDataType.UnsignedBinary, 0xF8F7F6F5F4F3F2F1)]
        public void NegativeIntegerTypeConversion(ushort length, EnumKeyDataType type, object expected)
        {
            var key = new BtrieveKey() {
              Segments = new List<BtrieveKeyDefinition>() {
                  new BtrieveKeyDefinition() {
                      Number = 0,
                      Offset = 0,
                      Length = length,
                      DataType = type,
                      Attributes = EnumKeyAttributeMask.UseExtendedDataType,
                      Segment = false,
                      SegmentIndex = 0,
                      NullValue = 0,
                  }
              },
            };

            key.KeyDataToSqliteObject(DATA_NEGATIVE.AsSpan().Slice(0, length)).Should().Be(expected);
        }

        [Theory]
        [InlineData(2, EnumKeyDataType.Integer, 0x201)]
        [InlineData(4, EnumKeyDataType.Integer, 0x4030201)]
        [InlineData(6, EnumKeyDataType.Integer, 0x60504030201)]
        [InlineData(8, EnumKeyDataType.Integer, 0x807060504030201)]
        [InlineData(2, EnumKeyDataType.AutoInc, 0x201)]
        [InlineData(4, EnumKeyDataType.AutoInc, 0x4030201)]
        [InlineData(6, EnumKeyDataType.AutoInc, 0x60504030201)]
        [InlineData(8, EnumKeyDataType.AutoInc, 0x807060504030201)]
        [InlineData(2, EnumKeyDataType.UnsignedBinary, 0x201)]
        [InlineData(4, EnumKeyDataType.UnsignedBinary, 0x4030201)]
        [InlineData(6, EnumKeyDataType.UnsignedBinary, 0x60504030201)]
        [InlineData(8, EnumKeyDataType.UnsignedBinary, 0x807060504030201)]
        [InlineData(2, EnumKeyDataType.Unsigned, 0x201)]
        [InlineData(4, EnumKeyDataType.Unsigned, 0x4030201)]
        [InlineData(6, EnumKeyDataType.Unsigned, 0x60504030201)]
        [InlineData(8, EnumKeyDataType.Unsigned, 0x807060504030201)]
        public void PositiveIntegerTypeConversion(ushort length, EnumKeyDataType type, object expected)
        {
            var key = new BtrieveKey() {
              Segments = new List<BtrieveKeyDefinition>() {
                  new BtrieveKeyDefinition() {
                      Number = 0,
                      Offset = 0,
                      Length = length,
                      DataType = type,
                      Attributes = EnumKeyAttributeMask.UseExtendedDataType,
                      Segment = false,
                      SegmentIndex = 0,
                      NullValue = 0,
                  }
              },
            };

            key.KeyDataToSqliteObject(DATA_POSITIVE.AsSpan().Slice(0, length)).Should().Be(expected);
        }

        [Theory]
        [InlineData(32, EnumKeyDataType.String, "Test")]
        [InlineData(5, EnumKeyDataType.String, "Test")]
        [InlineData(4, EnumKeyDataType.String, "Test")]
        [InlineData(3, EnumKeyDataType.String, "Tes")]
        [InlineData(2, EnumKeyDataType.String, "Te")]
        [InlineData(1, EnumKeyDataType.String, "T")]
        [InlineData(32, EnumKeyDataType.Lstring, "Test")]
        [InlineData(5, EnumKeyDataType.Lstring, "Test")]
        [InlineData(4, EnumKeyDataType.Lstring, "Test")]
        [InlineData(3, EnumKeyDataType.Lstring, "Tes")]
        [InlineData(2, EnumKeyDataType.Lstring, "Te")]
        [InlineData(1, EnumKeyDataType.Lstring, "T")]
        [InlineData(32, EnumKeyDataType.Zstring, "Test")]
        [InlineData(5, EnumKeyDataType.Zstring, "Test")]
        [InlineData(4, EnumKeyDataType.Zstring, "Test")]
        [InlineData(3, EnumKeyDataType.Zstring, "Tes")]
        [InlineData(2, EnumKeyDataType.Zstring, "Te")]
        [InlineData(1, EnumKeyDataType.Zstring, "T")]
        [InlineData(32, EnumKeyDataType.OldAscii, "Test")]
        [InlineData(5, EnumKeyDataType.OldAscii, "Test")]
        [InlineData(4, EnumKeyDataType.OldAscii, "Test")]
        [InlineData(3, EnumKeyDataType.OldAscii, "Tes")]
        [InlineData(2, EnumKeyDataType.OldAscii, "Te")]
        [InlineData(1, EnumKeyDataType.OldAscii, "T")]
        public void StringTypeConversion(ushort length, EnumKeyDataType type, object expected)
        {
            var key = new BtrieveKey() {
              Segments = new List<BtrieveKeyDefinition>() {
                  new BtrieveKeyDefinition() {
                      Number = 0,
                      Offset = 0,
                      Length = length,
                      DataType = type,
                      Attributes = EnumKeyAttributeMask.UseExtendedDataType,
                      Segment = false,
                      SegmentIndex = 0,
                      NullValue = 0,
                  }
              },
            };

            key.KeyDataToSqliteObject(STRING_DATA.AsSpan().Slice(0, length)).Should().Be(expected);
        }

        [Fact]
        public void CompositeKeyConcatentation()
        {
            var key = new BtrieveKey() {
              Segments = new List<BtrieveKeyDefinition>() {
                  new BtrieveKeyDefinition() {
                      Number = 0,
                      Offset = 2,
                      Length = 8,
                      DataType = EnumKeyDataType.Integer,
                      Attributes = EnumKeyAttributeMask.UseExtendedDataType,
                      Segment = true,
                      SegmentIndex = 0,
                      NullValue = 0,
                  },
                  new BtrieveKeyDefinition() {
                      Number = 0,
                      Offset = 20,
                      Length = 4,
                      DataType = EnumKeyDataType.Zstring,
                      Attributes = EnumKeyAttributeMask.UseExtendedDataType,
                      Segment = false,
                      SegmentIndex = 1,
                      NullValue = 0,
                  }}
            };

            var record = new byte[128];
            Array.Fill(record, (byte)0xFF, 0, record.Length);
            // first segment is all 0x5
            Array.Fill(record, (byte)0x5, 2, 8);
            // second segment is just a letter
            Array.Fill(record, (byte)'T', 20, 4);

            var sqlLiteObject = key.ExtractKeyInRecordToSqliteObject(record);
            sqlLiteObject.Should().BeEquivalentTo(new byte[] { 0x5, 0x5, 0x5, 0x5, 0x5, 0x5, 0x5, 0x5, (byte)'T', (byte)'T', (byte)'T', (byte)'T'});
        }

        [Theory]
        [InlineData(EnumKeyDataType.String)]
        [InlineData(EnumKeyDataType.Lstring)]
        [InlineData(EnumKeyDataType.Zstring)]
        [InlineData(EnumKeyDataType.OldAscii)]
        [InlineData(EnumKeyDataType.Integer)]
        [InlineData(EnumKeyDataType.Unsigned)]
        [InlineData(EnumKeyDataType.UnsignedBinary)]
        [InlineData(EnumKeyDataType.OldBinary)]
        public void NullValueString(EnumKeyDataType dataType)
        {
            var key = new BtrieveKey() {
              Segments = new List<BtrieveKeyDefinition>() {
                  new BtrieveKeyDefinition() {
                      Number = 0,
                      Offset = 2,
                      Length = 8,
                      DataType = dataType,
                      Attributes = EnumKeyAttributeMask.UseExtendedDataType | EnumKeyAttributeMask.NullAllSegments,
                      Segment = true,
                      SegmentIndex = 0,
                      NullValue = (byte)' ',
                  }}
            };

            var record = new byte[128];
            Array.Fill(record, (byte)0xFF, 0, record.Length);
            // first segment is all spaces i.e. null
            Array.Fill(record, (byte)' ', 2, 8);

            var sqlLiteObject = key.ExtractKeyInRecordToSqliteObject(record);
            sqlLiteObject.Should().Be(DBNull.Value);
        }
    }
}
