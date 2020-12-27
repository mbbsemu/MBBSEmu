using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class nctime_Tests : ExportedModuleTestBase
    {
        private const int NCTIME_ORDINAL = 430;

        [Theory]
        [InlineData(12, 4, 1, "12:04:01", true)]
        [InlineData(3, 9, 2, "03:09:02", false)]
        [InlineData(4, 11, 3, "04:11:03", true)]
        [InlineData(7, 5, 4, "07:05:04", false)]
        [InlineData(10, 1, 5, "10:01:05", true)]
        [InlineData(14, 3, 7, "14:03:07", false)]
        [InlineData(18, 12, 8, "18:12:08", true)]
        [InlineData(23, 1, 1, "23:01:01", false)]
        [InlineData(23, 59, 59, "23:59:59", true)]
        public void nctime_Test(int hour, int minutes, int seconds, string expectedTime, bool allocateNCTIME)
        {
            //Reset State
            Reset();

            //Set Argument Values to be Passed In
            var packedTime = hour << 11 | minutes << 5 | seconds >> 1;

            if (allocateNCTIME)
                mbbsEmuMemoryCore.AllocateVariable("NCTIME", sizeof(int));

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, NCTIME_ORDINAL, new List<ushort> { (ushort) packedTime });

            //Verify Results
            Assert.Equal(expectedTime, Encoding.ASCII.GetString(mbbsEmuMemoryCore.GetString("NCTIME", true)));

        }
    }
}
