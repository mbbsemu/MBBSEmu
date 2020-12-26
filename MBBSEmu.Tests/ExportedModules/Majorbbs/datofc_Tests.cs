using System;
using System.Collections.Generic;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class datofc_Tests : ExportedModuleTestBase
    {
        private const int DATOFC_ORDINAL = 154;

        [Theory]
        [InlineData(1945, 1985, 4, 29)]
        [InlineData(3897, 1990, 9, 2)]
        [InlineData(5785, 1995, 11, 3)]
        [InlineData(7063, 1999, 5, 4)]
        [InlineData(7309, 2000, 1, 5)]
        [InlineData(11023, 2010, 3, 7)]
        [InlineData(14952, 2020, 12, 8)]
        [InlineData(0, 1980, 1, 1)]
        public void datofc_Test(ushort numDays, int year, int month, int day)
        {
            //Reset State
            Reset();

            //Set Argument Values to be Passed In
            var expectedDate = ((year - 1980) << 9) | (month << 5) | day;

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, DATOFC_ORDINAL, new List<ushort> { numDays });

            //Verify Results
            Assert.Equal(expectedDate, mbbsEmuCpuRegisters.AX);
        }
    }
}
