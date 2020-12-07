using FluentAssertions;
using MBBSEmu.Btrieve;
using MBBSEmu.Btrieve.Enums;
using MBBSEmu.DependencyInjection;
using MBBSEmu.IO;
using MBBSEmu.Resources;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Xunit;

namespace MBBSEmu.Tests.Btrieve
{
    public class BtrieveFileProcessor_Tests : TestBase, IDisposable
    {
        const int RECORD_LENGTH = 74;

        private const string EXPECTED_METADATA_T_SQL = "CREATE TABLE metadata_t(record_length INTEGER NOT NULL, physical_record_length INTEGER NOT NULL, page_length INTEGER NOT NULL, variable_length_records INTEGER NOT NULL, version INTEGER NOT NULL, acs_name STRING, acs BLOB)";
        private const string EXPECTED_KEYS_T_SQL = "CREATE TABLE keys_t(id INTEGER PRIMARY KEY, number INTEGER NOT NULL, segment INTEGER NOT NULL, attributes INTEGER NOT NULL, data_type INTEGER NOT NULL, offset INTEGER NOT NULL, length INTEGER NOT NULL, null_value INTEGER NOT NULL, UNIQUE(number, segment))";
        private const string EXPECTED_DATA_T_SQL = "CREATE TABLE data_t(id INTEGER PRIMARY KEY, data BLOB NOT NULL, key_0 TEXT, key_1 INTEGER NOT NULL UNIQUE, key_2 TEXT, key_3 INTEGER NOT NULL UNIQUE)";

        private static readonly Random RANDOM = new Random(Guid.NewGuid().GetHashCode());

        protected readonly string _modulePath = Path.Join(Path.GetTempPath(), $"mbbsemu{RANDOM.Next()}");

        private class MBBSEmuRecord
        {
            public byte[] Data { get; }

            // offset 2, length 32
            public string Key0
            {
                get => Encoding.ASCII.GetString(Data.AsSpan().Slice(2, 32)).TrimEnd((char)0);
                set => Array.Copy(Encoding.ASCII.GetBytes(value), 0, Data, 2, value.Length);
            }

            // offset 34, length 4
            public int Key1
            {
                get => BitConverter.ToInt32(Data, 34);
                set => Array.Copy(BitConverter.GetBytes(value), 0, Data, 34, 4);
            }

            // offset 38, length 32
            public string Key2
            {
                get => Encoding.ASCII.GetString(Data.AsSpan().Slice(38, 32)).TrimEnd((char)0);
                set => Array.Copy(Encoding.ASCII.GetBytes(value), 0, Data, 38, value.Length);
            }

            // offset 70, length 4
            public int Key3
            {
                get => BitConverter.ToInt32(Data, 70);
                set => Array.Copy(BitConverter.GetBytes(value), 0, Data, 70, 4);
            }

            public MBBSEmuRecord() : this(new byte[RECORD_LENGTH]) { }

            public MBBSEmuRecord(byte[] data)
            {
                data.Should().NotBeNull();
                data.Length.Should().Be(RECORD_LENGTH);

                Data = data;
            }
        };

        public BtrieveFileProcessor_Tests()
        {
            Directory.CreateDirectory(_modulePath);
        }

        public void Dispose()
        {
            Directory.Delete(_modulePath, recursive: true);
        }

        private void CopyFilesToTempPath(params string[] files)
        {
            var resourceManager = ResourceManager.GetTestResourceManager();

            foreach (var file in files)
            {
                File.WriteAllBytes(Path.Combine(_modulePath, file), resourceManager.GetResource($"MBBSEmu.Tests.Assets.{file}").ToArray());
            }
        }

        [Fact]
        public void LoadsFileAndConvertsProperly()
        {
            CopyFilesToTempPath("MBBSEMU.DAT");

            var serviceResolver = new ServiceResolver();

            var btrieve = new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT");

            btrieve.Keys.Count.Should().Be(4);
            btrieve.RecordLength.Should().Be(RECORD_LENGTH);
            btrieve.PageLength.Should().Be(512);
            btrieve.VariableLengthRecords.Should().BeFalse();

            btrieve.Keys[0].PrimarySegment.Should().BeEquivalentTo(
                new BtrieveKeyDefinition()
                {
                    Number = 0,
                    Attributes = EnumKeyAttributeMask.Duplicates | EnumKeyAttributeMask.UseExtendedDataType,
                    DataType = EnumKeyDataType.Zstring,
                    Offset = 2,
                    Length = 32,
                    Segment = false,
                });
            btrieve.Keys[1].PrimarySegment.Should().BeEquivalentTo(
                new BtrieveKeyDefinition()
                {
                    Number = 1,
                    Attributes = EnumKeyAttributeMask.Modifiable | EnumKeyAttributeMask.UseExtendedDataType,
                    DataType = EnumKeyDataType.Integer,
                    Offset = 34,
                    Length = 4,
                    Segment = false,
                });
            btrieve.Keys[2].PrimarySegment.Should().BeEquivalentTo(
                new BtrieveKeyDefinition()
                {
                    Number = 2,
                    Attributes = EnumKeyAttributeMask.Duplicates | EnumKeyAttributeMask.Modifiable | EnumKeyAttributeMask.UseExtendedDataType,
                    DataType = EnumKeyDataType.Zstring,
                    Offset = 38,
                    Length = 32,
                    Segment = false,
                });
            btrieve.Keys[3].PrimarySegment.Should().BeEquivalentTo(
                new BtrieveKeyDefinition()
                {
                    Number = 3,
                    Attributes = EnumKeyAttributeMask.UseExtendedDataType,
                    DataType = EnumKeyDataType.AutoInc,
                    Offset = 70,
                    Length = 4,
                    Segment = false,
                });

            btrieve.Dispose();

            AssertSqlStructure(Path.Combine(_modulePath, "MBBSEMU.DB"));
        }

        private void AssertSqlStructure(string fullPath)
        {
            var connectionString = new SqliteConnectionStringBuilder()
            {
                Mode = SqliteOpenMode.ReadWriteCreate,
                DataSource = fullPath,
            }.ToString();

            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            using (var stmt = new SqliteCommand("SELECT sql FROM sqlite_master WHERE name = 'metadata_t';", connection))
                ((string)stmt.ExecuteScalar()).Should().Be(EXPECTED_METADATA_T_SQL);

            using (var stmt = new SqliteCommand("SELECT sql FROM sqlite_master WHERE name = 'keys_t';", connection))
                ((string)stmt.ExecuteScalar()).Should().Be(EXPECTED_KEYS_T_SQL);

            using (var stmt = new SqliteCommand("SELECT sql FROM sqlite_master WHERE name = 'data_t';", connection))
                ((string)stmt.ExecuteScalar()).Should().Be(EXPECTED_DATA_T_SQL);
        }

        [Fact]
        public void LoadsFileProperly()
        {
            CopyFilesToTempPath("MBBSEMU.DB");

            var serviceResolver = new ServiceResolver();

            using var btrieve = new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT");

            btrieve.Keys.Count.Should().Be(4);
            btrieve.RecordLength.Should().Be(RECORD_LENGTH);
            btrieve.PageLength.Should().Be(512);
            btrieve.VariableLengthRecords.Should().BeFalse();

            btrieve.Keys[0].PrimarySegment.Should().BeEquivalentTo(
                new BtrieveKeyDefinition()
                {
                    Number = 0,
                    Attributes = EnumKeyAttributeMask.Duplicates | EnumKeyAttributeMask.UseExtendedDataType,
                    DataType = EnumKeyDataType.Zstring,
                    Offset = 2,
                    Length = 32,
                    Segment = false,
                });
            btrieve.Keys[1].PrimarySegment.Should().BeEquivalentTo(
                new BtrieveKeyDefinition()
                {
                    Number = 1,
                    Attributes = EnumKeyAttributeMask.Modifiable | EnumKeyAttributeMask.UseExtendedDataType,
                    DataType = EnumKeyDataType.Integer,
                    Offset = 34,
                    Length = 4,
                    Segment = false,
                });
            btrieve.Keys[2].PrimarySegment.Should().BeEquivalentTo(
                new BtrieveKeyDefinition()
                {
                    Number = 2,
                    Attributes = EnumKeyAttributeMask.Duplicates | EnumKeyAttributeMask.Modifiable | EnumKeyAttributeMask.UseExtendedDataType,
                    DataType = EnumKeyDataType.Zstring,
                    Offset = 38,
                    Length = 32,
                    Segment = false,
                });
            btrieve.Keys[3].PrimarySegment.Should().BeEquivalentTo(
                new BtrieveKeyDefinition()
                {
                    Number = 3,
                    Attributes = EnumKeyAttributeMask.UseExtendedDataType,
                    DataType = EnumKeyDataType.AutoInc,
                    Offset = 70,
                    Length = 4,
                    Segment = false,
                });
        }

        [Fact]
        public void EnumeratesInitialEntriesForward()
        {
            CopyFilesToTempPath("MBBSEMU.DB");

            var serviceResolver = new ServiceResolver();
            using var btrieve = new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT");

            btrieve.PerformOperation(-1, ReadOnlySpan<byte>.Empty, EnumBtrieveOperationCodes.StepFirst).Should().BeTrue();
            btrieve.Position.Should().Be(1);
            new MBBSEmuRecord(btrieve.GetRecord()).Key1.Should().Be(3444);

            btrieve.PerformOperation(-1, ReadOnlySpan<byte>.Empty, EnumBtrieveOperationCodes.StepNext).Should().BeTrue();
            btrieve.Position.Should().Be(2);
            new MBBSEmuRecord(btrieve.GetRecord()).Key1.Should().Be(7776);

            btrieve.PerformOperation(-1, ReadOnlySpan<byte>.Empty, EnumBtrieveOperationCodes.StepNext).Should().BeTrue();
            btrieve.Position.Should().Be(3);
            new MBBSEmuRecord(btrieve.GetRecord()).Key1.Should().Be(1052234073);

            btrieve.PerformOperation(-1, ReadOnlySpan<byte>.Empty, EnumBtrieveOperationCodes.StepNext).Should().BeTrue();
            btrieve.Position.Should().Be(4);
            new MBBSEmuRecord(btrieve.GetRecord()).Key1.Should().Be(-615634567);

            btrieve.PerformOperation(-1, ReadOnlySpan<byte>.Empty, EnumBtrieveOperationCodes.StepNext).Should().BeFalse();
            btrieve.Position.Should().Be(4);
        }

        [Fact]
        public void EnumeratesInitialEntriesReverse()
        {
            CopyFilesToTempPath("MBBSEMU.DB");

            var serviceResolver = new ServiceResolver();
            using var btrieve = new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT");

            btrieve.PerformOperation(-1, ReadOnlySpan<byte>.Empty, EnumBtrieveOperationCodes.StepLast).Should().BeTrue();
            btrieve.Position.Should().Be(4);
            new MBBSEmuRecord(btrieve.GetRecord()).Key1.Should().Be(-615634567);

            btrieve.PerformOperation(-1, ReadOnlySpan<byte>.Empty, EnumBtrieveOperationCodes.StepPrevious).Should().BeTrue();
            btrieve.Position.Should().Be(3);
            new MBBSEmuRecord(btrieve.GetRecord()).Key1.Should().Be(1052234073);

            btrieve.PerformOperation(-1, ReadOnlySpan<byte>.Empty, EnumBtrieveOperationCodes.StepPrevious).Should().BeTrue();
            btrieve.Position.Should().Be(2);
            new MBBSEmuRecord(btrieve.GetRecord()).Key1.Should().Be(7776);

            btrieve.PerformOperation(-1, ReadOnlySpan<byte>.Empty, EnumBtrieveOperationCodes.StepPrevious).Should().BeTrue();
            btrieve.Position.Should().Be(1);
            new MBBSEmuRecord(btrieve.GetRecord()).Key1.Should().Be(3444);

            btrieve.PerformOperation(-1, ReadOnlySpan<byte>.Empty, EnumBtrieveOperationCodes.StepPrevious).Should().BeFalse();
            btrieve.Position.Should().Be(1);
        }

        [Fact]
        public void RandomAccess()
        {
            CopyFilesToTempPath("MBBSEMU.DB");

            var serviceResolver = new ServiceResolver();
            using var btrieve = new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT");

            var record = new MBBSEmuRecord(btrieve.GetRecord(4)?.Data);
            record.Key0.Should().Be("Sysop");
            record.Key1.Should().Be(-615634567);
            record.Key2.Should().Be("stringValue");
            record.Key3.Should().Be(4);

            new MBBSEmuRecord(btrieve.GetRecord(3)?.Data).Key1.Should().Be(1052234073);
            new MBBSEmuRecord(btrieve.GetRecord(2)?.Data).Key1.Should().Be(7776);
            new MBBSEmuRecord(btrieve.GetRecord(1)?.Data).Key1.Should().Be(3444);

            new MBBSEmuRecord(btrieve.GetRecord(2)?.Data).Key1.Should().Be(7776);
        }

        [Fact]
        public void RandomInvalidAccess()
        {
            CopyFilesToTempPath("MBBSEMU.DB");

            var serviceResolver = new ServiceResolver();
            using var btrieve = new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT");

            btrieve.GetRecord(5).Should().BeNull();
            btrieve.GetRecord(0).Should().BeNull();

            btrieve.Position = 5;
            btrieve.GetRecord().Should().BeNull();

            btrieve.Position = 0;
            btrieve.GetRecord().Should().BeNull();
        }

        [Fact]
        public void RecordCount()
        {
            CopyFilesToTempPath("MBBSEMU.DB");

            var serviceResolver = new ServiceResolver();
            using var btrieve = new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT");

            btrieve.GetRecordCount().Should().Be(4);
        }

        [Fact]
        public void RecordDeleteAll()
        {
            CopyFilesToTempPath("MBBSEMU.DB");

            var serviceResolver = new ServiceResolver();
            using var btrieve =
                new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT")
                {
                    Position = 3
                };

            btrieve.DeleteAll().Should().BeTrue();

            btrieve.Position.Should().Be(0);
            btrieve.GetRecordCount().Should().Be(0);

            btrieve.DeleteAll().Should().BeFalse();
        }

        [Fact]
        public void RecordDeleteOne()
        {
            CopyFilesToTempPath("MBBSEMU.DB");

            var serviceResolver = new ServiceResolver();
            using var btrieve =
                new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT")
                {
                    Position = 2
                };

            btrieve.Delete().Should().BeTrue();
            btrieve.Delete().Should().BeFalse();

            btrieve.Position.Should().Be(2);
            btrieve.GetRecordCount().Should().Be(3);
            btrieve.GetRecord().Should().BeNull();
        }

        [Fact]
        public void RecordDeleteOneIteration()
        {
            CopyFilesToTempPath("MBBSEMU.DB");

            var serviceResolver = new ServiceResolver();
            using var btrieve =
                new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT")
                {
                    Position = 2
                };

            btrieve.Delete().Should().BeTrue();

            btrieve.PerformOperation(-1, ReadOnlySpan<byte>.Empty, EnumBtrieveOperationCodes.StepFirst).Should().BeTrue();
            btrieve.Position.Should().Be(1);
            new MBBSEmuRecord(btrieve.GetRecord()).Key1.Should().Be(3444);

            btrieve.PerformOperation(-1, ReadOnlySpan<byte>.Empty, EnumBtrieveOperationCodes.StepNext).Should().BeTrue();
            btrieve.Position.Should().Be(3);
            new MBBSEmuRecord(btrieve.GetRecord()).Key1.Should().Be(1052234073);

            btrieve.PerformOperation(-1, ReadOnlySpan<byte>.Empty, EnumBtrieveOperationCodes.StepNext).Should().BeTrue();
            btrieve.Position.Should().Be(4);
            new MBBSEmuRecord(btrieve.GetRecord()).Key1.Should().Be(-615634567);

            btrieve.PerformOperation(-1, ReadOnlySpan<byte>.Empty, EnumBtrieveOperationCodes.StepNext).Should().BeFalse();
        }

        [Fact]
        public void InsertionTest()
        {
            CopyFilesToTempPath("MBBSEMU.DB");

            var serviceResolver = new ServiceResolver();
            using var btrieve = new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT");

            var record = new MBBSEmuRecord { Key0 = "Paladine", Key1 = 31337, Key2 = "In orbe terrarum, optimus sum" };

            var insertedId = btrieve.Insert(record.Data);
            insertedId.Should().Be(5);

            record = new MBBSEmuRecord(btrieve.GetRecord(insertedId)?.Data);
            record.Key0.Should().Be("Paladine");
            record.Key1.Should().Be(31337);
            record.Key2.Should().Be("In orbe terrarum, optimus sum");
            record.Key3.Should().Be(5);

            btrieve.GetRecordCount().Should().Be(5);
        }

        [Fact]
        public void InsertionTestManualAutoincrementedValue()
        {
            CopyFilesToTempPath("MBBSEMU.DB");

            var serviceResolver = new ServiceResolver();
            using var btrieve = new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT");

            var record = new MBBSEmuRecord
            {
                Key0 = "Paladine",
                Key1 = 31337,
                Key2 = "In orbe terrarum, optimus sum",
                Key3 = 4444
            };

            var insertedId = btrieve.Insert(record.Data);
            insertedId.Should().Be(5);

            record = new MBBSEmuRecord(btrieve.GetRecord(insertedId)?.Data);
            record.Key0.Should().Be("Paladine");
            record.Key1.Should().Be(31337);
            record.Key2.Should().Be("In orbe terrarum, optimus sum");
            record.Key3.Should().Be(4444);

            btrieve.GetRecordCount().Should().Be(5);
        }

        [Fact]
        public void InsertionTestSubSize()
        {
            CopyFilesToTempPath("MBBSEMU.DB");

            var serviceResolver = new ServiceResolver();
            using var btrieve = new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT");

            var record = new MBBSEmuRecord { Key0 = "Paladine", Key1 = 31337, Key2 = "In orbe terrarum, optimus sum" };

            var insertedId = btrieve.Insert(MakeSmaller(record.Data, 14));
            insertedId.Should().Be(5);

            record = new MBBSEmuRecord(btrieve.GetRecord(insertedId)?.Data);
            record.Key0.Should().Be("Paladine");
            record.Key1.Should().Be(31337);
            record.Key2.Should().Be("In orbe terrarum, opti"); // cut off

            btrieve.GetRecordCount().Should().Be(5);
        }

        [Fact]
        public void InsertionConstraintFailure()
        {
            CopyFilesToTempPath("MBBSEMU.DB");

            var serviceResolver = new ServiceResolver();
            using var btrieve = new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT");

            var record = new MBBSEmuRecord
            {
                Key0 = "Paladine",
                Key1 = 3444, // constraint failure here
                Key2 = "In orbe terrarum, optimus sum",
                Key3 = 4444
            };


            var insertedId = btrieve.Insert(record.Data);
            insertedId.Should().Be(0);
        }

        [Fact]
        public void UpdateTest()
        {
            CopyFilesToTempPath("MBBSEMU.DB");

            var serviceResolver = new ServiceResolver();
            using var btrieve = new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT");
            var record = new MBBSEmuRecord { Key0 = "Sysop", Key1 = 31337, Key2 = "In orbe terrarum, optimus sum", Key3 = 1 };

            btrieve.Update(1, record.Data).Should().BeTrue();

            record = new MBBSEmuRecord(btrieve.GetRecord(1)?.Data);
            record.Key0.Should().Be("Sysop");
            record.Key1.Should().Be(31337);
            record.Key2.Should().Be("In orbe terrarum, optimus sum");
            record.Key3.Should().Be(1);

            btrieve.GetRecordCount().Should().Be(4);
        }

        [Fact]
        public void UpdateTestSubSize()
        {
            CopyFilesToTempPath("MBBSEMU.DB");

            var serviceResolver = new ServiceResolver();
            using var btrieve = new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT");

            var record = new MBBSEmuRecord
            {
                Key0 = "Sysop",
                Key1 = 31337,
                Key2 = "In orbe terrarum, optimus sum",
                Key3 = 2
            };

            // we shorten the data by 3 bytes, meaning Key3 data is still valid, and is a single byte of 2.
            // The code will upsize to the full 74 bytes, filling in 0 for the rest of Key3 data,
            // so Key3 starts as 0x02 but grows to 0x02000000 (little endian == 2)
            // We have to keep Key3 as 2 since this key is marked non-modifiable
            btrieve.Update(2, MakeSmaller(record.Data, 3)).Should().BeTrue();

            record = new MBBSEmuRecord(btrieve.GetRecord(2)?.Data);
            record.Key0.Should().Be("Sysop");
            record.Key1.Should().Be(31337);
            record.Key2.Should().Be("In orbe terrarum, optimus sum");
            record.Key3.Should().Be(2);

            btrieve.GetRecordCount().Should().Be(4);
        }

        [Fact]
        public void UpdateTestConstraintFailed()
        {
            CopyFilesToTempPath("MBBSEMU.DB");

            var serviceResolver = new ServiceResolver();
            using var btrieve = new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT");

            var record = new MBBSEmuRecord { Key1 = 7776, Key2 = "In orbe terrarum, optimus sum" };
            // constraint failure here

            btrieve.Update(1, record.Data).Should().BeFalse();

            // assert update didn't occur
            record = new MBBSEmuRecord(btrieve.GetRecord(1)?.Data);
            record.Key0.Should().Be("Sysop");
            record.Key1.Should().Be(3444);
            record.Key2.Should().Be("3444");
            record.Key3.Should().Be(1);
        }

        [Fact]
        public void UpdateInvalidIndex()
        {
            CopyFilesToTempPath("MBBSEMU.DB");

            var serviceResolver = new ServiceResolver();
            using var btrieve = new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT");

            var record = new MBBSEmuRecord { Key0 = "Paladine", Key1 = 31337, Key2 = "In orbe terrarum, optimus sum" };

            btrieve.Update(5, record.Data).Should().BeFalse();
        }

        [Fact]
        public void SeekByKeyStringDuplicates()
        {
            CopyFilesToTempPath("MBBSEMU.DB");

            var serviceResolver = new ServiceResolver();
            using var btrieve = new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT");

            btrieve.PerformOperation(0, Encoding.ASCII.GetBytes("Sysop"), EnumBtrieveOperationCodes.QueryEqual).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(1);

            btrieve.PerformOperation(0, Encoding.ASCII.GetBytes("Sysop"), EnumBtrieveOperationCodes.QueryNext).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(2);

            btrieve.PerformOperation(0, Encoding.ASCII.GetBytes("Sysop"), EnumBtrieveOperationCodes.QueryNext).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(3);

            btrieve.PerformOperation(0, Encoding.ASCII.GetBytes("Sysop"), EnumBtrieveOperationCodes.QueryNext).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(4);

            btrieve.PerformOperation(0, Encoding.ASCII.GetBytes("Sysop"), EnumBtrieveOperationCodes.QueryNext).Should().BeFalse();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(4);
        }

        [Fact]
        public void SeekByKeyStringDuplicatesUpAndDown()
        {
            CopyFilesToTempPath("MBBSEMU.DB");

            var serviceResolver = new ServiceResolver();
            using var btrieve = new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT");
            var key = Encoding.ASCII.GetBytes("Sysop");

            btrieve.PerformOperation(0, key, EnumBtrieveOperationCodes.QueryEqual).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(1);

            btrieve.PerformOperation(0, key, EnumBtrieveOperationCodes.QueryNext).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(2);

            btrieve.PerformOperation(0, key, EnumBtrieveOperationCodes.QueryNext).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(3);

            btrieve.PerformOperation(0, key, EnumBtrieveOperationCodes.QueryNext).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(4);

            btrieve.PerformOperation(0, key, EnumBtrieveOperationCodes.QueryNext).Should().BeFalse();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(4);
            // let's go backwards now
            btrieve.PerformOperation(0, key, EnumBtrieveOperationCodes.QueryPrevious).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(3);

            btrieve.PerformOperation(0, key, EnumBtrieveOperationCodes.QueryPrevious).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(2);

            btrieve.PerformOperation(0, key, EnumBtrieveOperationCodes.QueryPrevious).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(1);

            btrieve.PerformOperation(0, key, EnumBtrieveOperationCodes.QueryPrevious).Should().BeFalse();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(1);
            // forward for one last test
            btrieve.PerformOperation(0, key, EnumBtrieveOperationCodes.QueryNext).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(2);
            // back one last time to test in-middle previous
            btrieve.PerformOperation(0, key, EnumBtrieveOperationCodes.QueryPrevious).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(1);
        }

        [Fact]
        public void SeekByKeyString()
        {
            CopyFilesToTempPath("MBBSEMU.DB");

            var serviceResolver = new ServiceResolver();
            using var btrieve = new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT");
            var key = Encoding.ASCII.GetBytes("StringValue");

            btrieve.PerformOperation(2, key, EnumBtrieveOperationCodes.QueryEqual).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(3);

            btrieve.PerformOperation(2, key, EnumBtrieveOperationCodes.QueryNext).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(4);
            new MBBSEmuRecord(btrieve.GetRecord(btrieve.Position)?.Data).Key2.Should().Be("stringValue");

            btrieve.PerformOperation(2, key, EnumBtrieveOperationCodes.QueryNext).Should().BeFalse();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(4);

            btrieve.PerformOperation(0, key, EnumBtrieveOperationCodes.QueryPrevious).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(3);
            btrieve.PerformOperation(0, key, EnumBtrieveOperationCodes.QueryPrevious).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(2);
            btrieve.PerformOperation(0, key, EnumBtrieveOperationCodes.QueryPrevious).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(1);
            btrieve.PerformOperation(0, key, EnumBtrieveOperationCodes.QueryPrevious).Should().BeFalse();
        }

        [Fact]
        public void SeekByKeyInteger()
        {
            CopyFilesToTempPath("MBBSEMU.DB");

            var serviceResolver = new ServiceResolver();
            using var btrieve = new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT");
            var key = BitConverter.GetBytes(1052234073);

            btrieve.PerformOperation(1, key, EnumBtrieveOperationCodes.QueryEqual).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(3);
            new MBBSEmuRecord(btrieve.GetRecord(btrieve.Position)?.Data).Key1.Should().Be(1052234073);

            btrieve.PerformOperation(1, key, EnumBtrieveOperationCodes.QueryNext).Should().BeFalse();

            btrieve.PerformOperation(1, key, EnumBtrieveOperationCodes.QueryPrevious).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(2);
            btrieve.PerformOperation(1, key, EnumBtrieveOperationCodes.QueryPrevious).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(1);
            btrieve.PerformOperation(1, key, EnumBtrieveOperationCodes.QueryPrevious).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(4);
            btrieve.PerformOperation(1, key, EnumBtrieveOperationCodes.QueryPrevious).Should().BeFalse();
        }

        [Fact]
        public void SeekByKeyNotFound()
        {
            CopyFilesToTempPath("MBBSEMU.DB");

            var serviceResolver = new ServiceResolver();
            using var btrieve = new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT");
            var key = Encoding.ASCII.GetBytes("Sysop2");

            btrieve.PerformOperation(0, key, EnumBtrieveOperationCodes.QueryEqual).Should().BeFalse();

            btrieve.PerformOperation(0, key, EnumBtrieveOperationCodes.QueryNext).Should().BeFalse();
            btrieve.PerformOperation(0, key, EnumBtrieveOperationCodes.QueryPrevious).Should().BeFalse();
        }

        [Fact]
        public void SeekByKeyFirstString()
        {
            CopyFilesToTempPath("MBBSEMU.DB");

            var serviceResolver = new ServiceResolver();
            using var btrieve = new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT");

            btrieve.PerformOperation(2, null, EnumBtrieveOperationCodes.QueryFirst).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(1);
            new MBBSEmuRecord(btrieve.GetRecord(btrieve.Position)?.Data).Key2.Should().Be("3444");

            btrieve.PerformOperation(2, null, EnumBtrieveOperationCodes.QueryPrevious).Should().BeFalse();

            btrieve.PerformOperation(2, null, EnumBtrieveOperationCodes.QueryNext).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(2);
            new MBBSEmuRecord(btrieve.GetRecord(btrieve.Position)?.Data).Key2.Should().Be("7776");

            btrieve.PerformOperation(2, null, EnumBtrieveOperationCodes.QueryNext).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(3);
            new MBBSEmuRecord(btrieve.GetRecord(btrieve.Position)?.Data).Key2.Should().Be("StringValue");

            btrieve.PerformOperation(2, null, EnumBtrieveOperationCodes.QueryNext).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(4);
            new MBBSEmuRecord(btrieve.GetRecord(btrieve.Position)?.Data).Key2.Should().Be("stringValue");

            btrieve.PerformOperation(2, null, EnumBtrieveOperationCodes.QueryNext).Should().BeFalse();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(4);
        }

        [Fact]
        public void SeekByKeyFirstInteger()
        {
            CopyFilesToTempPath("MBBSEMU.DB");

            var serviceResolver = new ServiceResolver();
            using var btrieve = new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT");

            btrieve.PerformOperation(1, null, EnumBtrieveOperationCodes.QueryFirst).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(4);
            new MBBSEmuRecord(btrieve.GetRecord(btrieve.Position)?.Data).Key1.Should().Be(-615634567);

            btrieve.PerformOperation(1, null, EnumBtrieveOperationCodes.QueryPrevious).Should().BeFalse();

            btrieve.PerformOperation(1, null, EnumBtrieveOperationCodes.QueryNext).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(1);
            new MBBSEmuRecord(btrieve.GetRecord(btrieve.Position)?.Data).Key1.Should().Be(3444);

            btrieve.PerformOperation(1, null, EnumBtrieveOperationCodes.QueryNext).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(2);
            new MBBSEmuRecord(btrieve.GetRecord(btrieve.Position)?.Data).Key1.Should().Be(7776);

            btrieve.PerformOperation(1, null, EnumBtrieveOperationCodes.QueryNext).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(3);
            new MBBSEmuRecord(btrieve.GetRecord(btrieve.Position)?.Data).Key1.Should().Be(1052234073);

            btrieve.PerformOperation(1, null, EnumBtrieveOperationCodes.QueryNext).Should().BeFalse();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(3);
        }

        [Fact]
        public void SeekByKeyFirstNotFound()
        {
            CopyFilesToTempPath("MBBSEMU.DB");

            var serviceResolver = new ServiceResolver();
            using var btrieve = new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT");

            btrieve.DeleteAll();

            btrieve.PerformOperation(0, null, EnumBtrieveOperationCodes.QueryFirst).Should().BeFalse();
        }

        [Fact]
        public void SeekByKeyLastString()
        {
            CopyFilesToTempPath("MBBSEMU.DB");

            var serviceResolver = new ServiceResolver();
            using var btrieve = new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT");

            btrieve.PerformOperation(2, null, EnumBtrieveOperationCodes.QueryLast).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(4);
            new MBBSEmuRecord(btrieve.GetRecord(btrieve.Position)?.Data).Key2.Should().Be("stringValue");

            btrieve.PerformOperation(2, null, EnumBtrieveOperationCodes.QueryNext).Should().BeFalse();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(4);

            btrieve.PerformOperation(2, null, EnumBtrieveOperationCodes.QueryPrevious).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(3);
        }

        [Fact]
        public void SeekByKeyLastInteger()
        {
            CopyFilesToTempPath("MBBSEMU.DB");

            var serviceResolver = new ServiceResolver();
            using var btrieve = new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT");

            btrieve.PerformOperation(1, null, EnumBtrieveOperationCodes.QueryLast).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(3);
            new MBBSEmuRecord(btrieve.GetRecord(btrieve.Position)?.Data).Key1.Should().Be(1052234073);

            btrieve.PerformOperation(1, null, EnumBtrieveOperationCodes.QueryNext).Should().BeFalse();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(3);

            btrieve.PerformOperation(1, null, EnumBtrieveOperationCodes.QueryPrevious).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(2);
        }

        [Fact]
        public void SeekByKeyLastNotFound()
        {
            CopyFilesToTempPath("MBBSEMU.DB");

            var serviceResolver = new ServiceResolver();
            using var btrieve = new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT");

            btrieve.DeleteAll();

            btrieve.PerformOperation(0, null, EnumBtrieveOperationCodes.QueryLast).Should().BeFalse();
        }

        [Fact]
        public void SeekByKeyGreaterString()
        {
            CopyFilesToTempPath("MBBSEMU.DB");

            var serviceResolver = new ServiceResolver();
            using var btrieve = new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT");

            btrieve.PerformOperation(2, Encoding.ASCII.GetBytes("7776"), EnumBtrieveOperationCodes.QueryGreater).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(3);
            new MBBSEmuRecord(btrieve.GetRecord(btrieve.Position)?.Data).Key2.Should().Be("StringValue");

            btrieve.PerformOperation(2, null, EnumBtrieveOperationCodes.QueryNext).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(4);
            new MBBSEmuRecord(btrieve.GetRecord(btrieve.Position)?.Data).Key2.Should().Be("stringValue");

            btrieve.PerformOperation(2, null, EnumBtrieveOperationCodes.QueryNext).Should().BeFalse();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(4);
        }

        [Fact]
        public void SeekByKeyGreaterInteger()
        {
            CopyFilesToTempPath("MBBSEMU.DB");

            var serviceResolver = new ServiceResolver();
            using var btrieve = new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT");

            btrieve.PerformOperation(1, BitConverter.GetBytes(3444), EnumBtrieveOperationCodes.QueryGreater).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(2);
            new MBBSEmuRecord(btrieve.GetRecord(btrieve.Position)?.Data).Key1.Should().Be(7776);

            btrieve.PerformOperation(1, null, EnumBtrieveOperationCodes.QueryNext).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(3);
            new MBBSEmuRecord(btrieve.GetRecord(btrieve.Position)?.Data).Key1.Should().Be(1052234073);

            btrieve.PerformOperation(1, null, EnumBtrieveOperationCodes.QueryNext).Should().BeFalse();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(3);
        }

        [Fact]
        public void SeekByKeyGreaterNotFound()
        {
            CopyFilesToTempPath("MBBSEMU.DB");

            var serviceResolver = new ServiceResolver();
            using var btrieve = new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT");

            btrieve.PerformOperation(1, BitConverter.GetBytes(2_000_000_000), EnumBtrieveOperationCodes.QueryGreater).Should().BeFalse();
        }

        [Fact]
        public void SeekByKeyGreaterOrEqualString()
        {
            CopyFilesToTempPath("MBBSEMU.DB");

            var serviceResolver = new ServiceResolver();
            using var btrieve = new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT");

            btrieve.PerformOperation(2, Encoding.ASCII.GetBytes("7776"), EnumBtrieveOperationCodes.QueryGreaterOrEqual).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(2);
            new MBBSEmuRecord(btrieve.GetRecord(btrieve.Position)?.Data).Key2.Should().Be("7776");

            btrieve.PerformOperation(2, null, EnumBtrieveOperationCodes.QueryNext).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(3);
            new MBBSEmuRecord(btrieve.GetRecord(btrieve.Position)?.Data).Key2.Should().Be("StringValue");

            btrieve.PerformOperation(2, null, EnumBtrieveOperationCodes.QueryNext).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(4);
            new MBBSEmuRecord(btrieve.GetRecord(btrieve.Position)?.Data).Key2.Should().Be("stringValue");

            btrieve.PerformOperation(2, null, EnumBtrieveOperationCodes.QueryNext).Should().BeFalse();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(4);
        }

        [Fact]
        public void SeekByKeyGreaterOrEqualInteger()
        {
            CopyFilesToTempPath("MBBSEMU.DB");

            var serviceResolver = new ServiceResolver();
            using var btrieve = new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT");

            btrieve.PerformOperation(1, BitConverter.GetBytes(3444), EnumBtrieveOperationCodes.QueryGreaterOrEqual).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(1);
            new MBBSEmuRecord(btrieve.GetRecord(btrieve.Position)?.Data).Key1.Should().Be(3444);

            btrieve.PerformOperation(1, null, EnumBtrieveOperationCodes.QueryNext).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(2);
            new MBBSEmuRecord(btrieve.GetRecord(btrieve.Position)?.Data).Key1.Should().Be(7776);

            btrieve.PerformOperation(1, null, EnumBtrieveOperationCodes.QueryNext).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(3);
            new MBBSEmuRecord(btrieve.GetRecord(btrieve.Position)?.Data).Key1.Should().Be(1052234073);

            btrieve.PerformOperation(1, null, EnumBtrieveOperationCodes.QueryNext).Should().BeFalse();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(3);
        }

        [Fact]
        public void SeekByKeyGreaterOrEqualFound()
        {
            CopyFilesToTempPath("MBBSEMU.DB");

            var serviceResolver = new ServiceResolver();
            using var btrieve = new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT");

            btrieve.DeleteAll();

            btrieve.PerformOperation(1, BitConverter.GetBytes(2_000_000_000), EnumBtrieveOperationCodes.QueryGreaterOrEqual).Should().BeFalse();
        }

        [Fact]
        public void SeekByKeyLessString()
        {
            CopyFilesToTempPath("MBBSEMU.DB");

            var serviceResolver = new ServiceResolver();
            using var btrieve = new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT");

            btrieve.PerformOperation(2, Encoding.ASCII.GetBytes("7776"), EnumBtrieveOperationCodes.QueryLess).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(1);
            new MBBSEmuRecord(btrieve.GetRecord(btrieve.Position)?.Data).Key2.Should().Be("3444");

            btrieve.PerformOperation(2, null, EnumBtrieveOperationCodes.QueryNext).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(2);
            new MBBSEmuRecord(btrieve.GetRecord(btrieve.Position)?.Data).Key2.Should().Be("7776");

            btrieve.PerformOperation(2, null, EnumBtrieveOperationCodes.QueryNext).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(3);
            new MBBSEmuRecord(btrieve.GetRecord(btrieve.Position)?.Data).Key2.Should().Be("StringValue");

            btrieve.PerformOperation(2, null, EnumBtrieveOperationCodes.QueryNext).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(4);
            new MBBSEmuRecord(btrieve.GetRecord(btrieve.Position)?.Data).Key2.Should().Be("stringValue");

            btrieve.PerformOperation(2, null, EnumBtrieveOperationCodes.QueryNext).Should().BeFalse();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(4);
        }

        [Fact]
        public void SeekByKeyLessInteger()
        {
            CopyFilesToTempPath("MBBSEMU.DB");

            var serviceResolver = new ServiceResolver();
            using var btrieve = new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT");

            btrieve.PerformOperation(1, BitConverter.GetBytes(7776), EnumBtrieveOperationCodes.QueryLess).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(1);
            new MBBSEmuRecord(btrieve.GetRecord(btrieve.Position)?.Data).Key1.Should().Be(3444);

            btrieve.PerformOperation(1, null, EnumBtrieveOperationCodes.QueryNext).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(2);
            new MBBSEmuRecord(btrieve.GetRecord(btrieve.Position)?.Data).Key1.Should().Be(7776);

            btrieve.PerformOperation(1, null, EnumBtrieveOperationCodes.QueryNext).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(3);
            new MBBSEmuRecord(btrieve.GetRecord(btrieve.Position)?.Data).Key1.Should().Be(1052234073);

            btrieve.PerformOperation(1, null, EnumBtrieveOperationCodes.QueryNext).Should().BeFalse();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(3);
        }

        [Fact]
        public void SeekByKeyLessNotFound()
        {
            CopyFilesToTempPath("MBBSEMU.DB");

            var serviceResolver = new ServiceResolver();
            using var btrieve = new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT");

            btrieve.PerformOperation(1, BitConverter.GetBytes(-2_000_000_000), EnumBtrieveOperationCodes.QueryLess).Should().BeFalse();
        }

        [Fact]
        public void SeekByKeyLessOrEqualString()
        {
            CopyFilesToTempPath("MBBSEMU.DB");

            var serviceResolver = new ServiceResolver();
            using var btrieve = new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT");

            btrieve.PerformOperation(2, Encoding.ASCII.GetBytes("7776"), EnumBtrieveOperationCodes.QueryLessOrEqual).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(2);
            new MBBSEmuRecord(btrieve.GetRecord(btrieve.Position)?.Data).Key2.Should().Be("7776");

            btrieve.PerformOperation(2, null, EnumBtrieveOperationCodes.QueryNext).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(3);
            new MBBSEmuRecord(btrieve.GetRecord(btrieve.Position)?.Data).Key2.Should().Be("StringValue");

            btrieve.PerformOperation(2, null, EnumBtrieveOperationCodes.QueryNext).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(4);
            new MBBSEmuRecord(btrieve.GetRecord(btrieve.Position)?.Data).Key2.Should().Be("stringValue");

            btrieve.PerformOperation(2, null, EnumBtrieveOperationCodes.QueryNext).Should().BeFalse();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(4);
        }

        [Fact]
        public void SeekByKeyLessOrEqualInteger()
        {
            CopyFilesToTempPath("MBBSEMU.DB");

            var serviceResolver = new ServiceResolver();
            using var btrieve = new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT");

            btrieve.PerformOperation(1, BitConverter.GetBytes(7776), EnumBtrieveOperationCodes.QueryLessOrEqual).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(2);
            new MBBSEmuRecord(btrieve.GetRecord(btrieve.Position)?.Data).Key1.Should().Be(7776);

            btrieve.PerformOperation(1, null, EnumBtrieveOperationCodes.QueryNext).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(3);
            new MBBSEmuRecord(btrieve.GetRecord(btrieve.Position)?.Data).Key1.Should().Be(1052234073);

            btrieve.PerformOperation(1, null, EnumBtrieveOperationCodes.QueryNext).Should().BeFalse();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(3);
        }

        [Fact]
        public void SeekByKeyLessOrEqualFound()
        {
            CopyFilesToTempPath("MBBSEMU.DB");

            var serviceResolver = new ServiceResolver();
            using var btrieve = new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT");

            btrieve.DeleteAll();

            btrieve.PerformOperation(1, BitConverter.GetBytes(-2_000_000_000), EnumBtrieveOperationCodes.QueryLessOrEqual).Should().BeFalse();
        }

        private const int ACS_RECORD_LENGTH = 128;

        private static byte[] CreateRecord(string username)
        {
            var usernameBytes = Encoding.ASCII.GetBytes(username);
            var record = new byte[ACS_RECORD_LENGTH];

            Array.Fill(record, (byte)0xFF);

            Array.Copy(usernameBytes, 0, record, 2, usernameBytes.Length);
            record[2 + usernameBytes.Length] = 0;

            return record;
        }

        private static BtrieveFile CreateACSBtrieveFile()
        {
            // all upper case acs
            var acs = new byte[256];
            for (var i = 0; i < acs.Length; ++i)
                acs[i] = (byte)i;
            for (var i = 'a'; i <= 'z'; ++i)
                acs[i] = (byte)char.ToUpper(i);

            var btrieveFile = new BtrieveFile()
            {
                RecordLength = ACS_RECORD_LENGTH,
                FileName = "TEST.DAT",
                RecordCount = 0,
                ACSName = "ALLCAPS",
                ACS = acs,
            };

            var key = new BtrieveKey();
            key.Segments.Add(new BtrieveKeyDefinition()
                {
                    Number = 0,
                    Attributes = EnumKeyAttributeMask.NumberedACS | EnumKeyAttributeMask.UseExtendedDataType,
                    DataType = EnumKeyDataType.Zstring,
                    Offset = 2,
                    Length = 30,
                    Segment = false,
                    ACS = acs
                });

            btrieveFile.Keys.Add(0, key);

            btrieveFile.Records.Add(new BtrieveRecord(1, CreateRecord("Sysop")));
            btrieveFile.Records.Add(new BtrieveRecord(2, CreateRecord("Paladine")));
            btrieveFile.Records.Add(new BtrieveRecord(3, CreateRecord("Testing")));
            return btrieveFile;
        }

        [Fact]
        public void CreatesACS()
        {
            var btrieve = new BtrieveFileProcessor();
            var connectionString = "Data Source=acs.db;Mode=Memory";

            btrieve.CreateSqliteDBWithConnectionString(connectionString, CreateACSBtrieveFile());

            btrieve.GetRecordCount().Should().Be(3);
            btrieve.GetKeyLength(0).Should().Be(30);

            // validate acs
            using var cmd = new SqliteCommand("SELECT acs_name, acs, LENGTH(acs) FROM metadata_t", btrieve.Connection);
            using var reader = cmd.ExecuteReader();
            reader.Read().Should().BeTrue();
            reader.GetString(0).Should().Be("ALLCAPS");
            reader.GetInt32(2).Should().Be(256);
        }

        [Fact]
        public void ACSSeekByKey()
        {
            var btrieve = new BtrieveFileProcessor();
            var connectionString = "Data Source=acs.db;Mode=Memory";

            btrieve.CreateSqliteDBWithConnectionString(connectionString, CreateACSBtrieveFile());

            var key = new byte[30];
            Array.Copy(Encoding.ASCII.GetBytes("paladine"), key, 8);

            btrieve.PerformOperation(0, key, EnumBtrieveOperationCodes.QueryEqual).Should().BeTrue();
            var record = btrieve.GetRecord(btrieve.Position);
            record.Should().NotBeNull();
            record.Offset.Should().Be(2);
            // we searched by paladine but the actual data is Paladine
            record.Data[2].Should().Be((byte)'P');
        }

        [Fact]
        public void ACSInsertDuplicateFails()
        {
            var btrieve = new BtrieveFileProcessor();
            var connectionString = "Data Source=acs.db;Mode=Memory";

            btrieve.CreateSqliteDBWithConnectionString(connectionString, CreateACSBtrieveFile());

            var record = new byte[ACS_RECORD_LENGTH];
            Array.Copy(Encoding.ASCII.GetBytes("paladine"), 0, record, 2, 8);

            btrieve.Insert(record).Should().Be(0);
        }

        private static BtrieveFile CreateKeylessBtrieveFile()
        {
            var btrieveFile = new BtrieveFile()
            {
                RecordLength = ACS_RECORD_LENGTH,
                FileName = "TEST.DAT",
                RecordCount = 0,
            };

            btrieveFile.Records.Add(new BtrieveRecord(1, CreateRecord("Sysop")));
            btrieveFile.Records.Add(new BtrieveRecord(2, CreateRecord("Paladine")));
            btrieveFile.Records.Add(new BtrieveRecord(3, CreateRecord("Testing")));
            return btrieveFile;
        }

        [Fact]
        public void KeylessDatabaseEnumeration()
        {
            var btrieve = new BtrieveFileProcessor();
            var connectionString = "Data Source=acs.db;Mode=Memory";

            btrieve.CreateSqliteDBWithConnectionString(connectionString, CreateKeylessBtrieveFile());

            var record = new byte[ACS_RECORD_LENGTH];
            Array.Copy(Encoding.ASCII.GetBytes("paladine"), 0, record, 2, 8);

            btrieve.Insert(record).Should().Be(4);

            btrieve.PerformOperation(-1, ReadOnlySpan<byte>.Empty, EnumBtrieveOperationCodes.StepFirst).Should().BeTrue();
            Encoding.ASCII.GetString(btrieve.GetRecord().AsSpan().Slice(2, 30)).TrimEnd('?', (char)0).Should().Be("Sysop");

            btrieve.PerformOperation(-1, ReadOnlySpan<byte>.Empty, EnumBtrieveOperationCodes.StepPrevious).Should().BeFalse();

            btrieve.PerformOperation(-1, ReadOnlySpan<byte>.Empty, EnumBtrieveOperationCodes.StepNext).Should().BeTrue();
            Encoding.ASCII.GetString(btrieve.GetRecord().AsSpan().Slice(2, 30)).TrimEnd('?', (char)0).Should().Be("Paladine");

            btrieve.PerformOperation(-1, ReadOnlySpan<byte>.Empty, EnumBtrieveOperationCodes.StepNext).Should().BeTrue();
            Encoding.ASCII.GetString(btrieve.GetRecord().AsSpan().Slice(2, 30)).TrimEnd('?', (char)0).Should().Be("Testing");

            btrieve.PerformOperation(-1, ReadOnlySpan<byte>.Empty, EnumBtrieveOperationCodes.StepNext).Should().BeTrue();
            Encoding.ASCII.GetString(btrieve.GetRecord().AsSpan().Slice(2, 30)).TrimEnd('?', (char)0).Should().Be("paladine");

            btrieve.PerformOperation(-1, ReadOnlySpan<byte>.Empty, EnumBtrieveOperationCodes.StepNext).Should().BeFalse();
        }

        [Fact]
        public void KeylessDataQueryFails()
        {
            var btrieve = new BtrieveFileProcessor();
            var connectionString = "Data Source=acs.db;Mode=Memory";

            btrieve.CreateSqliteDBWithConnectionString(connectionString, CreateKeylessBtrieveFile());

            Action act = () => btrieve.PerformOperation(0, Encoding.ASCII.GetBytes("test"), EnumBtrieveOperationCodes.QueryEqual);
            act.Should().Throw<KeyNotFoundException>();
        }

        /// <summary>Creates a copy of data shrunk by cutOff bytes at the end</summary>
        private static byte[] MakeSmaller(byte[] data, int cutOff)
        {
            var ret = new byte[data.Length - cutOff];
            Array.Copy(data, 0, ret, 0, ret.Length);
            return ret;
        }
    }
}
