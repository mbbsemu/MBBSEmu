using MBBSEmu.HostProcess.Structs;
using MBBSEmu.Memory;
using System;
using System.Collections.Generic;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class gettime_Tests : ExportedModuleTestBase
    {
        private const int GETTIME_ORDINAL = 327;

        [Theory]
        [InlineData(1, 1, 2000, 23, 0, 1)]
        [InlineData(2, 2, 2010, 1, 30, 30)]
        [InlineData(3, 3, 2020, 8, 45, 15)]
        [InlineData(4, 22, 2035, 11, 0, 59)]
        [InlineData(12, 31, 1980, 20, 59, 1)]
        [InlineData(1, 18, 2038, 2, 14, 7)]
        [InlineData(1, 1, 1970, 0, 0, 0)]
        public void gettime_Test(int month, int day, int year, int hour, int minute, int second)
        {
            //Reset State
            Reset();

            //Set Argument Values to be Passed In
            fakeClock.Now = new DateTime(year, month, day, hour, minute, second, DateTimeKind.Utc);

            var dosTimeDateStructPointer = mbbsEmuMemoryCore.AllocateVariable("DOSDATETIME", TimeStruct.Size);
            
            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, GETTIME_ORDINAL, new List<FarPtr> { dosTimeDateStructPointer });

            //Verify Results
            var dosTimeDateStruct = mbbsEmuMemoryCore.GetArray(dosTimeDateStructPointer, TimeStruct.Size);
            
            Assert.Equal(minute, dosTimeDateStruct[0]);
            Assert.Equal(hour, dosTimeDateStruct[1]);
            Assert.Equal(second, dosTimeDateStruct[3]);
        }
    }
}
