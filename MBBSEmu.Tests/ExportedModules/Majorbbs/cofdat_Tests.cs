using System.Collections.Generic;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class cofdat_Tests : ExportedModuleTestBase
    {
        private const int COFDAT_ORDINAL = 134;

        [Theory]
        [InlineData(1985, 4, 1, 23832)]
        [InlineData(1990, 9, 2, 25812)]
        [InlineData(1995, 11, 3, 27700)]
        [InlineData(1999, 5, 4, 28978)]
        [InlineData(2000, 1, 5, 29224)]
        [InlineData(2010, 3, 7, 32938)]
        [InlineData(2020, 12, 8, 36867)]
        public void cofdat_Test(int year, int month, int day, int expectedNumDays)
        {
            //Reset State
            Reset();

            //Set Argument Values to be Passed In
            var inputDate = (year << 9) | (month << 5) | day;

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, COFDAT_ORDINAL, new List<ushort> { (ushort) inputDate });

            //Verify Results
            Assert.Equal(expectedNumDays, mbbsEmuCpuRegisters.AX);
        }

        [Fact]
        public void cofdat_empty_Test()
        {
            //Reset State
            Reset();

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, COFDAT_ORDINAL, new List<ushort>());

            //Verify Results
            Assert.Equal(0, mbbsEmuCpuRegisters.AX);
        }
    }
}
