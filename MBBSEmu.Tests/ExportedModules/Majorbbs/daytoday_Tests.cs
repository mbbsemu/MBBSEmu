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

            fakeClock.Now = new DateTime(1979, 4, 1); // Sunday (0)
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, DAYTODAY_ORDINAL, new List<IntPtr16>());
            Assert.Equal(0, mbbsEmuCpuRegisters.AX);

            fakeClock.Now = new DateTime(1979, 4, 2); // Monday (1)
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, DAYTODAY_ORDINAL, new List<IntPtr16>());
            Assert.Equal(1, mbbsEmuCpuRegisters.AX);
        }
    }
}
