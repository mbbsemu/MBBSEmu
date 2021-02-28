using System.Collections.Generic;
using System.Text;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public partial class Majorbbs_Tests
    {
        private const int NCEDAT_ORDINAL = 429;

        [Theory]
        [InlineData(1985, 4, 1, "01/04/85", true)]
        [InlineData(1990, 9, 2, "02/09/90", false)]
        [InlineData(1995, 11, 3, "03/11/95", true)]
        [InlineData(1999, 5, 4, "04/05/99", false)]
        [InlineData(2000, 1, 5, "05/01/00", true)]
        [InlineData(2010, 3, 7, "07/03/10", false)]
        [InlineData(2020, 12, 8, "08/12/20", true)]
        [InlineData(2001, 1, 1, "01/01/01", false)]
        [InlineData(2050, 1, 1, "01/01/50", true)]
        public void ncedat_Test(int year, int month, int day, string expectedDate, bool allocateNCEDAT)
        {
            //Reset State
            Reset();

            //Set Argument Values to be Passed In
            var inputDate = ((year - 1980) << 9) | (month << 5) | day;
           
            if(allocateNCEDAT)
                mbbsEmuMemoryCore.AllocateVariable("NCEDAT", sizeof(int));

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, NCEDAT_ORDINAL, new List<ushort> { (ushort)inputDate });

            //Verify Results
            Assert.Equal(expectedDate, Encoding.ASCII.GetString(mbbsEmuMemoryCore.GetString("NCEDAT", true)));
        }
    }
}
