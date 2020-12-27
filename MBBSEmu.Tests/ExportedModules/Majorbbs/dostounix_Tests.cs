using MBBSEmu.HostProcess.Structs;
using MBBSEmu.Memory;
using System;
using System.Collections.Generic;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class dostounix_Tests : ExportedModuleTestBase
    {
        private const int DOSTOUNIX_ORDINAL = 173;

        [Theory]
        [InlineData(1, 1, 1970, 0, 0, 0)]
        [InlineData(1, 1, 1975, 0, 0, 1)]
        [InlineData(1, 1, 2000, 23, 0, 1)]
        [InlineData(2, 2, 2010, 1, 30, 30)]
        [InlineData(3, 3, 2020, 8, 45, 15)]
        [InlineData(4, 22, 2035, 11, 0, 59)]
        [InlineData(12, 31, 1980, 20, 59, 1)]
        [InlineData(1, 18, 2038, 2, 14, 7)]
        public void dostounix_Test(int month, int day, int year, int hour, int minute, int second)
        {
            //Reset State
            Reset();

            fakeClock.Now = new DateTime(year, month, day, hour, minute, second);
            var expectedUnixDate = ((DateTimeOffset)fakeClock.Now).ToUnixTimeSeconds();

            var inputDateStruct = new DateStruct(fakeClock.Now);
            var inputTimeStruct = new TimeStruct(fakeClock.Now);

            //Set Argument Values to be Passed In
            var dateStringPointer = mbbsEmuMemoryCore.AllocateVariable("DATE_STRING", (DateStruct.Size));
            mbbsEmuMemoryCore.SetArray(dateStringPointer, inputDateStruct.Data);

            var timeStringPointer = mbbsEmuMemoryCore.AllocateVariable("TIME_STRING", TimeStruct.Size);
            mbbsEmuMemoryCore.SetArray(timeStringPointer, inputTimeStruct.Data);

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, DOSTOUNIX_ORDINAL, new List<FarPtr> { dateStringPointer, timeStringPointer });

            //Verify Results
            Assert.Equal(expectedUnixDate, mbbsEmuCpuRegisters.GetLong());
        }
    }
}
