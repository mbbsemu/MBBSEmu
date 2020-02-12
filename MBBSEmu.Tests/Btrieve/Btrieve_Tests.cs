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
            Assert.Equal(0, btrieveFile.KeyCount);
            Assert.Equal(0, btrieveFile.KeyLength);
            Assert.Equal(0, btrieveFile.RecordCount);
            Assert.Equal(70, btrieveFile.RecordLength);
        }

        [Theory]
        [InlineData(@"C:\dos\modules\dialchat", "DIALACT.DAT")]
        public void Btrieve_MultiplePages_Keys(string path, string fileName)
        {
            var btrieveFile = new BtrieveFile(fileName, path, ushort.MaxValue);
            Assert.Equal(1, btrieveFile.PageCount);
            Assert.Equal(512, btrieveFile.PageLength);
            Assert.Equal(0, btrieveFile.KeyCount);
            Assert.Equal(0, btrieveFile.KeyLength);
            Assert.Equal(0, btrieveFile.RecordCount);
            Assert.Equal(70, btrieveFile.RecordLength);
        }
    }
}
