using MBBSEmu.Memory;
using System;
using System.Collections.Generic;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class time_Tests : ExportedModuleTestBase
    {
        private const int TIME_ORDINAL = 599;

        [Theory]
        [InlineData(1, 1, 2000, 23, 0, 1, 946767601)]
        [InlineData(2, 2, 2010, 1, 30, 30, 1265074230)]
        [InlineData(3, 3, 2020, 8, 45, 15, 1583225115)]
        [InlineData(4, 22, 2035, 11, 0, 59, 2060852459)]
        [InlineData(12, 31, 1980, 20, 59, 1, 347144341)]
        [InlineData(1, 18, 2038, 2, 14, 7, 2147393647)]
        [InlineData(1, 1, 1970, 0, 0, 0, 0)]
        public void time_Test(int month, int day, int year, int hour, int minute, int second, int expectedUnixDate)
        {
            //Reset State
            Reset();
            
            //Set Argument Values to be Passed In
            fakeClock.Now = new DateTime(year, month, day, hour, minute, second, DateTimeKind.Utc);

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, TIME_ORDINAL, new List<ushort> ());

            //Verify Results
            Assert.Equal(expectedUnixDate >> 16, mbbsEmuCpuRegisters.DX);
            Assert.Equal(expectedUnixDate & 0xFFFF, mbbsEmuCpuRegisters.AX);
        }
    }
}
