using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public partial class Majorbbs_Tests
    {
        private const int DIGALW_ORDINAL = 169;

        [Fact]
        public void digalw_Test()
        {
            //Reset State
            Reset();

            var actual = mbbsEmuMemoryCore.GetWord("DIGALW");

            Assert.Equal(1, actual);
        }
    }
}
