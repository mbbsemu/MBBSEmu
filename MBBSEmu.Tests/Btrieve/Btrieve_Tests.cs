using MBBSEmu.Btrieve;
using Xunit;

namespace MBBSEmu.Tests.Btrieve
{
    public class Btrieve_Tests
    {
        [Theory]
        [InlineData(@"C:\dos\install3", "GWWARROW.DAT")]
        public void Btrieve_SinglePage_NoKeys(string path, string fileName)
        {
            var btrieveFile = new BtrieveFile(fileName, path, ushort.MaxValue);
            Assert.Equal(1, btrieveFile.PageCount);
            Assert.Equal(512, btrieveFile.PageLength);
            Assert.Equal(0, btrieveFile.RecordCount);
            Assert.Equal(70, btrieveFile.RecordLength);
            Assert.Equal(0, btrieveFile.KeyCount);
        }

        [Theory]
        [InlineData(@"C:\dos\modules\dialchat", "DIALACT.DAT")]
        public void Btrieve_MultiplePages_Keys(string path, string fileName)
        {
            var btrieveFile = new BtrieveFile(fileName, path, ushort.MaxValue);
            Assert.Equal(24, btrieveFile.PageCount);
            Assert.Equal(1024, btrieveFile.PageLength);
            Assert.Equal(61, btrieveFile.RecordCount);
            Assert.Equal(333, btrieveFile.RecordLength);
            Assert.Equal(1, btrieveFile.KeyCount);
            Assert.Equal(1, btrieveFile.Keys[0].Segments[0].Position);
            Assert.Equal(9, btrieveFile.Keys[0].Segments[0].Length);
            Assert.Equal(61, btrieveFile.Keys[0].Segments[0].TotalRecords);
        }

        [Theory]
        [InlineData(@"C:\dos\modules\telearena", "tsgarn-c.DAT")]
        public void Btrieve_MultiplePages_Keys2(string path, string fileName)
        {
            var btrieveFile = new BtrieveFile(fileName, path, ushort.MaxValue);
            Assert.Equal(2, btrieveFile.PageCount);
            Assert.Equal(1024, btrieveFile.PageLength);
            Assert.Equal(1, btrieveFile.KeyCount);
            Assert.Equal(1, btrieveFile.RecordCount);
            Assert.Equal(256, btrieveFile.RecordLength);
            Assert.Equal(1, btrieveFile.Keys[0].Segments[0].Position);
            Assert.Equal(30, btrieveFile.Keys[0].Segments[0].Length);
            Assert.Equal(1, btrieveFile.Keys[0].Segments[0].TotalRecords);
        }

        [Theory]
        [InlineData(@"C:\dos\modules\telearena", "tsgarn-c.DAT", new byte[] { 0x53, 0x79, 0x73, 0x6F, 0x70, 0x00 })]
        public void GetRecord_ByKey(string path, string fileName, byte[] key)
        {
            var btrieveFile = new BtrieveFile(fileName, path, ushort.MaxValue);
            var result = btrieveFile.SeekByKey(0, key);

            Assert.Equal(1, result);
            Assert.Equal((uint)2054, btrieveFile.AbsolutePosition);
        }

        [Theory]
        [InlineData(@"C:\dos\modules\telearena", "tsgarn-c.DAT", 2054)]
        public void GetRecord_ByAbsolutePosition(string path, string fileName, ushort absolutePosition)
        {
            var btrieveFile = new BtrieveFile(fileName, path, ushort.MaxValue);
            var record = btrieveFile.GetRecordByAbsolutePosition(absolutePosition);

            Assert.Equal(0x53, record[0]);
            Assert.Equal(0x79, record[1]);
            Assert.Equal(0x73, record[2]);
            Assert.Equal(0x6F, record[3]);
            Assert.Equal(0x70, record[4]);
            Assert.Equal(0x00, record[5]);
        }
    }
}
