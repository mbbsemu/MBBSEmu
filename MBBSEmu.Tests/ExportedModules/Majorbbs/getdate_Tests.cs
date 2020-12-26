using MBBSEmu.HostProcess.Structs;
using MBBSEmu.Memory;
using System;
using System.Collections.Generic;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class getdate_Tests : ExportedModuleTestBase
    {
        private const int GETDATE_ORDINAL = 320;

        [Theory]
        [InlineData(1, 1, 2000)]
        [InlineData(2, 2, 2010)]
        [InlineData(3, 3, 2020)]
        [InlineData(4, 22, 2035)]
        [InlineData(12, 31, 1980)]
        [InlineData(1, 18, 2038)]
        [InlineData(1, 1, 1970)]
        public void getdate_Test(int month, int day, int year)
        {
            //Reset State
            Reset();


            //Set Argument Values to be Passed In
            fakeClock.Now = new DateTime(year, month, day);
            var dateStringPointer = mbbsEmuMemoryCore.AllocateVariable("DATE_STRING", (DateStruct.Size));
            
            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, GETDATE_ORDINAL, new List<IntPtr16> { dateStringPointer });

            //Verify Results
            var expectedDateStruct = new DateStruct(mbbsEmuMemoryCore.GetArray(dateStringPointer, DateStruct.Size));
            var expectedDate = new DateTime(expectedDateStruct.year, expectedDateStruct.month, expectedDateStruct.day);

            Assert.Equal(fakeClock.Now, expectedDate);
        }
    }
}
