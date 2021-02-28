using System;
using System.Collections.Generic;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public partial class Majorbbs_Tests
    {
        private const int TODAY_ORDINAL = 601;

        [Theory]
        [InlineData(1985, 4, 1)]
        [InlineData(1990, 9, 2)]
        [InlineData(1995, 11, 3)]
        [InlineData(1999, 5, 4)]
        [InlineData(2000, 1, 5)]
        [InlineData(2010, 3, 7)]
        [InlineData(2020, 12, 8)]
        [InlineData(2025, 1, 1)]
        [InlineData(2050, 1, 1)]
        public void today_Test(int year, int month, int day)
        {
            //Reset State
            Reset();

            //Set Argument Values to be Passed In
            fakeClock.Now = new DateTime(year, month, day);

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, TODAY_ORDINAL, new List<ushort>());

            //Verify Results
            var resultDate = ((year - 1980) << 9) | (month << 5) | day;
            Assert.Equal(resultDate, mbbsEmuCpuRegisters.AX);
        }
    }
}
