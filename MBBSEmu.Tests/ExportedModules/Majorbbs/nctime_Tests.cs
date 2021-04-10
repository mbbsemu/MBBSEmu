using System.Collections.Generic;
using System.Text;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class nctime_Tests : ExportedModuleTestBase
    {
        private const int NCTIME_ORDINAL = 430;

        [Theory]
        [InlineData(12, 4, 10, "12:04:10\0", true)]
        [InlineData(3, 9, 2, "03:09:02\0", false)]
        [InlineData(4, 11, 15, "04:11:14\0", true)]
        [InlineData(7, 5, 3, "07:05:02\0", false)]
        [InlineData(10, 1, 28, "10:01:28\0", true)]
        [InlineData(14, 3, 36, "14:03:36\0", false)]
        [InlineData(18, 12, 7, "18:12:06\0", true)]
        [InlineData(23, 1, 44, "23:01:44\0", false)]
        [InlineData(23, 59, 58, "23:59:58\0", true)]
        public void nctime_Test(int hour, int minutes, int seconds, string expectedTime, bool allocateNCTIME)
        {
            //Reset State
            Reset();

            //Set Argument Values to be Passed In -- Odd seconds are rounded down to next even number
            var packedTime = hour << 11 | minutes << 5 | seconds >> 1;

            if (allocateNCTIME)
                mbbsEmuMemoryCore.AllocateVariable("NCTIME", (ushort)expectedTime.Length);

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, NCTIME_ORDINAL, new List<ushort> { (ushort) packedTime });

            //Put Garbage After it to ensure the null terminator catches
            var garbagePointer = mbbsEmuMemoryCore.AllocateVariable("GARBAGE", 10);
            mbbsEmuMemoryCore.SetArray(garbagePointer, Encoding.ASCII.GetBytes(new string('C', 10)));

            //Verify Results
            Assert.Equal(expectedTime, Encoding.ASCII.GetString(mbbsEmuMemoryCore.GetString("NCTIME")));
        }
    }
}
