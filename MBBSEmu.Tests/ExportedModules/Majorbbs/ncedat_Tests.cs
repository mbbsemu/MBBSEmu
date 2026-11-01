using System.Collections.Generic;
using System.Text;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class ncedat_Tests : ExportedModuleTestBase
    {
        private const int NCEDAT_ORDINAL = 429;
        private const int NCDATE_ORDINAL = 428;

        [Theory]
        [InlineData(1985, 4, 1, "01-APR-85", true)]
        [InlineData(1990, 9, 2, "02-SEP-90", false)]
        [InlineData(1995, 11, 3, "03-NOV-95", true)]
        [InlineData(1999, 5, 4, "04-MAY-99", false)]
        [InlineData(2000, 1, 5, "05-JAN-00", true)]
        [InlineData(2010, 3, 7, "07-MAR-10", false)]
        [InlineData(2020, 12, 8, "08-DEC-20", true)]
        [InlineData(2001, 1, 1, "01-JAN-01", false)]
        [InlineData(2050, 1, 1, "01-JAN-50", true)]
        public void ncedat_Test(int year, int month, int day, string expectedDate, bool allocateNCEDAT)
        {
            //Reset State
            Reset();

            //Set Argument Values to be Passed In
            var inputDate = ((year - 1980) << 9) | (month << 5) | day;

            if (allocateNCEDAT)
                mbbsEmuMemoryCore.AllocateVariable("NCEDAT", sizeof(int));

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, NCEDAT_ORDINAL, new List<ushort> { (ushort)inputDate });

            //Verify Results
            Assert.Equal(expectedDate, Encoding.ASCII.GetString(mbbsEmuMemoryCore.GetString("NCEDAT", true)));
        }

        /// <summary>
        ///     Expected strings are those of the original kernels' NCDATE and NCEDAT, executed on the same packed dates
        ///     (The Major BBS 6.x and Worldgroup MAJORBBS.EXE give identical results).
        /// </summary>
        [Theory]
        [InlineData(0x0000, "", "00-000-80")]
        [InlineData(0x1E07, "00/07/95", "07-000-95")]
        [InlineData(0x1E27, "01/07/95", "07-JAN-95")]
        [InlineData(0x1E47, "02/07/95", "07-FEB-95")]
        [InlineData(0x1E67, "03/07/95", "07-MAR-95")]
        [InlineData(0x1E87, "04/07/95", "07-APR-95")]
        [InlineData(0x1EA7, "05/07/95", "07-MAY-95")]
        [InlineData(0x1EC7, "06/07/95", "07-JUN-95")]
        [InlineData(0x1EE7, "07/07/95", "07-JUL-95")]
        [InlineData(0x1F07, "08/07/95", "07-AUG-95")]
        [InlineData(0x1F27, "09/07/95", "07-SEP-95")]
        [InlineData(0x1F47, "10/07/95", "07-OCT-95")]
        [InlineData(0x1F67, "11/07/95", "07-NOV-95")]
        [InlineData(0x1F87, "12/07/95", "07-DEC-95")]
        [InlineData(0x1FA7, "13/07/95", "07-XXX-95")]
        [InlineData(0x1FC7, "14/07/95", "07-XXX-95")]
        [InlineData(0x1FE7, "15/07/95", "07-XXX-95")]
        [InlineData(0x2040, "02/00/96", "00-FEB-96")]
        [InlineData(0x2041, "02/01/96", "01-FEB-96")]
        [InlineData(0x2049, "02/09/96", "09-FEB-96")]
        [InlineData(0x204A, "02/10/96", "10-FEB-96")]
        [InlineData(0x205D, "02/29/96", "29-FEB-96")]
        [InlineData(0x205F, "02/31/96", "31-FEB-96")]
        [InlineData(0x0021, "01/01/80", "01-JAN-80")]
        [InlineData(0x2621, "01/01/99", "01-JAN-99")]
        [InlineData(0x2821, "01/01/00", "01-JAN-00")]
        [InlineData(0x3221, "01/01/05", "01-JAN-05")]
        [InlineData(0x3A21, "01/01/09", "01-JAN-09")]
        [InlineData(0x3C21, "01/01/10", "01-JAN-10")]
        [InlineData(0xC621, "01/01/79", "01-JAN-79")]
        [InlineData(0xC821, "01/01/80", "01-JAN-80")]
        [InlineData(0xEE21, "01/01/99", "01-JAN-99")]
        [InlineData(0xFE21, "01/01/07", "01-JAN-07")]
        [InlineData(0x0001, "00/01/80", "01-000-80")]
        [InlineData(0x0020, "01/00/80", "00-JAN-80")]
        [InlineData(0xFFFF, "15/31/07", "31-XXX-07")]
        public void ncdate_ncedat_MatchKernel(int packedDate, string expectedNcdate, string expectedNcedat)
        {
            Reset();

            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, NCDATE_ORDINAL, new List<ushort> { (ushort)packedDate });
            Assert.Equal(expectedNcdate, Encoding.ASCII.GetString(mbbsEmuMemoryCore.GetString(mbbsEmuCpuRegisters.GetPointer(), true)));

            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, NCEDAT_ORDINAL, new List<ushort> { (ushort)packedDate });
            Assert.Equal(expectedNcedat, Encoding.ASCII.GetString(mbbsEmuMemoryCore.GetString(mbbsEmuCpuRegisters.GetPointer(), true)));
        }
    }
}
