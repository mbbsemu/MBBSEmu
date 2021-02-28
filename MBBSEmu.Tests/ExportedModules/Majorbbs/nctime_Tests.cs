using System.Collections.Generic;
using System.Text;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public partial class Majorbbs_Tests
    {
        private const int NCTIME_ORDINAL = 430;

        [Theory]
        [InlineData(12, 4, 10, "12:04:10", true)]
        [InlineData(3, 9, 2, "03:09:02", false)]
        [InlineData(4, 11, 15, "04:11:14", true)]
        [InlineData(7, 5, 3, "07:05:02", false)]
        [InlineData(10, 1, 28, "10:01:28", true)]
        [InlineData(14, 3, 36, "14:03:36", false)]
        [InlineData(18, 12, 7, "18:12:06", true)]
        [InlineData(23, 1, 44, "23:01:44", false)]
        [InlineData(23, 59, 58, "23:59:58", true)]
        public void nctime_Test(int hour, int minutes, int seconds, string expectedTime, bool allocateNCTIME)
        {
            //Reset State
            Reset();

            //Set Argument Values to be Passed In -- Odd seconds are rounded down to next even number
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
