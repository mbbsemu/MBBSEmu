using MBBSEmu.Memory;
using System;
using System.Collections.Generic;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class daytoday_Tests : ExportedModuleTestBase
    {
        private const int DAYTODAY_ORDINAL = 155;

        [Fact]
        public void daytoday_Test()
        {
            //Reset State
            Reset();

            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, DAYTODAY_ORDINAL, new List<IntPtr16>());

            //Verify Results
            ExecutePropertyTest(DAYTODAY_ORDINAL);

            //Expected Results based on the Day of the Week
            var expectedResult = DateTime.Now.DayOfWeek switch
            {
                DayOfWeek.Sunday => 0,
                DayOfWeek.Monday => 1,
                DayOfWeek.Tuesday => 2,
                DayOfWeek.Wednesday => 3,
                DayOfWeek.Thursday => 4,
                DayOfWeek.Friday => 5,
                DayOfWeek.Saturday => 6,
                _ => throw new ArgumentOutOfRangeException()
            };

            Assert.Equal(expectedResult, mbbsEmuCpuRegisters.AX);
        }
    }
}
