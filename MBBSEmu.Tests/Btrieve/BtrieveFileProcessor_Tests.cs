using FluentAssertions;
using MBBSEmu.Btrieve;
using MBBSEmu.Btrieve.Enums;
using MBBSEmu.DependencyInjection;
using MBBSEmu.IO;
using MBBSEmu.Resources;
using MBBSEmu.Testing;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using MBBSEmu.Logging;
using Xunit;

namespace MBBSEmu.Tests.Btrieve
{
    /* Data layout as follows:

    sqlite> select * from data_t;
        id          data        key_0       key_1       key_2       key_3
        ----------  ----------  ----------  ----------  ----------  ----------
        1                       Sysop       3444        3444        1
        2                       Sysop       7776        7776        2
        3                       Sysop       1052234073  StringValu  3
        4                       Sysop       -615634567  stringValu  4
    */

    [Collection("Non-Parallel")]
    public class BtrieveFileProcessor_Tests : TestBase, IDisposable
    {
        const int CACHE_SIZE = 8;

        private const string EXPECTED_METADATA_T_SQL = "CREATE TABLE metadata_t(record_length INTEGER NOT NULL, physical_record_length INTEGER NOT NULL, page_length INTEGER NOT NULL, variable_length_records INTEGER NOT NULL, version INTEGER NOT NULL, acs_name STRING, acs BLOB)";
        private const string EXPECTED_KEYS_T_SQL = "CREATE TABLE keys_t(id INTEGER PRIMARY KEY, number INTEGER NOT NULL, segment INTEGER NOT NULL, attributes INTEGER NOT NULL, data_type INTEGER NOT NULL, offset INTEGER NOT NULL, length INTEGER NOT NULL, null_value INTEGER NOT NULL, UNIQUE(number, segment))";
        private const string EXPECTED_DATA_T_SQL = "CREATE TABLE data_t(id INTEGER PRIMARY KEY, data BLOB NOT NULL, key_0 TEXT, key_1 INTEGER NOT NULL UNIQUE, key_2 TEXT, key_3 INTEGER NOT NULL UNIQUE)";

        protected readonly string _modulePath;



        public BtrieveFileProcessor_Tests()
        {
            _modulePath = GetModulePath();

            Directory.CreateDirectory(_modulePath);
        }

        public void Dispose()
        {
            SqliteConnection.ClearAllPools();

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

            var btrieve = new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT", CACHE_SIZE);

            btrieve.Keys.Count.Should().Be(4);
            btrieve.RecordLength.Should().Be(MBBSEmuRecordStruct.RECORD_LENGTH);
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
            var connectionString = BtrieveFileProcessor.GetDefaultConnectionStringBuilder(fullPath).ToString();
            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            using (var stmt = new SqliteCommand("SELECT version FROM metadata_t", connection))
                stmt.ExecuteScalar().Should().Be(BtrieveFileProcessor.CURRENT_VERSION);

            using (var stmt = new SqliteCommand("SELECT sql FROM sqlite_master WHERE name = 'metadata_t';", connection))
                stmt.ExecuteScalar().Should().Be(EXPECTED_METADATA_T_SQL);

            using (var stmt = new SqliteCommand("SELECT sql FROM sqlite_master WHERE name = 'keys_t';", connection))
                stmt.ExecuteScalar().Should().Be(EXPECTED_KEYS_T_SQL);

            using (var stmt = new SqliteCommand("SELECT sql FROM sqlite_master WHERE name = 'data_t';", connection))
                stmt.ExecuteScalar().Should().Be(EXPECTED_DATA_T_SQL);

            using (var stmt = new SqliteCommand("SELECT name, tbl_name, sql FROM sqlite_master WHERE type = 'trigger';", connection))
            {
                using var reader = stmt.ExecuteReader();
                reader.Read().Should().BeTrue();
                reader.GetString(0).Should().Be("non_modifiable");
                reader.GetString(1).Should().Be("data_t");
                reader.GetString(2).Should().Be("CREATE TRIGGER non_modifiable BEFORE UPDATE ON data_t " +
                    "BEGIN SELECT CASE " +
                    "WHEN NEW.key_0 != OLD.key_0 " +
                    "THEN RAISE (ABORT,'You modified a non-modifiable key_0!') " +
                    "WHEN NEW.key_3 != OLD.key_3 " +
                    "THEN RAISE (ABORT,'You modified a non-modifiable key_3!') END; END");
            }
        }

        [Fact]
        public void LoadsFileProperly()
        {
            CopyFilesToTempPath("MBBSEMU.DB");

            var serviceResolver = new ServiceResolver();

            using var btrieve = new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT", CACHE_SIZE);

            btrieve.Keys.Count.Should().Be(4);
            btrieve.RecordLength.Should().Be(MBBSEmuRecordStruct.RECORD_LENGTH);
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
            using var btrieve = new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT", CACHE_SIZE);

            btrieve.PerformOperation(-1, ReadOnlySpan<byte>.Empty, EnumBtrieveOperationCodes.StepFirst).Should().BeTrue();
            btrieve.Position.Should().Be(1);
            new MBBSEmuRecordStruct(btrieve.GetRecord()).Key1.Should().Be(3444);

            btrieve.PerformOperation(-1, ReadOnlySpan<byte>.Empty, EnumBtrieveOperationCodes.StepNext).Should().BeTrue();
            btrieve.Position.Should().Be(2);
            new MBBSEmuRecordStruct(btrieve.GetRecord()).Key1.Should().Be(7776);

            btrieve.PerformOperation(-1, ReadOnlySpan<byte>.Empty, EnumBtrieveOperationCodes.StepNext).Should().BeTrue();
            btrieve.Position.Should().Be(3);
            new MBBSEmuRecordStruct(btrieve.GetRecord()).Key1.Should().Be(1052234073);

            btrieve.PerformOperation(-1, ReadOnlySpan<byte>.Empty, EnumBtrieveOperationCodes.StepNext).Should().BeTrue();
            btrieve.Position.Should().Be(4);
            new MBBSEmuRecordStruct(btrieve.GetRecord()).Key1.Should().Be(-615634567);

            btrieve.PerformOperation(-1, ReadOnlySpan<byte>.Empty, EnumBtrieveOperationCodes.StepNext).Should().BeFalse();
            btrieve.Position.Should().Be(4);
        }

        [Fact]
        public void EnumeratesInitialEntriesReverse()
        {
            CopyFilesToTempPath("MBBSEMU.DB");

            var serviceResolver = new ServiceResolver();
            using var btrieve = new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT", CACHE_SIZE);

            btrieve.PerformOperation(-1, ReadOnlySpan<byte>.Empty, EnumBtrieveOperationCodes.StepLast).Should().BeTrue();
            btrieve.Position.Should().Be(4);
            new MBBSEmuRecordStruct(btrieve.GetRecord()).Key1.Should().Be(-615634567);

            btrieve.PerformOperation(-1, ReadOnlySpan<byte>.Empty, EnumBtrieveOperationCodes.StepPrevious).Should().BeTrue();
            btrieve.Position.Should().Be(3);
            new MBBSEmuRecordStruct(btrieve.GetRecord()).Key1.Should().Be(1052234073);

            btrieve.PerformOperation(-1, ReadOnlySpan<byte>.Empty, EnumBtrieveOperationCodes.StepPrevious).Should().BeTrue();
            btrieve.Position.Should().Be(2);
            new MBBSEmuRecordStruct(btrieve.GetRecord()).Key1.Should().Be(7776);

            btrieve.PerformOperation(-1, ReadOnlySpan<byte>.Empty, EnumBtrieveOperationCodes.StepPrevious).Should().BeTrue();
            btrieve.Position.Should().Be(1);
            new MBBSEmuRecordStruct(btrieve.GetRecord()).Key1.Should().Be(3444);

            btrieve.PerformOperation(-1, ReadOnlySpan<byte>.Empty, EnumBtrieveOperationCodes.StepPrevious).Should().BeFalse();
            btrieve.Position.Should().Be(1);
        }

        [Fact]
        public void RandomAccess()
        {
            CopyFilesToTempPath("MBBSEMU.DB");

            var serviceResolver = new ServiceResolver();
            using var btrieve = new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT", CACHE_SIZE);

            var record = new MBBSEmuRecordStruct(btrieve.GetRecord(4)?.Data);
            record.Key0.Should().Be("Sysop");
            record.Key1.Should().Be(-615634567);
            record.Key2.Should().Be("stringValue");
            record.Key3.Should().Be(4);

            new MBBSEmuRecordStruct(btrieve.GetRecord(3)?.Data).Key1.Should().Be(1052234073);
            new MBBSEmuRecordStruct(btrieve.GetRecord(2)?.Data).Key1.Should().Be(7776);
            new MBBSEmuRecordStruct(btrieve.GetRecord(1)?.Data).Key1.Should().Be(3444);

            new MBBSEmuRecordStruct(btrieve.GetRecord(2)?.Data).Key1.Should().Be(7776);
        }

        [Fact]
        public void RandomInvalidAccess()
        {
            CopyFilesToTempPath("MBBSEMU.DB");

            var serviceResolver = new ServiceResolver();
            using var btrieve = new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT", CACHE_SIZE);

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
            using var btrieve = new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT", CACHE_SIZE);

            btrieve.GetRecordCount().Should().Be(4);
        }

        [Fact]
        public void RecordDeleteAll()
        {
            CopyFilesToTempPath("MBBSEMU.DB");

            var serviceResolver = new ServiceResolver();
            using var btrieve =
                new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT", CACHE_SIZE)
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
                new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT", CACHE_SIZE)
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
                new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT", CACHE_SIZE)
                {
                    Position = 2
                };

            btrieve.Delete().Should().BeTrue();

            btrieve.PerformOperation(-1, ReadOnlySpan<byte>.Empty, EnumBtrieveOperationCodes.StepFirst).Should().BeTrue();
            btrieve.Position.Should().Be(1);
            new MBBSEmuRecordStruct(btrieve.GetRecord()).Key1.Should().Be(3444);

            btrieve.PerformOperation(-1, ReadOnlySpan<byte>.Empty, EnumBtrieveOperationCodes.StepNext).Should().BeTrue();
            btrieve.Position.Should().Be(3);
            new MBBSEmuRecordStruct(btrieve.GetRecord()).Key1.Should().Be(1052234073);

            btrieve.PerformOperation(-1, ReadOnlySpan<byte>.Empty, EnumBtrieveOperationCodes.StepNext).Should().BeTrue();
            btrieve.Position.Should().Be(4);
            new MBBSEmuRecordStruct(btrieve.GetRecord()).Key1.Should().Be(-615634567);

            btrieve.PerformOperation(-1, ReadOnlySpan<byte>.Empty, EnumBtrieveOperationCodes.StepNext).Should().BeFalse();
        }

        [Fact]
        public void InsertionTest()
        {
            CopyFilesToTempPath("MBBSEMU.DB");

            var serviceResolver = new ServiceResolver();
            using var btrieve = new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT", CACHE_SIZE);

            var record = new MBBSEmuRecordStruct { Key0 = "Paladine", Key1 = 31337, Key2 = "In orbe terrarum, optimus sum" };

            var insertedId = btrieve.Insert(record.Data, EnumLogLevel.Error);
            insertedId.Should().Be(5);

            record = new MBBSEmuRecordStruct(btrieve.GetRecord(insertedId)?.Data);
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
            using var btrieve = new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT", CACHE_SIZE);

            var record = new MBBSEmuRecordStruct
            {
                Key0 = "Paladine",
                Key1 = 31337,
                Key2 = "In orbe terrarum, optimus sum",
                Key3 = 4444
            };

            var insertedId = btrieve.Insert(record.Data, EnumLogLevel.Error);
            insertedId.Should().Be(5);

            record = new MBBSEmuRecordStruct(btrieve.GetRecord(insertedId)?.Data);
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
            using var btrieve = new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT", CACHE_SIZE);

            var record = new MBBSEmuRecordStruct { Key0 = "Paladine", Key1 = 31337, Key2 = "In orbe terrarum, optimus sum" };

            var insertedId = btrieve.Insert(MakeSmaller(record.Data, 14), EnumLogLevel.Error);
            insertedId.Should().Be(5);

            record = new MBBSEmuRecordStruct(btrieve.GetRecord(insertedId)?.Data);
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
            using var btrieve = new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT", CACHE_SIZE);

            var record = new MBBSEmuRecordStruct
            {
                Key0 = "Paladine",
                Key1 = 3444, // constraint failure here
                Key2 = "In orbe terrarum, optimus sum",
                Key3 = 4444
            };


            var insertedId = btrieve.Insert(record.Data, EnumLogLevel.Debug);
            insertedId.Should().Be(0);
        }

        [Fact]
        public void UpdateTest()
        {
            CopyFilesToTempPath("MBBSEMU.DB");

            var serviceResolver = new ServiceResolver();
            using var btrieve = new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT", CACHE_SIZE);
            var record = new MBBSEmuRecordStruct { Key0 = "Sysop", Key1 = 31337, Key2 = "In orbe terrarum, optimus sum", Key3 = 1 };

            btrieve.Update(1, record.Data).Should().Be(BtrieveError.Success);

            record = new MBBSEmuRecordStruct(btrieve.GetRecord(1)?.Data);
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
            using var btrieve = new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT", CACHE_SIZE);

            var record = new MBBSEmuRecordStruct
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
            btrieve.Update(2, MakeSmaller(record.Data, 3)).Should().Be(BtrieveError.Success);

            record = new MBBSEmuRecordStruct(btrieve.GetRecord(2)?.Data);
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
            using var btrieve = new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT", CACHE_SIZE);

            var record = new MBBSEmuRecordStruct { Key1 = 7776, Key2 = "In orbe terrarum, optimus sum", Key3 = 1 };
            // constraint failure here
            btrieve.Update(1, record.Data).Should().Be(BtrieveError.DuplicateKeyValue);

            // assert update didn't occur
            record = new MBBSEmuRecordStruct(btrieve.GetRecord(1)?.Data);
            record.Key0.Should().Be("Sysop");
            record.Key1.Should().Be(3444);
            record.Key2.Should().Be("3444");
            record.Key3.Should().Be(1);
        }

        [Fact]
        public void UpdateTestNonModifiableKeyModifiedFailed()
        {
            CopyFilesToTempPath("MBBSEMU.DB");

            var serviceResolver = new ServiceResolver();
            using var btrieve = new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT", CACHE_SIZE);

            var record = new MBBSEmuRecordStruct { Key1 = 7776, Key2 = "In orbe terrarum, optimus sum", Key3 = 333333 };

            // non-modifiable trigger failure here
            Action action = () => btrieve.Update(1, record.Data).Should().NotBe(BtrieveError.Success);
            action.Should().Throw<SqliteException>()
                .Where(ex => ex.SqliteErrorCode == BtrieveFileProcessor.SQLITE_CONSTRAINT)
                .Where(ex => ex.SqliteExtendedErrorCode == BtrieveFileProcessor.SQLITE_CONSTRAINT_TRIGGER);

            // assert update didn't occur
            record = new MBBSEmuRecordStruct(btrieve.GetRecord(1)?.Data);
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
            using var btrieve = new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT", CACHE_SIZE);

            var record = new MBBSEmuRecordStruct { Key0 = "Paladine", Key1 = 31337, Key2 = "In orbe terrarum, optimus sum" };

            btrieve.Update(5, record.Data).Should().Be(BtrieveError.InvalidKeyNumber);
        }

        [Fact]
        public void SeekByKeyStringDuplicates()
        {
            CopyFilesToTempPath("MBBSEMU.DB");

            var serviceResolver = new ServiceResolver();
            using var btrieve = new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT", CACHE_SIZE);

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
            using var btrieve = new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT", CACHE_SIZE);
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
            using var btrieve = new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT", CACHE_SIZE);
            var key = Encoding.ASCII.GetBytes("StringValue");

            btrieve.PerformOperation(2, key, EnumBtrieveOperationCodes.QueryEqual).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(3);

            btrieve.PerformOperation(2, key, EnumBtrieveOperationCodes.QueryNext).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(4);
            new MBBSEmuRecordStruct(btrieve.GetRecord(btrieve.Position)?.Data).Key2.Should().Be("stringValue");

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
            using var btrieve = new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT", CACHE_SIZE);
            var key = BitConverter.GetBytes(1052234073);

            btrieve.PerformOperation(1, key, EnumBtrieveOperationCodes.QueryEqual).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(3);
            new MBBSEmuRecordStruct(btrieve.GetRecord(btrieve.Position)?.Data).Key1.Should().Be(1052234073);

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
            using var btrieve = new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT", CACHE_SIZE);
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
            using var btrieve = new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT", CACHE_SIZE);

            btrieve.PerformOperation(2, null, EnumBtrieveOperationCodes.QueryFirst).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(1);
            new MBBSEmuRecordStruct(btrieve.GetRecord(btrieve.Position)?.Data).Key2.Should().Be("3444");

            btrieve.PerformOperation(2, null, EnumBtrieveOperationCodes.QueryPrevious).Should().BeFalse();

            btrieve.PerformOperation(2, null, EnumBtrieveOperationCodes.QueryNext).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(2);
            new MBBSEmuRecordStruct(btrieve.GetRecord(btrieve.Position)?.Data).Key2.Should().Be("7776");

            btrieve.PerformOperation(2, null, EnumBtrieveOperationCodes.QueryNext).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(3);
            new MBBSEmuRecordStruct(btrieve.GetRecord(btrieve.Position)?.Data).Key2.Should().Be("StringValue");

            btrieve.PerformOperation(2, null, EnumBtrieveOperationCodes.QueryNext).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(4);
            new MBBSEmuRecordStruct(btrieve.GetRecord(btrieve.Position)?.Data).Key2.Should().Be("stringValue");

            btrieve.PerformOperation(2, null, EnumBtrieveOperationCodes.QueryNext).Should().BeFalse();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(4);
        }

        [Fact]
        public void SeekByKeyFirstInteger()
        {
            CopyFilesToTempPath("MBBSEMU.DB");

            var serviceResolver = new ServiceResolver();
            using var btrieve = new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT", CACHE_SIZE);

            btrieve.PerformOperation(1, null, EnumBtrieveOperationCodes.QueryFirst).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(4);
            new MBBSEmuRecordStruct(btrieve.GetRecord(btrieve.Position)?.Data).Key1.Should().Be(-615634567);

            btrieve.PerformOperation(1, null, EnumBtrieveOperationCodes.QueryPrevious).Should().BeFalse();

            btrieve.PerformOperation(1, null, EnumBtrieveOperationCodes.QueryNext).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(1);
            new MBBSEmuRecordStruct(btrieve.GetRecord(btrieve.Position)?.Data).Key1.Should().Be(3444);

            btrieve.PerformOperation(1, null, EnumBtrieveOperationCodes.QueryNext).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(2);
            new MBBSEmuRecordStruct(btrieve.GetRecord(btrieve.Position)?.Data).Key1.Should().Be(7776);

            btrieve.PerformOperation(1, null, EnumBtrieveOperationCodes.QueryNext).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(3);
            new MBBSEmuRecordStruct(btrieve.GetRecord(btrieve.Position)?.Data).Key1.Should().Be(1052234073);

            btrieve.PerformOperation(1, null, EnumBtrieveOperationCodes.QueryNext).Should().BeFalse();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(3);
        }

        [Fact]
        public void SeekByKeyFirstNotFound()
        {
            CopyFilesToTempPath("MBBSEMU.DB");

            var serviceResolver = new ServiceResolver();
            using var btrieve = new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT", CACHE_SIZE);

            btrieve.DeleteAll();

            btrieve.PerformOperation(0, null, EnumBtrieveOperationCodes.QueryFirst).Should().BeFalse();
        }

        [Fact]
        public void SeekByKeyLastString()
        {
            CopyFilesToTempPath("MBBSEMU.DB");

            var serviceResolver = new ServiceResolver();
            using var btrieve = new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT", CACHE_SIZE);

            btrieve.PerformOperation(2, null, EnumBtrieveOperationCodes.QueryLast).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(4);
            new MBBSEmuRecordStruct(btrieve.GetRecord(btrieve.Position)?.Data).Key2.Should().Be("stringValue");

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
            using var btrieve = new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT", CACHE_SIZE);

            btrieve.PerformOperation(1, null, EnumBtrieveOperationCodes.QueryLast).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(3);
            new MBBSEmuRecordStruct(btrieve.GetRecord(btrieve.Position)?.Data).Key1.Should().Be(1052234073);

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
            using var btrieve = new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT", CACHE_SIZE);

            btrieve.DeleteAll();

            btrieve.PerformOperation(0, null, EnumBtrieveOperationCodes.QueryLast).Should().BeFalse();
        }

        [Fact]
        public void SeekByKeyGreaterString()
        {
            CopyFilesToTempPath("MBBSEMU.DB");

            var serviceResolver = new ServiceResolver();
            using var btrieve = new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT", CACHE_SIZE);

            btrieve.PerformOperation(2, Encoding.ASCII.GetBytes("7776"), EnumBtrieveOperationCodes.QueryGreater).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(3);
            new MBBSEmuRecordStruct(btrieve.GetRecord(btrieve.Position)?.Data).Key2.Should().Be("StringValue");

            btrieve.PerformOperation(2, null, EnumBtrieveOperationCodes.QueryNext).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(4);
            new MBBSEmuRecordStruct(btrieve.GetRecord(btrieve.Position)?.Data).Key2.Should().Be("stringValue");

            btrieve.PerformOperation(2, null, EnumBtrieveOperationCodes.QueryNext).Should().BeFalse();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(4);
        }

        [Fact]
        public void SeekByKeyGreaterInteger()
        {
            CopyFilesToTempPath("MBBSEMU.DB");

            var serviceResolver = new ServiceResolver();
            using var btrieve = new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT", CACHE_SIZE);

            btrieve.PerformOperation(1, BitConverter.GetBytes(3444), EnumBtrieveOperationCodes.QueryGreater).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(2);
            new MBBSEmuRecordStruct(btrieve.GetRecord(btrieve.Position)?.Data).Key1.Should().Be(7776);

            btrieve.PerformOperation(1, null, EnumBtrieveOperationCodes.QueryNext).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(3);
            new MBBSEmuRecordStruct(btrieve.GetRecord(btrieve.Position)?.Data).Key1.Should().Be(1052234073);

            btrieve.PerformOperation(1, null, EnumBtrieveOperationCodes.QueryNext).Should().BeFalse();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(3);
        }

        [Fact]
        public void SeekByKeyGreaterNotFound()
        {
            CopyFilesToTempPath("MBBSEMU.DB");

            var serviceResolver = new ServiceResolver();
            using var btrieve = new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT", CACHE_SIZE);

            btrieve.PerformOperation(1, BitConverter.GetBytes(2_000_000_000), EnumBtrieveOperationCodes.QueryGreater).Should().BeFalse();
        }

        [Fact]
        public void SeekByKeyGreaterOrEqualString()
        {
            CopyFilesToTempPath("MBBSEMU.DB");

            var serviceResolver = new ServiceResolver();
            using var btrieve = new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT", CACHE_SIZE);

            btrieve.PerformOperation(2, Encoding.ASCII.GetBytes("7776"), EnumBtrieveOperationCodes.QueryGreaterOrEqual).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(2);
            new MBBSEmuRecordStruct(btrieve.GetRecord(btrieve.Position)?.Data).Key2.Should().Be("7776");

            btrieve.PerformOperation(2, null, EnumBtrieveOperationCodes.QueryNext).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(3);
            new MBBSEmuRecordStruct(btrieve.GetRecord(btrieve.Position)?.Data).Key2.Should().Be("StringValue");

            btrieve.PerformOperation(2, null, EnumBtrieveOperationCodes.QueryNext).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(4);
            new MBBSEmuRecordStruct(btrieve.GetRecord(btrieve.Position)?.Data).Key2.Should().Be("stringValue");

            btrieve.PerformOperation(2, null, EnumBtrieveOperationCodes.QueryNext).Should().BeFalse();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(4);
        }

        [Fact]
        public void SeekByKeyGreaterOrEqualInteger()
        {
            CopyFilesToTempPath("MBBSEMU.DB");

            var serviceResolver = new ServiceResolver();
            using var btrieve = new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT", CACHE_SIZE);

            btrieve.PerformOperation(1, BitConverter.GetBytes(3444), EnumBtrieveOperationCodes.QueryGreaterOrEqual).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(1);
            new MBBSEmuRecordStruct(btrieve.GetRecord(btrieve.Position)?.Data).Key1.Should().Be(3444);

            btrieve.PerformOperation(1, null, EnumBtrieveOperationCodes.QueryNext).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(2);
            new MBBSEmuRecordStruct(btrieve.GetRecord(btrieve.Position)?.Data).Key1.Should().Be(7776);

            btrieve.PerformOperation(1, null, EnumBtrieveOperationCodes.QueryNext).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(3);
            new MBBSEmuRecordStruct(btrieve.GetRecord(btrieve.Position)?.Data).Key1.Should().Be(1052234073);

            btrieve.PerformOperation(1, null, EnumBtrieveOperationCodes.QueryNext).Should().BeFalse();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(3);
        }

        [Fact]
        public void SeekByKeyGreaterOrEqualFound()
        {
            CopyFilesToTempPath("MBBSEMU.DB");

            var serviceResolver = new ServiceResolver();
            using var btrieve = new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT", CACHE_SIZE);

            btrieve.DeleteAll();

            btrieve.PerformOperation(1, BitConverter.GetBytes(2_000_000_000), EnumBtrieveOperationCodes.QueryGreaterOrEqual).Should().BeFalse();
        }

        [Fact]
        public void SeekByKeyLessString()
        {
            CopyFilesToTempPath("MBBSEMU.DB");

            var serviceResolver = new ServiceResolver();
            using var btrieve = new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT", CACHE_SIZE);

            btrieve.PerformOperation(2, Encoding.ASCII.GetBytes("7776"), EnumBtrieveOperationCodes.QueryLess).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(1);
            new MBBSEmuRecordStruct(btrieve.GetRecord(btrieve.Position)?.Data).Key2.Should().Be("3444");

            btrieve.PerformOperation(2, null, EnumBtrieveOperationCodes.QueryNext).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(2);
            new MBBSEmuRecordStruct(btrieve.GetRecord(btrieve.Position)?.Data).Key2.Should().Be("7776");

            btrieve.PerformOperation(2, null, EnumBtrieveOperationCodes.QueryNext).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(3);
            new MBBSEmuRecordStruct(btrieve.GetRecord(btrieve.Position)?.Data).Key2.Should().Be("StringValue");

            btrieve.PerformOperation(2, null, EnumBtrieveOperationCodes.QueryNext).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(4);
            new MBBSEmuRecordStruct(btrieve.GetRecord(btrieve.Position)?.Data).Key2.Should().Be("stringValue");

            btrieve.PerformOperation(2, null, EnumBtrieveOperationCodes.QueryNext).Should().BeFalse();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(4);
        }

        [Fact]
        public void SeekByKeyLessInteger()
        {
            CopyFilesToTempPath("MBBSEMU.DB");

            var serviceResolver = new ServiceResolver();
            using var btrieve = new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT", CACHE_SIZE);

            btrieve.PerformOperation(1, BitConverter.GetBytes(7776), EnumBtrieveOperationCodes.QueryLess).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(1);
            new MBBSEmuRecordStruct(btrieve.GetRecord(btrieve.Position)?.Data).Key1.Should().Be(3444);

            btrieve.PerformOperation(1, null, EnumBtrieveOperationCodes.QueryNext).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(2);
            new MBBSEmuRecordStruct(btrieve.GetRecord(btrieve.Position)?.Data).Key1.Should().Be(7776);

            btrieve.PerformOperation(1, null, EnumBtrieveOperationCodes.QueryNext).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(3);
            new MBBSEmuRecordStruct(btrieve.GetRecord(btrieve.Position)?.Data).Key1.Should().Be(1052234073);

            btrieve.PerformOperation(1, null, EnumBtrieveOperationCodes.QueryNext).Should().BeFalse();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(3);
        }

        [Fact]
        public void SeekByKeyLessNotFound()
        {
            CopyFilesToTempPath("MBBSEMU.DB");

            var serviceResolver = new ServiceResolver();
            using var btrieve = new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT", CACHE_SIZE);

            btrieve.PerformOperation(1, BitConverter.GetBytes(-2_000_000_000), EnumBtrieveOperationCodes.QueryLess).Should().BeFalse();
        }

        [Fact]
        public void SeekByKeyLessOrEqualString()
        {
            CopyFilesToTempPath("MBBSEMU.DB");

            var serviceResolver = new ServiceResolver();
            using var btrieve = new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT", CACHE_SIZE);

            btrieve.PerformOperation(2, Encoding.ASCII.GetBytes("7776"), EnumBtrieveOperationCodes.QueryLessOrEqual).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(2);
            new MBBSEmuRecordStruct(btrieve.GetRecord(btrieve.Position)?.Data).Key2.Should().Be("7776");

            btrieve.PerformOperation(2, null, EnumBtrieveOperationCodes.QueryNext).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(3);
            new MBBSEmuRecordStruct(btrieve.GetRecord(btrieve.Position)?.Data).Key2.Should().Be("StringValue");

            btrieve.PerformOperation(2, null, EnumBtrieveOperationCodes.QueryNext).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(4);
            new MBBSEmuRecordStruct(btrieve.GetRecord(btrieve.Position)?.Data).Key2.Should().Be("stringValue");

            btrieve.PerformOperation(2, null, EnumBtrieveOperationCodes.QueryNext).Should().BeFalse();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(4);
        }

        [Fact]
        public void SeekByKeyLessOrEqualInteger()
        {
            CopyFilesToTempPath("MBBSEMU.DB");

            var serviceResolver = new ServiceResolver();
            using var btrieve = new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT", CACHE_SIZE);

            btrieve.PerformOperation(1, BitConverter.GetBytes(7776), EnumBtrieveOperationCodes.QueryLessOrEqual).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(2);
            new MBBSEmuRecordStruct(btrieve.GetRecord(btrieve.Position)?.Data).Key1.Should().Be(7776);

            btrieve.PerformOperation(1, null, EnumBtrieveOperationCodes.QueryNext).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(3);
            new MBBSEmuRecordStruct(btrieve.GetRecord(btrieve.Position)?.Data).Key1.Should().Be(1052234073);

            btrieve.PerformOperation(1, null, EnumBtrieveOperationCodes.QueryNext).Should().BeFalse();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(3);
        }

        [Fact]
        public void SeekByKeyLessOrEqualFound()
        {
            CopyFilesToTempPath("MBBSEMU.DB");

            var serviceResolver = new ServiceResolver();
            using var btrieve = new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT", CACHE_SIZE);

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
            var connectionString = BtrieveFileProcessor.GetDefaultConnectionStringBuilder("acs.db");
            connectionString.Mode = SqliteOpenMode.Memory;

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
            var connectionString = BtrieveFileProcessor.GetDefaultConnectionStringBuilder("acs.db");
            connectionString.Mode = SqliteOpenMode.Memory;

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
            var connectionString = BtrieveFileProcessor.GetDefaultConnectionStringBuilder("acs.db");
            connectionString.Mode = SqliteOpenMode.Memory;

            btrieve.CreateSqliteDBWithConnectionString(connectionString, CreateACSBtrieveFile());

            var record = new byte[ACS_RECORD_LENGTH];
            Array.Copy(Encoding.ASCII.GetBytes("paladine"), 0, record, 2, 8);

            btrieve.Insert(record, EnumLogLevel.Debug).Should().Be(0);
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
            var connectionString = BtrieveFileProcessor.GetDefaultConnectionStringBuilder("acs.db");
            connectionString.Mode = SqliteOpenMode.Memory;

            btrieve.CreateSqliteDBWithConnectionString(connectionString, CreateKeylessBtrieveFile());

            var record = new byte[ACS_RECORD_LENGTH];
            Array.Copy(Encoding.ASCII.GetBytes("paladine"), 0, record, 2, 8);

            btrieve.Insert(record, EnumLogLevel.Error).Should().Be(4);

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
            var connectionString = BtrieveFileProcessor.GetDefaultConnectionStringBuilder("acs.db");
            connectionString.Mode = SqliteOpenMode.Memory;

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
