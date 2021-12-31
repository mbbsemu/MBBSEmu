using System.Collections.Generic;
using MBBSEmu.Extensions;
using MBBSEmu.HostProcess.Enums;
using MBBSEmu.HostProcess.Structs;
using MBBSEmu.Memory;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class condex_Tests : ExportedModuleTestBase
    {
        private const ushort CONDEX_ORDINAL = 137;

        [Fact]
        public void condexSet_Test()
        {
            Reset();

            //Set Concex flag to halt CPU and set session status=0
            testSessions[0].UsrPtr.Flags = testSessions[0].UsrPtr.Flags.SetFlag((ushort)EnumRuntimeFlags.Concex);

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, CONDEX_ORDINAL, new List<FarPtr>());

            //Verify Results
            Assert.True(mbbsEmuCpuRegisters.Halt);
            Assert.Equal(UserStatus.UNUSED, testSessions[0].GetStatus());
        }

        [Fact]
        public void condexNotSet_Test()
        {
            Reset();

            //Concex flag not set

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, CONDEX_ORDINAL, new List<FarPtr>());

            //Verify Results
            Assert.False(mbbsEmuCpuRegisters.Halt);
            Assert.Equal(UserStatus.RING, testSessions[0].GetStatus());
        }
    }
}
