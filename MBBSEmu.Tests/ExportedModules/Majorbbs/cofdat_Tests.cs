using System.Collections.Generic;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class cofdat_Tests : ExportedModuleTestBase
    {
        private const int COFDAT_ORDINAL = 134;

        [Theory]
        [InlineData(1985, 4, 1, 1917)]
        [InlineData(1990, 9, 2, 3897)]
        [InlineData(1995, 11, 3, 5785)]
        [InlineData(1999, 5, 4, 7063)]
        [InlineData(2000, 1, 5, 7309)]
        [InlineData(2010, 3, 7, 11023)]
        [InlineData(2020, 12, 8, 14952)]
        [InlineData(2025, 1, 1, 16437)]
        [InlineData(2050, 1, 1, 25568)]
        [InlineData(1980, 0, 0, 0)]
        public void cofdat_Test(int year, int month, int day, int expectedNumDays)
        {
            //Reset State
            Reset();

            //Set Argument Values to be Passed In
            var inputDate = ((year - 1980) << 9) | (month << 5) | day;

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, COFDAT_ORDINAL, new List<ushort> { (ushort) inputDate });

            //Verify Results
            Assert.Equal(expectedNumDays, mbbsEmuCpuRegisters.AX);
        }
    }
}
