using MBBSEmu.Memory;
using System;
using System.Collections.Generic;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class daytoday_Tests : ExportedModuleTestBase
    {
        private const int DAYTODAY_ORDINAL = 155;

        [Theory]
        [InlineData(1979, 4, 1, DayOfWeek.Sunday)]
        [InlineData(1979, 4, 2, DayOfWeek.Monday)]
        [InlineData(1979, 4, 3, DayOfWeek.Tuesday)]
        [InlineData(1979, 4, 4, DayOfWeek.Wednesday)]
        [InlineData(1979, 4, 5, DayOfWeek.Thursday)]
        [InlineData(1979, 4, 6, DayOfWeek.Friday)]
        [InlineData(1979, 4, 7, DayOfWeek.Saturday)]
        [InlineData(1979, 4, 8, DayOfWeek.Sunday)]
        public void daytoday_Test(int year, int month, int day, DayOfWeek expectedDayOfWeek)
        {
            //Reset State
            Reset();

            //Set fake clock
            fakeClock.Now = new DateTime(year, month, day);
            
            //Execute test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, DAYTODAY_ORDINAL, new List<IntPtr16>());
            
            //Verify results
            Assert.Equal((int)expectedDayOfWeek, mbbsEmuCpuRegisters.AX);
        }
    }
}
