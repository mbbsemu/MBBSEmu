using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Phapi
{
    /// <summary>
    ///     PHAPI functions are imported by name. Trade Wars 2002 imports DOSREALINTR, while Galactic
    ///     Empire imports _DOSREALINTR (with the C compiler's leading underscore). Both must reach the
    ///     same function.
    /// </summary>
    public class GetOrdinal_Tests
    {
        [Theory]
        [InlineData("DOSREALINTR", 49)]
        [InlineData("_DOSREALINTR", 49)]
        [InlineData("DosRealIntr", 49)]
        [InlineData("DOSALLOCREALSEG", 16)]
        [InlineData("_DOSALLOCREALSEG", 16)]
        public void ResolvesNamesWithOrWithoutLeadingUnderscore(string importedName, ushort expectedOrdinal)
        {
            Assert.Equal(expectedOrdinal, HostProcess.ExportedModules.Phapi.GetOrdinal(importedName));
        }

        [Fact]
        public void UnimplementedNameGetsAReportingOrdinal()
        {
            Assert.True(HostProcess.ExportedModules.Phapi.GetOrdinal("_DOSNOTAREALFUNCTION") >= 0xFF00);
        }
    }
}
