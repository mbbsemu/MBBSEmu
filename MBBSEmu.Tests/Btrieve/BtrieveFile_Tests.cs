using FluentAssertions;
using MBBSEmu.Btrieve;
using MBBSEmu.Btrieve.Enums;
using MBBSEmu.DependencyInjection;
using MBBSEmu.Logging;
using MBBSEmu.Resources;
using MBBSEmu.Testing;
using System;
using System.IO;
using Xunit;

namespace MBBSEmu.Tests.Btrieve
{
    /// <summary>
    ///     Tests for BtrieveFile, the legacy v5 .DAT header/key/record parser.
    ///
    ///     These are pure byte-parsing tests -- no native wbtrv32.dll involved -- either against
    ///     the real MBBSEMU.DAT test asset (a known-good v5 database also used by the Int7Bh and
    ///     BtrieveRuntime tests) or hand-built minimal headers for ValidateDatabase's failure paths.
    /// </summary>
    public class BtrieveFile_Tests : TestBase, IDisposable
    {
        private readonly string _modulePath;
        private readonly IMessageLogger _logger;

        public BtrieveFile_Tests()
        {
            _modulePath = GetModulePath();
            Directory.CreateDirectory(_modulePath);

            _logger = new ServiceResolver().GetService<LogFactory>().GetLogger<MessageLogger>();
        }

        public void Dispose()
        {
            Directory.Delete(_modulePath, recursive: true);
        }

        private string WriteAsset(string resourceFileName)
        {
            var resourceManager = ResourceManager.GetTestResourceManager();
            var path = Path.Combine(_modulePath, resourceFileName);
            File.WriteAllBytes(path, resourceManager.GetResource($"MBBSEmu.Tests.Assets.{resourceFileName}").ToArray());
            return path;
        }

        private string WriteRawFile(byte[] data, string fileName = "TEST.DAT")
        {
            var path = Path.Combine(_modulePath, fileName);
            File.WriteAllBytes(path, data);
            return path;
        }

        [Fact]
        public void LoadFile_RealDatabase_ParsesHeader()
        {
            var path = WriteAsset("MBBSEMU.DAT");

            var btrieveFile = new BtrieveFile();
            btrieveFile.LoadFile(_logger, path);

            btrieveFile.RecordLength.Should().Be(MBBSEmuRecordStruct.RECORD_LENGTH);
            btrieveFile.PageLength.Should().Be(512);
            btrieveFile.RecordCount.Should().Be(4);
            btrieveFile.VariableLengthRecords.Should().BeFalse();
        }

        [Fact]
        public void LoadFile_RealDatabase_ParsesKeyDefinitions()
        {
            var path = WriteAsset("MBBSEMU.DAT");

            var btrieveFile = new BtrieveFile();
            btrieveFile.LoadFile(_logger, path);

            btrieveFile.Keys.Should().HaveCount(4);

            var key0 = btrieveFile.Keys[0].PrimarySegment;
            key0.Offset.Should().Be(2);
            key0.Length.Should().Be(32);
            key0.DataType.Should().Be(EnumKeyDataType.Zstring);
            key0.Attributes.Should().Be(EnumKeyAttributeMask.Duplicates | EnumKeyAttributeMask.UseExtendedDataType);

            var key1 = btrieveFile.Keys[1].PrimarySegment;
            key1.Offset.Should().Be(34);
            key1.Length.Should().Be(4);
            key1.DataType.Should().Be(EnumKeyDataType.Integer);
            key1.Attributes.Should().Be(EnumKeyAttributeMask.Modifiable | EnumKeyAttributeMask.UseExtendedDataType);

            var key2 = btrieveFile.Keys[2].PrimarySegment;
            key2.Offset.Should().Be(38);
            key2.Length.Should().Be(32);
            key2.DataType.Should().Be(EnumKeyDataType.Zstring);
            key2.Attributes.Should().Be(EnumKeyAttributeMask.Duplicates | EnumKeyAttributeMask.Modifiable | EnumKeyAttributeMask.UseExtendedDataType);

            var key3 = btrieveFile.Keys[3].PrimarySegment;
            key3.Offset.Should().Be(70);
            key3.Length.Should().Be(4);
            key3.DataType.Should().Be(EnumKeyDataType.AutoInc);
            key3.Attributes.Should().Be(EnumKeyAttributeMask.UseExtendedDataType);
        }

        [Fact]
        public void LoadFile_RealDatabase_ParsesRecords()
        {
            var path = WriteAsset("MBBSEMU.DAT");

            var btrieveFile = new BtrieveFile();
            btrieveFile.LoadFile(_logger, path);

            btrieveFile.Records.Should().HaveCount(4);

            var record = new MBBSEmuRecordStruct(btrieveFile.Records[0].Data);
            record.Key0.Should().Be("Sysop");
            record.Key1.Should().Be(3444);
        }

        [Fact]
        public void LoadFile_MissingFile_Throws()
        {
            var btrieveFile = new BtrieveFile();

            Assert.Throws<FileNotFoundException>(() => btrieveFile.LoadFile(_logger, _modulePath, "NOEXIST.DAT"));
        }

        [Fact]
        public void LoadFile_TooShort_ThrowsArgumentException()
        {
            var path = WriteRawFile(new byte[] { 0 });

            var btrieveFile = new BtrieveFile();

            var ex = Assert.Throws<ArgumentException>(() => btrieveFile.LoadFile(_logger, path));
            ex.Message.Should().Contain("Empty/Invalid Length");
        }

        [Fact]
        public void LoadFile_V6Signature_ThrowsArgumentException()
        {
            var path = WriteRawFile(new byte[] { (byte)'F', (byte)'C' });

            var btrieveFile = new BtrieveFile();

            var ex = Assert.Throws<ArgumentException>(() => btrieveFile.LoadFile(_logger, path));
            ex.Message.Should().Contain("v6 Btrieve database");
        }

        [Fact]
        public void LoadFile_NotV5Signature_ThrowsArgumentException()
        {
            // first 4 bytes all non-zero, and not the 'F','C' v6 signature
            var path = WriteRawFile(new byte[] { 1, 1, 1, 1 });

            var btrieveFile = new BtrieveFile();

            var ex = Assert.Throws<ArgumentException>(() => btrieveFile.LoadFile(_logger, path));
            ex.Message.Should().Contain("Doesn't appear to be a v5 Btrieve database");
        }

        [Fact]
        public void LoadFile_InvalidVersionCode_ThrowsArgumentException()
        {
            // 8 zeroed bytes: passes the length/signature checks, but the version code
            // (bytes 6-7) is 0, which isn't one of the supported v5 versions (3/4/5).
            var path = WriteRawFile(new byte[8]);

            var btrieveFile = new BtrieveFile();

            var ex = Assert.Throws<ArgumentException>(() => btrieveFile.LoadFile(_logger, path));
            ex.Message.Should().Contain("Invalid version code");
        }

        [Fact]
        public void LoadFile_InvalidPageLength_ThrowsArgumentException()
        {
            var data = new byte[40];
            data[7] = 3; // version code 3, a valid v5 version
            // PageLength (bytes 0x08-0x09) left at 0, which is < 512 and therefore invalid.
            var path = WriteRawFile(data);

            var btrieveFile = new BtrieveFile();

            var ex = Assert.Throws<ArgumentException>(() => btrieveFile.LoadFile(_logger, path));
            ex.Message.Should().Contain("Invalid PageLength");
        }
    }
}
