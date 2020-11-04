using MBBSEmu.Memory;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class haskey_Tests :ExportedModuleTestBase
    {

        private const ushort HASKEY_ORDINAL = 334;

        [Fact]
        public void haskey_Test()
        {
            //Reset State
            Reset();

            //Set the test Username
            testSessions[0].Username = "sysop";
            var key = "SYSOP";

            //Set Argument Values to be Passed In
            var stringPointer = mbbsEmuMemoryCore.AllocateVariable("INPUT_STRING", (ushort)(key.Length + 1));
            mbbsEmuMemoryCore.SetArray("INPUT_STRING", Encoding.ASCII.GetBytes("SYSOP"));

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, HASKEY_ORDINAL, new List<IntPtr16> { stringPointer });

            Assert.Equal(1, mbbsEmuCpuRegisters.AX);

        }
    }
}
