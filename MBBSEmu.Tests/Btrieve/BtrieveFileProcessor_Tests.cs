using FluentAssertions;
using MBBSEmu.Btrieve;
using MBBSEmu.Btrieve.Enums;
using MBBSEmu.DependencyInjection;
using MBBSEmu.IO;
using MBBSEmu.Resources;
using System.Data.SQLite;
using System.IO;
using System.Text;
using System;
using Xunit;

namespace MBBSEmu.Tests.Btrieve
{
    public class BtrieveFileProcessor_Tests : TestBase, IDisposable
    {
        const int RECORD_LENGTH = 74;

        private const string EXPECTED_METADATA_T_SQL = "CREATE TABLE metadata_t(record_length INTEGER NOT NULL, physical_record_length INTEGER NOT NULL, page_length INTEGER NOT NULL)";
        private const string EXPECTED_KEYS_T_SQL = "CREATE TABLE keys_t(id INTEGER PRIMARY KEY, number INTEGER NOT NULL, segment INTEGER NOT NULL, attributes INTEGER NOT NULL, data_type INTEGER NOT NULL, offset INTEGER NOT NULL, length INTEGER NOT NULL, UNIQUE(number, segment))";
        private const string EXPECTED_DATA_T_SQL = "CREATE TABLE data_t(id INTEGER PRIMARY KEY, data BLOB NOT NULL, key_0 TEXT NOT NULL, key_1 INTEGER NOT NULL UNIQUE, key_2 TEXT NOT NULL, key_3 INTEGER NOT NULL UNIQUE)";

        private static readonly Random RANDOM = new Random();

        protected readonly string _modulePath = Path.Join(Path.GetTempPath(), $"mbbsemu{RANDOM.Next()}");

        private class MBBSEmuRecord
        {
            public byte[] Data { get; }

            // offset 2, length 32
            public string Key0 {
                get => Encoding.ASCII.GetString(Data.AsSpan().Slice(2, 32)).TrimEnd((char) 0);
                set => Array.Copy(Encoding.ASCII.GetBytes(value), 0, Data, 2, value.Length);
            }

            // offset 34, length 4
            public int Key1 {
                get => BitConverter.ToInt32(Data, 34);
                set => Array.Copy(BitConverter.GetBytes(value), 0, Data, 34, 4);
            }

            // offset 38, length 32
            public string Key2 {
                get => Encoding.ASCII.GetString(Data.AsSpan().Slice(38, 32)).TrimEnd((char) 0);
                set => Array.Copy(Encoding.ASCII.GetBytes(value), 0, Data, 38, value.Length);
            }

            // offset 70, length 4
            public int Key3 {
                get => BitConverter.ToInt32(Data, 70);
                set => Array.Copy(BitConverter.GetBytes(value), 0, Data, 70, 4);
            }

            public MBBSEmuRecord() : this(new byte[RECORD_LENGTH]) {}

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
            Directory.Delete(_modulePath,  recursive: true);
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

            ServiceResolver serviceResolver = new ServiceResolver(ServiceResolver.GetTestDefaults());

            var btrieve = new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT");

            btrieve.Keys.Count.Should().Be(4);
            btrieve.RecordLength.Should().Be(RECORD_LENGTH);
            btrieve.PageLength.Should().Be(512);

            btrieve.Keys[0].PrimarySegment.Should().BeEquivalentTo(
                new BtrieveKeyDefinition() {
                    Number = 0,
                    Attributes = EnumKeyAttributeMask.Duplicates,
                    DataType = EnumKeyDataType.Zstring,
                    Offset = 2,
                    Length = 32,
                    Segment = false,
                });
            btrieve.Keys[1].PrimarySegment.Should().BeEquivalentTo(
                new BtrieveKeyDefinition() {
                    Number = 1,
                    Attributes = EnumKeyAttributeMask.Modifiable,
                    DataType = EnumKeyDataType.Integer,
                    Offset = 34,
                    Length = 4,
                    Segment = false,
                });
            btrieve.Keys[2].PrimarySegment.Should().BeEquivalentTo(
                new BtrieveKeyDefinition() {
                    Number = 2,
                    Attributes = EnumKeyAttributeMask.Duplicates | EnumKeyAttributeMask.Modifiable,
                    DataType = EnumKeyDataType.Zstring,
                    Offset = 38,
                    Length = 32,
                    Segment = false,
                });
            btrieve.Keys[3].PrimarySegment.Should().BeEquivalentTo(
                new BtrieveKeyDefinition() {
                    Number = 3,
                    Attributes = 0,
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
            var connectionString = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder()
            {
                Mode = Microsoft.Data.Sqlite.SqliteOpenMode.ReadWriteCreate,
                DataSource = fullPath,
            }.ToString();

            using var connection = new SQLiteConnection(connectionString);
            connection.Open();

            using (var stmt = new SQLiteCommand("SELECT sql FROM sqlite_master WHERE name = 'metadata_t';", connection))
                ((string) stmt.ExecuteScalar()).Should().Be(EXPECTED_METADATA_T_SQL);

            using (var stmt = new SQLiteCommand("SELECT sql FROM sqlite_master WHERE name = 'keys_t';", connection))
                ((string) stmt.ExecuteScalar()).Should().Be(EXPECTED_KEYS_T_SQL);

            using (var stmt = new SQLiteCommand("SELECT sql FROM sqlite_master WHERE name = 'data_t';", connection))
                ((string) stmt.ExecuteScalar()).Should().Be(EXPECTED_DATA_T_SQL);
        }

        [Fact]
        public void LoadsFileProperly()
        {
            CopyFilesToTempPath("MBBSEMU.DB");

            ServiceResolver serviceResolver = new ServiceResolver(ServiceResolver.GetTestDefaults());

            var btrieve = new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT");

            btrieve.Keys.Count.Should().Be(4);
            btrieve.RecordLength.Should().Be(RECORD_LENGTH);
            btrieve.PageLength.Should().Be(512);

            btrieve.Keys[0].PrimarySegment.Should().BeEquivalentTo(
                new BtrieveKeyDefinition() {
                    Number = 0,
                    Attributes = EnumKeyAttributeMask.Duplicates,
                    DataType = EnumKeyDataType.Zstring,
                    Offset = 2,
                    Length = 32,
                    Segment = false,
                });
            btrieve.Keys[1].PrimarySegment.Should().BeEquivalentTo(
                new BtrieveKeyDefinition() {
                    Number = 1,
                    Attributes = EnumKeyAttributeMask.Modifiable,
                    DataType = EnumKeyDataType.Integer,
                    Offset = 34,
                    Length = 4,
                    Segment = false,
                });
            btrieve.Keys[2].PrimarySegment.Should().BeEquivalentTo(
                new BtrieveKeyDefinition() {
                    Number = 2,
                    Attributes = EnumKeyAttributeMask.Duplicates | EnumKeyAttributeMask.Modifiable,
                    DataType = EnumKeyDataType.Zstring,
                    Offset = 38,
                    Length = 32,
                    Segment = false,
                });
            btrieve.Keys[3].PrimarySegment.Should().BeEquivalentTo(
                new BtrieveKeyDefinition() {
                    Number = 3,
                    Attributes = 0,
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

            ServiceResolver serviceResolver = new ServiceResolver(ServiceResolver.GetTestDefaults());
            var btrieve = new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT");

            btrieve.StepFirst().Should().BeTrue();
            btrieve.Position.Should().Be(1);
            new MBBSEmuRecord(btrieve.GetRecord()).Key1.Should().Be(3444);

            btrieve.StepNext().Should().BeTrue();
            btrieve.Position.Should().Be(2);
            new MBBSEmuRecord(btrieve.GetRecord()).Key1.Should().Be(7776);

            btrieve.StepNext().Should().BeTrue();
            btrieve.Position.Should().Be(3);
            new MBBSEmuRecord(btrieve.GetRecord()).Key1.Should().Be(1052234073);

            btrieve.StepNext().Should().BeTrue();
            btrieve.Position.Should().Be(4);
            new MBBSEmuRecord(btrieve.GetRecord()).Key1.Should().Be(-615634567);

            btrieve.StepNext().Should().BeFalse();
            btrieve.Position.Should().Be(4);
        }

        [Fact]
        public void EnumeratesInitialEntriesReverse()
        {
            CopyFilesToTempPath("MBBSEMU.DB");

            ServiceResolver serviceResolver = new ServiceResolver(ServiceResolver.GetTestDefaults());
            var btrieve = new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT");

            btrieve.StepLast().Should().BeTrue();
            btrieve.Position.Should().Be(4);
            new MBBSEmuRecord(btrieve.GetRecord()).Key1.Should().Be(-615634567);

            btrieve.StepPrevious().Should().BeTrue();
            btrieve.Position.Should().Be(3);
            new MBBSEmuRecord(btrieve.GetRecord()).Key1.Should().Be(1052234073);

            btrieve.StepPrevious().Should().BeTrue();
            btrieve.Position.Should().Be(2);
            new MBBSEmuRecord(btrieve.GetRecord()).Key1.Should().Be(7776);

            btrieve.StepPrevious().Should().BeTrue();
            btrieve.Position.Should().Be(1);
            new MBBSEmuRecord(btrieve.GetRecord()).Key1.Should().Be(3444);

            btrieve.StepPrevious().Should().BeFalse();
            btrieve.Position.Should().Be(1);
        }

        [Fact]
        public void RandomAccess()
        {
            CopyFilesToTempPath("MBBSEMU.DB");

            ServiceResolver serviceResolver = new ServiceResolver(ServiceResolver.GetTestDefaults());
            var btrieve = new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT");

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

            ServiceResolver serviceResolver = new ServiceResolver(ServiceResolver.GetTestDefaults());
            var btrieve = new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT");

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

            ServiceResolver serviceResolver = new ServiceResolver(ServiceResolver.GetTestDefaults());
            var btrieve = new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT");

            btrieve.GetRecordCount().Should().Be(4);
        }

        [Fact]
        public void RecordDeleteAll()
        {
            CopyFilesToTempPath("MBBSEMU.DB");

            ServiceResolver serviceResolver = new ServiceResolver(ServiceResolver.GetTestDefaults());
            var btrieve = new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT");

            btrieve.Position = 3;
            btrieve.DeleteAll().Should().BeTrue();

            btrieve.Position.Should().Be(0);
            btrieve.GetRecordCount().Should().Be(0);

            btrieve.DeleteAll().Should().BeFalse();
        }

        [Fact]
        public void RecordDeleteOne()
        {
            CopyFilesToTempPath("MBBSEMU.DB");

            ServiceResolver serviceResolver = new ServiceResolver(ServiceResolver.GetTestDefaults());
            var btrieve = new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT");

            btrieve.Position = 2;
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

            ServiceResolver serviceResolver = new ServiceResolver(ServiceResolver.GetTestDefaults());
            var btrieve = new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT");

            btrieve.Position = 2;
            btrieve.Delete().Should().BeTrue();

            btrieve.StepFirst().Should().BeTrue();
            btrieve.Position.Should().Be(1);
            new MBBSEmuRecord(btrieve.GetRecord()).Key1.Should().Be(3444);

            btrieve.StepNext().Should().BeTrue();
            btrieve.Position.Should().Be(3);
            new MBBSEmuRecord(btrieve.GetRecord()).Key1.Should().Be(1052234073);

            btrieve.StepNext().Should().BeTrue();
            btrieve.Position.Should().Be(4);
            new MBBSEmuRecord(btrieve.GetRecord()).Key1.Should().Be(-615634567);

            btrieve.StepNext().Should().BeFalse();
        }

        [Fact]
        public void InsertionTest()
        {
            CopyFilesToTempPath("MBBSEMU.DB");

            ServiceResolver serviceResolver = new ServiceResolver(ServiceResolver.GetTestDefaults());
            var btrieve = new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT");

            var record = new MBBSEmuRecord();
            record.Key0 = "Paladine";
            record.Key1 = 31337;
            record.Key2 = "In orbe terrarum, optimus sum";

            var insertedId = btrieve.Insert(record.Data);
            insertedId.Should().BePositive();

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

            ServiceResolver serviceResolver = new ServiceResolver(ServiceResolver.GetTestDefaults());
            var btrieve = new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT");

            var record = new MBBSEmuRecord();
            record.Key0 = "Paladine";
            record.Key1 = 31337;
            record.Key2 = "In orbe terrarum, optimus sum";
            record.Key3 = 4444;

            var insertedId = btrieve.Insert(record.Data);
            insertedId.Should().BePositive();

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

            ServiceResolver serviceResolver = new ServiceResolver(ServiceResolver.GetTestDefaults());
            var btrieve = new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT");

            var record = new MBBSEmuRecord();
            record.Key0 = "Paladine";
            record.Key1 = 31337;
            record.Key2 = "In orbe terrarum, optimus sum";

            var insertedId = btrieve.Insert(MakeSmaller(record.Data, 14));
            insertedId.Should().BePositive();

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

            ServiceResolver serviceResolver = new ServiceResolver(ServiceResolver.GetTestDefaults());
            var btrieve = new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT");

            var record = new MBBSEmuRecord();
            record.Key0 = "Paladine";
            record.Key1 = 3444; // constraint failure here
            record.Key2 = "In orbe terrarum, optimus sum";
            record.Key3 = 4444;

            var insertedId = btrieve.Insert(record.Data);
            insertedId.Should().Be(0);
        }

        [Fact]
        public void UpdateTest()
        {
            CopyFilesToTempPath("MBBSEMU.DB");

            ServiceResolver serviceResolver = new ServiceResolver(ServiceResolver.GetTestDefaults());
            var btrieve = new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT");

            var record = new MBBSEmuRecord();
            record.Key0 = "Paladine";
            record.Key1 = 31337;
            record.Key2 = "In orbe terrarum, optimus sum";

            btrieve.Update(1, record.Data).Should().BeTrue();

            record = new MBBSEmuRecord(btrieve.GetRecord(1)?.Data);
            record.Key0.Should().Be("Paladine");
            record.Key1.Should().Be(31337);
            record.Key2.Should().Be("In orbe terrarum, optimus sum");

            btrieve.GetRecordCount().Should().Be(4);
        }

        [Fact]
        public void UpdateTestSubSize()
        {
            CopyFilesToTempPath("MBBSEMU.DB");

            ServiceResolver serviceResolver = new ServiceResolver(ServiceResolver.GetTestDefaults());
            var btrieve = new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT");

            var record = new MBBSEmuRecord();
            record.Key0 = "Paladine";
            record.Key1 = 31337;
            record.Key2 = "In orbe terrarum, optimus sum";
            record.Key3 = 2;

            btrieve.Update(2, MakeSmaller(record.Data, 14)).Should().BeTrue();

            record = new MBBSEmuRecord(btrieve.GetRecord(2)?.Data);
            record.Key0.Should().Be("Paladine");
            record.Key1.Should().Be(31337);
            record.Key2.Should().Be("In orbe terrarum, opti");
            record.Key3.Should().Be(5); // regenerated

            btrieve.GetRecordCount().Should().Be(4);
        }

        [Fact]
        public void UpdateTestConstraintFailed()
        {
            CopyFilesToTempPath("MBBSEMU.DB");

            ServiceResolver serviceResolver = new ServiceResolver(ServiceResolver.GetTestDefaults());
            var btrieve = new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT");

            var record = new MBBSEmuRecord();
            record.Key0 = "Paladine";
            record.Key1 = 7776; // constraint failure here
            record.Key2 = "In orbe terrarum, optimus sum";

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

            ServiceResolver serviceResolver = new ServiceResolver(ServiceResolver.GetTestDefaults());
            var btrieve = new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT");

            var record = new MBBSEmuRecord();
            record.Key0 = "Paladine";
            record.Key1 = 31337;
            record.Key2 = "In orbe terrarum, optimus sum";

            btrieve.Update(5, record.Data).Should().BeFalse();
        }

        [Fact]
        public void SeekByKeyString()
        {
            CopyFilesToTempPath("MBBSEMU.DB");

            ServiceResolver serviceResolver = new ServiceResolver(ServiceResolver.GetTestDefaults());
            var btrieve = new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT");

            btrieve.SeekByKey(0, Encoding.ASCII.GetBytes("Sysop"), EnumBtrieveOperationCodes.GetKeyEqual, newQuery: true).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(1);

            btrieve.SeekByKey(0, Encoding.ASCII.GetBytes("Sysop"), EnumBtrieveOperationCodes.GetKeyNext, newQuery: false).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(2);

            btrieve.SeekByKey(0, Encoding.ASCII.GetBytes("Sysop"), EnumBtrieveOperationCodes.GetKeyNext, newQuery: false).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(3);

            btrieve.SeekByKey(0, Encoding.ASCII.GetBytes("Sysop"), EnumBtrieveOperationCodes.GetKeyNext, newQuery: false).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(4);

            btrieve.SeekByKey(0, Encoding.ASCII.GetBytes("Sysop"), EnumBtrieveOperationCodes.GetKeyNext, newQuery: false).Should().BeFalse();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(4);
        }

        [Fact]
        public void SeekByKeyInteger()
        {
            CopyFilesToTempPath("MBBSEMU.DB");

            ServiceResolver serviceResolver = new ServiceResolver(ServiceResolver.GetTestDefaults());
            var btrieve = new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT");

            btrieve.SeekByKey(1, BitConverter.GetBytes(1052234073), EnumBtrieveOperationCodes.GetKeyEqual, newQuery: true).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(3);
            new MBBSEmuRecord(btrieve.GetRecord(btrieve.Position)?.Data).Key1.Should().Be(1052234073);

            btrieve.SeekByKey(1, BitConverter.GetBytes(23556), EnumBtrieveOperationCodes.GetKeyNext, newQuery: false).Should().BeFalse();
        }

        [Fact]
        public void SeekByKeyNotFound()
        {
            CopyFilesToTempPath("MBBSEMU.DB");

            ServiceResolver serviceResolver = new ServiceResolver(ServiceResolver.GetTestDefaults());
            var btrieve = new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT");

            btrieve.SeekByKey(0, Encoding.ASCII.GetBytes("Sysop2"), EnumBtrieveOperationCodes.GetEqual, newQuery: true).Should().BeFalse();
        }

        [Fact]
        public void SeekByKeyFirstString()
        {
            CopyFilesToTempPath("MBBSEMU.DB");

            ServiceResolver serviceResolver = new ServiceResolver(ServiceResolver.GetTestDefaults());
            var btrieve = new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT");

            btrieve.SeekByKey(2, null, EnumBtrieveOperationCodes.GetKeyFirst, newQuery: true).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(1);
            new MBBSEmuRecord(btrieve.GetRecord(btrieve.Position)?.Data).Key2.Should().Be("3444");

            btrieve.SeekByKey(2, null, EnumBtrieveOperationCodes.GetKeyNext, newQuery: false).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(2);
            new MBBSEmuRecord(btrieve.GetRecord(btrieve.Position)?.Data).Key2.Should().Be("7776");

            btrieve.SeekByKey(2, null, EnumBtrieveOperationCodes.GetKeyNext, newQuery: false).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(3);
            new MBBSEmuRecord(btrieve.GetRecord(btrieve.Position)?.Data).Key2.Should().Be("StringValue");

            btrieve.SeekByKey(2, null, EnumBtrieveOperationCodes.GetKeyNext, newQuery: false).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(4);
            new MBBSEmuRecord(btrieve.GetRecord(btrieve.Position)?.Data).Key2.Should().Be("stringValue");

            btrieve.SeekByKey(2, null, EnumBtrieveOperationCodes.GetKeyNext, newQuery: false).Should().BeFalse();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(4);
        }

        [Fact]
        public void SeekByKeyFirstInteger()
        {
            CopyFilesToTempPath("MBBSEMU.DB");

            ServiceResolver serviceResolver = new ServiceResolver(ServiceResolver.GetTestDefaults());
            var btrieve = new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT");

            btrieve.SeekByKey(1, null, EnumBtrieveOperationCodes.GetKeyFirst, newQuery: true).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(4);
            new MBBSEmuRecord(btrieve.GetRecord(btrieve.Position)?.Data).Key1.Should().Be(-615634567);

            btrieve.SeekByKey(1, null, EnumBtrieveOperationCodes.GetKeyNext, newQuery: false).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(1);
            new MBBSEmuRecord(btrieve.GetRecord(btrieve.Position)?.Data).Key1.Should().Be(3444);

            btrieve.SeekByKey(1, null, EnumBtrieveOperationCodes.GetKeyNext, newQuery: false).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(2);
            new MBBSEmuRecord(btrieve.GetRecord(btrieve.Position)?.Data).Key1.Should().Be(7776);

            btrieve.SeekByKey(1, null, EnumBtrieveOperationCodes.GetKeyNext, newQuery: false).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(3);
            new MBBSEmuRecord(btrieve.GetRecord(btrieve.Position)?.Data).Key1.Should().Be(1052234073);

            btrieve.SeekByKey(1, null, EnumBtrieveOperationCodes.GetKeyNext, newQuery: false).Should().BeFalse();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(3);
        }

        [Fact]
        public void SeekByKeyFirstNotFound()
        {
            CopyFilesToTempPath("MBBSEMU.DB");

            ServiceResolver serviceResolver = new ServiceResolver(ServiceResolver.GetTestDefaults());
            var btrieve = new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT");

            btrieve.DeleteAll();

            btrieve.SeekByKey(0, null, EnumBtrieveOperationCodes.GetKeyFirst, newQuery: true).Should().BeFalse();
        }

        [Fact]
        public void SeekByKeyLastString()
        {
            CopyFilesToTempPath("MBBSEMU.DB");

            ServiceResolver serviceResolver = new ServiceResolver(ServiceResolver.GetTestDefaults());
            var btrieve = new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT");

            btrieve.SeekByKey(2, null, EnumBtrieveOperationCodes.GetKeyLast, newQuery: true).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(4);
            new MBBSEmuRecord(btrieve.GetRecord(btrieve.Position)?.Data).Key2.Should().Be("stringValue");

            btrieve.SeekByKey(2, null, EnumBtrieveOperationCodes.GetKeyNext, newQuery: false).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(3);
            new MBBSEmuRecord(btrieve.GetRecord(btrieve.Position)?.Data).Key2.Should().Be("StringValue");

            btrieve.SeekByKey(2, null, EnumBtrieveOperationCodes.GetKeyNext, newQuery: false).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(2);
            new MBBSEmuRecord(btrieve.GetRecord(btrieve.Position)?.Data).Key2.Should().Be("7776");

            btrieve.SeekByKey(2, null, EnumBtrieveOperationCodes.GetKeyNext, newQuery: false).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(1);
            new MBBSEmuRecord(btrieve.GetRecord(btrieve.Position)?.Data).Key2.Should().Be("3444");

            btrieve.SeekByKey(2, null, EnumBtrieveOperationCodes.GetKeyNext, newQuery: false).Should().BeFalse();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(1);
        }

        [Fact]
        public void SeekByKeyLastInteger()
        {
            CopyFilesToTempPath("MBBSEMU.DB");

            ServiceResolver serviceResolver = new ServiceResolver(ServiceResolver.GetTestDefaults());
            var btrieve = new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT");

            btrieve.SeekByKey(1, null, EnumBtrieveOperationCodes.GetKeyLast, newQuery: true).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(3);
            new MBBSEmuRecord(btrieve.GetRecord(btrieve.Position)?.Data).Key1.Should().Be(1052234073);

            btrieve.SeekByKey(1, null, EnumBtrieveOperationCodes.GetKeyNext, newQuery: false).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(2);
            new MBBSEmuRecord(btrieve.GetRecord(btrieve.Position)?.Data).Key1.Should().Be(7776);

            btrieve.SeekByKey(1, null, EnumBtrieveOperationCodes.GetKeyNext, newQuery: false).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(1);
            new MBBSEmuRecord(btrieve.GetRecord(btrieve.Position)?.Data).Key1.Should().Be(3444);

            btrieve.SeekByKey(1, null, EnumBtrieveOperationCodes.GetKeyNext, newQuery: false).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(4);
            new MBBSEmuRecord(btrieve.GetRecord(btrieve.Position)?.Data).Key1.Should().Be(-615634567);

            btrieve.SeekByKey(1, null, EnumBtrieveOperationCodes.GetKeyNext, newQuery: false).Should().BeFalse();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(4);
        }

        [Fact]
        public void SeekByKeyLastNotFound()
        {
            CopyFilesToTempPath("MBBSEMU.DB");

            ServiceResolver serviceResolver = new ServiceResolver(ServiceResolver.GetTestDefaults());
            var btrieve = new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT");

            btrieve.DeleteAll();

            btrieve.SeekByKey(0, null, EnumBtrieveOperationCodes.GetKeyLast, newQuery: true).Should().BeFalse();
        }

        [Fact]
        public void SeekByKeyGreaterString()
        {
            CopyFilesToTempPath("MBBSEMU.DB");

            ServiceResolver serviceResolver = new ServiceResolver(ServiceResolver.GetTestDefaults());
            var btrieve = new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT");

            btrieve.SeekByKey(2, Encoding.ASCII.GetBytes("7776"), EnumBtrieveOperationCodes.GetKeyGreater, newQuery: true).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(3);
            new MBBSEmuRecord(btrieve.GetRecord(btrieve.Position)?.Data).Key2.Should().Be("StringValue");

            btrieve.SeekByKey(1, null, EnumBtrieveOperationCodes.GetKeyNext, newQuery: false).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(4);
            new MBBSEmuRecord(btrieve.GetRecord(btrieve.Position)?.Data).Key2.Should().Be("stringValue");

            btrieve.SeekByKey(1, null, EnumBtrieveOperationCodes.GetKeyNext, newQuery: false).Should().BeFalse();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(4);
        }

        [Fact]
        public void SeekByKeyGreaterInteger()
        {
            CopyFilesToTempPath("MBBSEMU.DB");

            ServiceResolver serviceResolver = new ServiceResolver(ServiceResolver.GetTestDefaults());
            var btrieve = new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT");

            btrieve.SeekByKey(1, BitConverter.GetBytes(3444), EnumBtrieveOperationCodes.GetKeyGreater, newQuery: true).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(2);
            new MBBSEmuRecord(btrieve.GetRecord(btrieve.Position)?.Data).Key1.Should().Be(7776);

            btrieve.SeekByKey(1, null, EnumBtrieveOperationCodes.GetKeyNext, newQuery: false).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(3);
            new MBBSEmuRecord(btrieve.GetRecord(btrieve.Position)?.Data).Key1.Should().Be(1052234073);

            btrieve.SeekByKey(1, null, EnumBtrieveOperationCodes.GetKeyNext, newQuery: false).Should().BeFalse();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(3);
        }

        [Fact]
        public void SeekByKeyGreaterNotFound()
        {
            CopyFilesToTempPath("MBBSEMU.DB");

            ServiceResolver serviceResolver = new ServiceResolver(ServiceResolver.GetTestDefaults());
            var btrieve = new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT");

            btrieve.DeleteAll();

            btrieve.SeekByKey(1, BitConverter.GetBytes(2_000_000_000), EnumBtrieveOperationCodes.GetKeyGreater, newQuery: true).Should().BeFalse();
        }

        [Fact]
        public void SeekByKeyGreaterOrEqualString()
        {
            CopyFilesToTempPath("MBBSEMU.DB");

            ServiceResolver serviceResolver = new ServiceResolver(ServiceResolver.GetTestDefaults());
            var btrieve = new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT");

            btrieve.SeekByKey(2, Encoding.ASCII.GetBytes("7776"), EnumBtrieveOperationCodes.GetGreaterOrEqual, newQuery: true).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(2);
            new MBBSEmuRecord(btrieve.GetRecord(btrieve.Position)?.Data).Key2.Should().Be("7776");

            btrieve.SeekByKey(1, null, EnumBtrieveOperationCodes.GetKeyNext, newQuery: false).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(3);
            new MBBSEmuRecord(btrieve.GetRecord(btrieve.Position)?.Data).Key2.Should().Be("StringValue");

            btrieve.SeekByKey(1, null, EnumBtrieveOperationCodes.GetKeyNext, newQuery: false).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(4);
            new MBBSEmuRecord(btrieve.GetRecord(btrieve.Position)?.Data).Key2.Should().Be("stringValue");

            btrieve.SeekByKey(1, null, EnumBtrieveOperationCodes.GetKeyNext, newQuery: false).Should().BeFalse();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(4);
        }

        [Fact]
        public void SeekByKeyGreaterOrEqualInteger()
        {
            CopyFilesToTempPath("MBBSEMU.DB");

            ServiceResolver serviceResolver = new ServiceResolver(ServiceResolver.GetTestDefaults());
            var btrieve = new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT");

            btrieve.SeekByKey(1, BitConverter.GetBytes(3444), EnumBtrieveOperationCodes.GetGreaterOrEqual, newQuery: true).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(1);
            new MBBSEmuRecord(btrieve.GetRecord(btrieve.Position)?.Data).Key1.Should().Be(3444);

            btrieve.SeekByKey(1, null, EnumBtrieveOperationCodes.GetKeyNext, newQuery: false).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(2);
            new MBBSEmuRecord(btrieve.GetRecord(btrieve.Position)?.Data).Key1.Should().Be(7776);

            btrieve.SeekByKey(1, null, EnumBtrieveOperationCodes.GetKeyNext, newQuery: false).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(3);
            new MBBSEmuRecord(btrieve.GetRecord(btrieve.Position)?.Data).Key1.Should().Be(1052234073);

            btrieve.SeekByKey(1, null, EnumBtrieveOperationCodes.GetKeyNext, newQuery: false).Should().BeFalse();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(3);
        }

        [Fact]
        public void SeekByKeyGreaterOrEqualFound()
        {
            CopyFilesToTempPath("MBBSEMU.DB");

            ServiceResolver serviceResolver = new ServiceResolver(ServiceResolver.GetTestDefaults());
            var btrieve = new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT");

            btrieve.DeleteAll();

            btrieve.SeekByKey(1, BitConverter.GetBytes(2_000_000_000), EnumBtrieveOperationCodes.GetGreaterOrEqual, newQuery: true).Should().BeFalse();
        }

        [Fact]
        public void SeekByKeyLessString()
        {
            CopyFilesToTempPath("MBBSEMU.DB");

            ServiceResolver serviceResolver = new ServiceResolver(ServiceResolver.GetTestDefaults());
            var btrieve = new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT");

            btrieve.SeekByKey(2, Encoding.ASCII.GetBytes("7776"), EnumBtrieveOperationCodes.GetKeyLess, newQuery: true).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(1);
            new MBBSEmuRecord(btrieve.GetRecord(btrieve.Position)?.Data).Key2.Should().Be("3444");

            btrieve.SeekByKey(1, null, EnumBtrieveOperationCodes.GetKeyNext, newQuery: false).Should().BeFalse();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(1);
        }

        [Fact]
        public void SeekByKeyLessInteger()
        {
            CopyFilesToTempPath("MBBSEMU.DB");

            ServiceResolver serviceResolver = new ServiceResolver(ServiceResolver.GetTestDefaults());
            var btrieve = new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT");

            btrieve.SeekByKey(1, BitConverter.GetBytes(7776), EnumBtrieveOperationCodes.GetKeyLess, newQuery: true).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(1);
            new MBBSEmuRecord(btrieve.GetRecord(btrieve.Position)?.Data).Key1.Should().Be(3444);

            btrieve.SeekByKey(1, null, EnumBtrieveOperationCodes.GetKeyNext, newQuery: false).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(4);
            new MBBSEmuRecord(btrieve.GetRecord(btrieve.Position)?.Data).Key1.Should().Be(-615634567);

            btrieve.SeekByKey(1, null, EnumBtrieveOperationCodes.GetKeyNext, newQuery: false).Should().BeFalse();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(4);
        }

        [Fact]
        public void SeekByKeyLessNotFound()
        {
            CopyFilesToTempPath("MBBSEMU.DB");

            ServiceResolver serviceResolver = new ServiceResolver(ServiceResolver.GetTestDefaults());
            var btrieve = new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT");

            btrieve.DeleteAll();

            btrieve.SeekByKey(1, BitConverter.GetBytes(-2_000_000_000), EnumBtrieveOperationCodes.GetKeyLess, newQuery: true).Should().BeFalse();
        }

        [Fact]
        public void SeekByKeyLessOrEqualString()
        {
            CopyFilesToTempPath("MBBSEMU.DB");

            ServiceResolver serviceResolver = new ServiceResolver(ServiceResolver.GetTestDefaults());
            var btrieve = new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT");

            btrieve.SeekByKey(2, Encoding.ASCII.GetBytes("7776"), EnumBtrieveOperationCodes.GetLessOrEqual, newQuery: true).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(2);
            new MBBSEmuRecord(btrieve.GetRecord(btrieve.Position)?.Data).Key2.Should().Be("7776");

            btrieve.SeekByKey(1, null, EnumBtrieveOperationCodes.GetKeyNext, newQuery: false).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(1);
            new MBBSEmuRecord(btrieve.GetRecord(btrieve.Position)?.Data).Key2.Should().Be("3444");

            btrieve.SeekByKey(1, null, EnumBtrieveOperationCodes.GetKeyNext, newQuery: false).Should().BeFalse();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(1);
        }

        [Fact]
        public void SeekByKeyLessOrEqualInteger()
        {
            CopyFilesToTempPath("MBBSEMU.DB");

            ServiceResolver serviceResolver = new ServiceResolver(ServiceResolver.GetTestDefaults());
            var btrieve = new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT");

            btrieve.SeekByKey(1, BitConverter.GetBytes(7776), EnumBtrieveOperationCodes.GetLessOrEqual, newQuery: true).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(2);
            new MBBSEmuRecord(btrieve.GetRecord(btrieve.Position)?.Data).Key1.Should().Be(7776);

            btrieve.SeekByKey(1, null, EnumBtrieveOperationCodes.GetKeyNext, newQuery: false).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(1);
            new MBBSEmuRecord(btrieve.GetRecord(btrieve.Position)?.Data).Key1.Should().Be(3444);

            btrieve.SeekByKey(1, null, EnumBtrieveOperationCodes.GetKeyNext, newQuery: false).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(4);
            new MBBSEmuRecord(btrieve.GetRecord(btrieve.Position)?.Data).Key1.Should().Be(-615634567);

            btrieve.SeekByKey(1, null, EnumBtrieveOperationCodes.GetKeyNext, newQuery: false).Should().BeFalse();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(4);
        }

        [Fact]
        public void SeekByKeyLessOrEqualFound()
        {
            CopyFilesToTempPath("MBBSEMU.DB");

            ServiceResolver serviceResolver = new ServiceResolver(ServiceResolver.GetTestDefaults());
            var btrieve = new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT");

            btrieve.DeleteAll();

            btrieve.SeekByKey(1, BitConverter.GetBytes(-2_000_000_000), EnumBtrieveOperationCodes.GetLessOrEqual, newQuery: true).Should().BeFalse();
        }

        /// <summary>Creates a copy of data shrunk by cutOff bytes at the end</summary>
        private static byte[] MakeSmaller(byte[] data, int cutOff)
        {
            var ret =  new byte[data.Length - cutOff];
            Array.Copy(data, 0, ret, 0, ret.Length);
            return ret;
        }
    }
}
