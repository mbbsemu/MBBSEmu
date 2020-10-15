using FluentAssertions;
using FluentAssertions.Extensions;
using MBBSEmu.Btrieve;
using MBBSEmu.Btrieve.Enums;
using MBBSEmu.DependencyInjection;
using MBBSEmu.IO;
using MBBSEmu.Resources;
using System.Data.SQLite;
using System.IO;
using System;
using Xunit;

namespace MBBSEmu.Tests.Btrieve
{
    public class BtrieveFileProcessor_Tests : TestBase, IDisposable
    {
        private const string EXPECTED_METADATA_T_SQL = "CREATE TABLE metadata_t(record_length INTEGER NOT NULL, physical_record_length INTEGER NOT NULL, page_length INTEGER NOT NULL)";
        private const string EXPECTED_KEYS_T_SQL = "CREATE TABLE keys_t(id INTEGER PRIMARY KEY, attributes INTEGER NOT NULL, data_type INTEGER NOT NULL, offset INTEGER NOT NULL, length INTEGER NOT NULL)";
        private const string EXPECTED_DATA_T_SQL = "CREATE TABLE data_t(id INTEGER PRIMARY KEY, data BLOB NOT NULL, key0 TEXT NOT NULL, key1 INTEGER NOT NULL, key2 TEXT NOT NULL)";

        private static readonly Random RANDOM = new Random();

        private readonly string[] _btrieveFiles = {"MBBSEMU.DAT"};


        protected readonly string _modulePath = Path.Join(Path.GetTempPath(), $"mbbsemu{RANDOM.Next()}");

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
            btrieve.RecordLength.Should().Be(70);
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
            btrieve.RecordLength.Should().Be(70);
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
            GetKey1(btrieve, btrieve.GetRecord()).Should().Be(23923);

            btrieve.StepNext().Should().BeTrue();
            btrieve.Position.Should().Be(2);
            GetKey1(btrieve, btrieve.GetRecord()).Should().Be(0);

            btrieve.StepNext().Should().BeTrue();
            btrieve.Position.Should().Be(3);
            GetKey1(btrieve, btrieve.GetRecord()).Should().Be(23556);

            btrieve.StepNext().Should().BeTrue();
            btrieve.Position.Should().Be(4);
            GetKey1(btrieve, btrieve.GetRecord()).Should().Be(3774400);

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
            GetKey1(btrieve, btrieve.GetRecord()).Should().Be(3774400);

            btrieve.StepPrevious().Should().BeTrue();
            btrieve.Position.Should().Be(3);
            GetKey1(btrieve, btrieve.GetRecord()).Should().Be(23556);

            btrieve.StepPrevious().Should().BeTrue();
            btrieve.Position.Should().Be(2);
            GetKey1(btrieve, btrieve.GetRecord()).Should().Be(0);

            btrieve.StepPrevious().Should().BeTrue();
            btrieve.Position.Should().Be(1);
            GetKey1(btrieve, btrieve.GetRecord()).Should().Be(23923);

            btrieve.StepPrevious().Should().BeFalse();
            btrieve.Position.Should().Be(1);
        }

        [Fact]
        public void RandomAccess()
        {
            CopyFilesToTempPath("MBBSEMU.DB");

            ServiceResolver serviceResolver = new ServiceResolver(ServiceResolver.GetTestDefaults());
            var btrieve = new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT");

            GetKey1(btrieve, btrieve.GetRecord(4)?.Data).Should().Be(3774400);
            GetKey1(btrieve, btrieve.GetRecord(3)?.Data).Should().Be(23556);
            GetKey1(btrieve, btrieve.GetRecord(2)?.Data).Should().Be(0);
            GetKey1(btrieve, btrieve.GetRecord(1)?.Data).Should().Be(23923);
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
            GetKey1(btrieve, btrieve.GetRecord()).Should().Be(23923);

            btrieve.StepNext().Should().BeTrue();
            btrieve.Position.Should().Be(3);
            GetKey1(btrieve, btrieve.GetRecord()).Should().Be(23556);

            btrieve.StepNext().Should().BeTrue();
            btrieve.Position.Should().Be(4);
            GetKey1(btrieve, btrieve.GetRecord()).Should().Be(3774400);

            btrieve.StepNext().Should().BeFalse();
        }

        private static int GetKey1(BtrieveFileProcessor btrieve, byte[] data)
        {
            return BitConverter.ToInt32(data.AsSpan().Slice(btrieve.Keys[1].Offset, btrieve.Keys[1].Length));
        }
    }
}
