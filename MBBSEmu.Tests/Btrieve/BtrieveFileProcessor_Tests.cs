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
        const int RECORD_LENGTH = 70;

        private const string EXPECTED_METADATA_T_SQL = "CREATE TABLE metadata_t(record_length INTEGER NOT NULL, physical_record_length INTEGER NOT NULL, page_length INTEGER NOT NULL)";
        private const string EXPECTED_KEYS_T_SQL = "CREATE TABLE keys_t(id INTEGER PRIMARY KEY, attributes INTEGER NOT NULL, data_type INTEGER NOT NULL, offset INTEGER NOT NULL, length INTEGER NOT NULL)";
        private const string EXPECTED_DATA_T_SQL = "CREATE TABLE data_t(id INTEGER PRIMARY KEY, data BLOB NOT NULL, key0 TEXT NOT NULL, key1 INTEGER NOT NULL, key2 TEXT NOT NULL)";

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

            btrieve.Keys.Count.Should().Be(3);
            btrieve.RecordLength.Should().Be(RECORD_LENGTH);
            btrieve.PageLength.Should().Be(512);

            btrieve.Keys[0].Should().BeEquivalentTo(
                new BtrieveKeyDefinition() {
                    Number = 0,
                    Attributes = EnumKeyAttributeMask.Duplicates,
                    DataType = EnumKeyDataType.Zstring,
                    Offset = 2,
                    Length = 32,
                    Segment = false,
                });
            btrieve.Keys[1].Should().BeEquivalentTo(
                new BtrieveKeyDefinition() {
                    Number = 1,
                    Attributes = EnumKeyAttributeMask.Modifiable,
                    DataType = EnumKeyDataType.Integer,
                    Offset = 34,
                    Length = 4,
                    Segment = false,
                });
            btrieve.Keys[2].Should().BeEquivalentTo(
                new BtrieveKeyDefinition() {
                    Number = 2,
                    Attributes = EnumKeyAttributeMask.Duplicates | EnumKeyAttributeMask.Modifiable,
                    DataType = EnumKeyDataType.Zstring,
                    Offset = 38,
                    Length = 32,
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

            btrieve.Keys.Count.Should().Be(3);
            btrieve.RecordLength.Should().Be(RECORD_LENGTH);
            btrieve.PageLength.Should().Be(512);

            btrieve.Keys[0].Should().BeEquivalentTo(
                new BtrieveKeyDefinition() {
                    Number = 0,
                    Attributes = EnumKeyAttributeMask.Duplicates,
                    DataType = EnumKeyDataType.Zstring,
                    Offset = 2,
                    Length = 32,
                    Segment = false,
                });
            btrieve.Keys[1].Should().BeEquivalentTo(
                new BtrieveKeyDefinition() {
                    Number = 1,
                    Attributes = EnumKeyAttributeMask.Modifiable,
                    DataType = EnumKeyDataType.Integer,
                    Offset = 34,
                    Length = 4,
                    Segment = false,
                });
            btrieve.Keys[2].Should().BeEquivalentTo(
                new BtrieveKeyDefinition() {
                    Number = 2,
                    Attributes = EnumKeyAttributeMask.Duplicates | EnumKeyAttributeMask.Modifiable,
                    DataType = EnumKeyDataType.Zstring,
                    Offset = 38,
                    Length = 32,
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
            new MBBSEmuRecord(btrieve.GetRecord()).Key1.Should().Be(23923);

            btrieve.StepNext().Should().BeTrue();
            btrieve.Position.Should().Be(2);
            new MBBSEmuRecord(btrieve.GetRecord()).Key1.Should().Be(0);

            btrieve.StepNext().Should().BeTrue();
            btrieve.Position.Should().Be(3);
            new MBBSEmuRecord(btrieve.GetRecord()).Key1.Should().Be(23556);

            btrieve.StepNext().Should().BeTrue();
            btrieve.Position.Should().Be(4);
            new MBBSEmuRecord(btrieve.GetRecord()).Key1.Should().Be(3774400);

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
            new MBBSEmuRecord(btrieve.GetRecord()).Key1.Should().Be(3774400);

            btrieve.StepPrevious().Should().BeTrue();
            btrieve.Position.Should().Be(3);
            new MBBSEmuRecord(btrieve.GetRecord()).Key1.Should().Be(23556);

            btrieve.StepPrevious().Should().BeTrue();
            btrieve.Position.Should().Be(2);
            new MBBSEmuRecord(btrieve.GetRecord()).Key1.Should().Be(0);

            btrieve.StepPrevious().Should().BeTrue();
            btrieve.Position.Should().Be(1);
            new MBBSEmuRecord(btrieve.GetRecord()).Key1.Should().Be(23923);

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
            record.Key1.Should().Be(3774400);
            record.Key2.Should().Be("hahah");

            new MBBSEmuRecord(btrieve.GetRecord(3)?.Data).Key1.Should().Be(23556);
            new MBBSEmuRecord(btrieve.GetRecord(2)?.Data).Key1.Should().Be(0);
            new MBBSEmuRecord(btrieve.GetRecord(1)?.Data).Key1.Should().Be(23923);

            new MBBSEmuRecord(btrieve.GetRecord(2)?.Data).Key1.Should().Be(0);
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
            new MBBSEmuRecord(btrieve.GetRecord()).Key1.Should().Be(23923);

            btrieve.StepNext().Should().BeTrue();
            btrieve.Position.Should().Be(3);
            new MBBSEmuRecord(btrieve.GetRecord()).Key1.Should().Be(23556);

            btrieve.StepNext().Should().BeTrue();
            btrieve.Position.Should().Be(4);
            new MBBSEmuRecord(btrieve.GetRecord()).Key1.Should().Be(3774400);

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

            var insertedId = btrieve.Insert(MakeSmaller(record.Data, 10));
            insertedId.Should().BePositive();

            record = new MBBSEmuRecord(btrieve.GetRecord(insertedId)?.Data);
            record.Key0.Should().Be("Paladine");
            record.Key1.Should().Be(31337);
            record.Key2.Should().Be("In orbe terrarum, opti"); // cut off

            btrieve.GetRecordCount().Should().Be(5);
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

            btrieve.Update(2, MakeSmaller(record.Data, 10)).Should().BeTrue();

            record = new MBBSEmuRecord(btrieve.GetRecord(2)?.Data);
            record.Key0.Should().Be("Paladine");
            record.Key1.Should().Be(31337);
            record.Key2.Should().Be("In orbe terrarum, opti");

            btrieve.GetRecordCount().Should().Be(4);
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

            btrieve.SeekByKey(1, BitConverter.GetBytes(23556), EnumBtrieveOperationCodes.GetKeyEqual, newQuery: true).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(3);
            new MBBSEmuRecord(btrieve.GetRecord(btrieve.Position)?.Data).Key1.Should().Be(23556);

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
            new MBBSEmuRecord(btrieve.GetRecord(btrieve.Position)?.Data).Key2.Should().BeEmpty();

            btrieve.SeekByKey(2, null, EnumBtrieveOperationCodes.GetKeyNext, newQuery: false).Should().BeFalse();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(1);
        }

        [Fact]
        public void SeekByKeyFirstInteger()
        {
            CopyFilesToTempPath("MBBSEMU.DB");

            ServiceResolver serviceResolver = new ServiceResolver(ServiceResolver.GetTestDefaults());
            var btrieve = new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT");

            btrieve.SeekByKey(1, null, EnumBtrieveOperationCodes.GetKeyFirst, newQuery: true).Should().BeTrue();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(2);
            new MBBSEmuRecord(btrieve.GetRecord(btrieve.Position)?.Data).Key1.Should().Be(0);

            btrieve.SeekByKey(1, null, EnumBtrieveOperationCodes.GetKeyNext, newQuery: false).Should().BeFalse();
            btrieve.GetRecord(btrieve.Position)?.Offset.Should().Be(2);
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

        /// <summary>Creates a copy of data shrunk by cutOff bytes at the end</summary>
        private static byte[] MakeSmaller(byte[] data, int cutOff)
        {
            var ret =  new byte[data.Length - cutOff];
            Array.Copy(data, 0, ret, 0, ret.Length);
            return ret;
        }
    }
}
