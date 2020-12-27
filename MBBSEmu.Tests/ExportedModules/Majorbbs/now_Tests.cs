using System;
using System.Collections.Generic;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class now_Tests : ExportedModuleTestBase
    {
        private const int NOW_ORDINAL = 435;

        [Theory]
        [InlineData(12, 4, 10, 10)]
        [InlineData(3, 9, 2, 2)]
        [InlineData(4, 11, 15, 14)]
        [InlineData(7, 5, 4, 4)]
        [InlineData(10, 1, 28, 28)]
        [InlineData(14, 3, 35, 34)]
        [InlineData(18, 12, 8, 8)]
        [InlineData(23, 1, 43, 42)]
        [InlineData(23, 59, 58, 58)]
        public void now_Test(int hour, int minutes, int seconds, int expectedSeconds)
        {
            //Reset State
            Reset();

            //Set Argument Values to be Passed In -- Odd seconds are rounded down to nearest even
            fakeClock.Now = new DateTime(2000, 1, 1, hour, minutes, seconds);

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, NOW_ORDINAL, new List<ushort>());

            //Verify Results
            Assert.Equal(hour, (mbbsEmuCpuRegisters.AX >> 11) & 0x1F);
            Assert.Equal(minutes, (mbbsEmuCpuRegisters.AX >> 5) & 0x3F);
            Assert.Equal(expectedSeconds, (mbbsEmuCpuRegisters.AX << 1) & 0x3E);
        }
    }
}
