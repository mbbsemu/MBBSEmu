using FluentAssertions;
using MBBSEmu.Btrieve;
using MBBSEmu.Btrieve.Enums;
using MBBSEmu.Database.Session;
using MBBSEmu.DependencyInjection;
using MBBSEmu.IO;
using MBBSEmu.Logging;
using MBBSEmu.Resources;
using MBBSEmu.Testing;
using System;
using System.IO;
using Xunit;

namespace MBBSEmu.Tests.Btrieve
{
    /// <summary>
    ///     Tests for BtrieveFileProcessor, the wrapper around wbtrv32.dll. These exercise the real
    ///     native library (via WBTRV32_PATH), both for files opened directly from disk and for
    ///     in-memory databases created from a parsed BtrieveFile.
    ///
    ///     All tests here share the same known-good MBBSEMU.DAT test asset also used by the
    ///     Int7Bh and BtrieveRuntime tests: 4 records, 4 keys (Zstring/Integer/Zstring/AutoInc),
    ///     74-byte records.
    /// </summary>
    [Collection("Non-Parallel")]
    public class BtrieveFileProcessor_Tests : TestBase, IDisposable
    {
        private readonly string _modulePath;
        private readonly ServiceResolver _serviceResolver;
        private readonly IFileUtility _fileUtility;
        private readonly IMessageLogger _logger;

        public BtrieveFileProcessor_Tests()
        {
            _modulePath = GetModulePath();
            Directory.CreateDirectory(_modulePath);

            _serviceResolver = new ServiceResolver(SessionBuilder.ForTest($"BtrieveFileProcessor_Tests_{RANDOM.Next()}"));
            _fileUtility = _serviceResolver.GetService<IFileUtility>();
            _logger = _serviceResolver.GetService<LogFactory>().GetLogger<MessageLogger>();

            var resourceManager = ResourceManager.GetTestResourceManager();
            File.WriteAllBytes(Path.Combine(_modulePath, "MBBSEMU.DAT"),
                resourceManager.GetResource("MBBSEmu.Tests.Assets.MBBSEMU.DAT").ToArray());
        }

        public void Dispose()
        {
            Directory.Delete(_modulePath, recursive: true);
        }

        private BtrieveFileProcessor OpenFromDisk() => new(_fileUtility, _modulePath, "MBBSEMU.DAT", cacheSize: 8);

        [Fact]
        public void Constructor_OpensExistingDatabase_PopulatesMetadata()
        {
            using var processor = OpenFromDisk();

            processor.RecordLength.Should().Be(MBBSEmuRecordStruct.RECORD_LENGTH);
            processor.VariableLengthRecords.Should().BeFalse();
            processor.Keys.Should().HaveCount(4);
            processor.GetRecordCount().Should().Be(4);
        }

        [Fact]
        public void Constructor_FileDoesNotExist_Throws()
        {
            Assert.Throws<Exception>(() => new BtrieveFileProcessor(_fileUtility, _modulePath, "NOEXIST.DAT", cacheSize: 8));
        }

        [Fact]
        public void GetRecordCount_MatchesKnownRecordCount()
        {
            using var processor = OpenFromDisk();

            processor.GetRecordCount().Should().Be(4);
        }

        [Fact]
        public void PerformOperation_AcquireEqual_FindsRecordByKey()
        {
            using var processor = OpenFromDisk();

            var found = processor.PerformOperation(2, System.Text.Encoding.ASCII.GetBytes("StringValue"),
                EnumBtrieveOperationCodes.AcquireEqual);

            found.Should().BeTrue();

            var record = new MBBSEmuRecordStruct(processor.GetRecord());
            record.Key0.Should().Be("Sysop");
            record.Key1.Should().Be(1052234073);
            record.Key2.Should().Be("StringValue");
        }

        [Fact]
        public void PerformOperation_AcquireEqual_KeyNotFound_ReturnsFalse()
        {
            using var processor = OpenFromDisk();

            var found = processor.PerformOperation(2, System.Text.Encoding.ASCII.GetBytes("DoesNotExist"),
                EnumBtrieveOperationCodes.AcquireEqual);

            found.Should().BeFalse();
        }

        [Fact]
        public void PerformOperation_StepFirstThenNext_WalksPhysicalOrder()
        {
            using var processor = OpenFromDisk();

            processor.PerformOperation(-1, ReadOnlySpan<byte>.Empty, EnumBtrieveOperationCodes.StepFirst).Should().BeTrue();
            new MBBSEmuRecordStruct(processor.GetRecord()).Key1.Should().Be(3444);

            processor.PerformOperation(-1, ReadOnlySpan<byte>.Empty, EnumBtrieveOperationCodes.StepNext).Should().BeTrue();
            new MBBSEmuRecordStruct(processor.GetRecord()).Key1.Should().Be(7776);
        }

        [Fact]
        public void GetRecord_ByOffset_ReturnsSameRecordAsCurrentPosition()
        {
            using var processor = OpenFromDisk();

            processor.PerformOperation(-1, ReadOnlySpan<byte>.Empty, EnumBtrieveOperationCodes.StepFirst).Should().BeTrue();
            var offset = processor.Position;

            var record = processor.GetRecord(offset);

            record.Should().NotBeNull();
            new MBBSEmuRecordStruct(record.Data).Key1.Should().Be(3444);
        }

        [Fact]
        public void Insert_NewRecord_IncreasesRecordCountAndAssignsAutoIncKey()
        {
            using var processor = OpenFromDisk();

            var record = new MBBSEmuRecordStruct { Key0 = "Paladine", Key1 = 31337, Key2 = "In orbe terrarum, optimus sum" };

            var position = processor.Insert(record.Data);

            position.Should().NotBe(0);
            processor.GetRecordCount().Should().Be(5);

            // Insert() calls wbtrv32.dll with keyNumber -1, so it doesn't reposition the cursor
            // to the new record -- re-find it by its own (unique in this dataset) key0 instead
            // of trusting the returned position.
            //
            // AutoInc key (key3) should have been assigned by the database, not left at the
            // placeholder 0 the record was inserted with -- this is a regression test for the
            // wbtrv32.dll insertRecord bug where the auto-incremented value never made it into
            // the stored row.
            processor.PerformOperation(0, System.Text.Encoding.ASCII.GetBytes("Paladine"),
                EnumBtrieveOperationCodes.AcquireEqual).Should().BeTrue();
            new MBBSEmuRecordStruct(processor.GetRecord()).Key3.Should().Be(5);
        }

        [Fact]
        public void Insert_DuplicateUniqueKey_ReturnsZero()
        {
            using var processor = OpenFromDisk();

            // key0 ("Sysop") allows duplicates, but key1 does not -- reuse an existing key1 value.
            var record = new MBBSEmuRecordStruct { Key0 = "Someone", Key1 = 3444, Key2 = "duplicate key1" };

            processor.Insert(record.Data).Should().Be(0);
            processor.GetRecordCount().Should().Be(4);
        }

        [Fact]
        public void Update_ExistingRecord_PersistsChange()
        {
            using var processor = OpenFromDisk();

            processor.PerformOperation(2, System.Text.Encoding.ASCII.GetBytes("StringValue"),
                EnumBtrieveOperationCodes.AcquireEqual).Should().BeTrue();

            var record = new MBBSEmuRecordStruct(processor.GetRecord());
            record.Key1 = 99999;

            processor.Update(record.Data).Should().Be(BtrieveError.Success);

            var updated = new MBBSEmuRecordStruct(processor.GetRecord());
            updated.Key1.Should().Be(99999);
        }

        [Fact]
        public void Delete_CurrentRecord_DecreasesRecordCount()
        {
            using var processor = OpenFromDisk();

            processor.PerformOperation(-1, ReadOnlySpan<byte>.Empty, EnumBtrieveOperationCodes.StepFirst).Should().BeTrue();

            processor.Delete().Should().BeTrue();

            processor.GetRecordCount().Should().Be(3);
        }

        [Fact]
        public void DeleteAll_EmptiesDatabase()
        {
            using var processor = OpenFromDisk();

            processor.DeleteAll().Should().BeTrue();

            processor.GetRecordCount().Should().Be(0);
        }

        [Fact]
        public void Constructor_FromBtrieveFile_CreatesQueryableInMemoryDatabase()
        {
            var btrieveFile = new BtrieveFile();
            btrieveFile.LoadFile(_logger, Path.Combine(_modulePath, "MBBSEMU.DAT"));

            using var processor = new BtrieveFileProcessor(btrieveFile);

            processor.Keys.Should().HaveCount(4);
            processor.GetRecordCount().Should().Be(4);

            processor.PerformOperation(2, System.Text.Encoding.ASCII.GetBytes("StringValue"),
                EnumBtrieveOperationCodes.AcquireEqual).Should().BeTrue();
            new MBBSEmuRecordStruct(processor.GetRecord()).Key1.Should().Be(1052234073);
        }

        [Fact]
        public void Constructor_FromBtrieveFile_WithACSKey_Succeeds()
        {
            var btrieveFile = new BtrieveFile();
            btrieveFile.LoadFile(_logger, Path.Combine(_modulePath, "MBBSEMU.DAT"));

            // give key0 (a Zstring key) an ACS requirement to exercise the ACS-table-writing path
            // in BtrieveFileProcessor(BtrieveFile) -- an identity mapping so record data round-trips
            // unchanged. Regression test: wbtrv32.dll's Create rejects the whole database with
            // InvalidACS if a key claims NumberedACS but the ACS table isn't actually written.
            var acs = new byte[256];
            for (var i = 0; i < 256; i++)
                acs[i] = (byte)i;

            btrieveFile.ACS = acs;
            btrieveFile.ACSName = "TESTACS";
            var key0Segment = btrieveFile.Keys[0].PrimarySegment;
            key0Segment.Attributes |= EnumKeyAttributeMask.NumberedACS;
            key0Segment.ACS = acs;

            using var processor = new BtrieveFileProcessor(btrieveFile);

            processor.Keys[0].RequiresACS.Should().BeTrue();
            processor.GetRecordCount().Should().Be(4);

            processor.PerformOperation(0, System.Text.Encoding.ASCII.GetBytes("Sysop"),
                EnumBtrieveOperationCodes.AcquireEqual).Should().BeTrue();
        }
    }
}
