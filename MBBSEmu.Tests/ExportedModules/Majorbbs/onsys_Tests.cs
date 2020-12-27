using MBBSEmu.Memory;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class onsys_Tests : ExportedModuleTestBase
    {
        private const int ONSYS_ORDINAL = 449;

        [Theory]
        [InlineData("TEST", true)]
        [InlineData("test", true)]
        [InlineData("tes", false)]
        [InlineData("test1", false)]
        [InlineData("", false)]
        public void ONSYS_Test(string inputString, bool isOnline)
        {
            //Reset State
            Reset();

            //Set the test Username
            testSessions[0].Username = "Test";

            //Set Argument Values to be Passed In
            var stringPointer = mbbsEmuMemoryCore.AllocateVariable("INPUT_STRING", (ushort)(inputString.Length + 1));
            mbbsEmuMemoryCore.SetArray("INPUT_STRING", Encoding.ASCII.GetBytes(inputString));

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, ONSYS_ORDINAL, new List<FarPtr> { stringPointer });

            Assert.Equal(isOnline, mbbsEmuCpuRegisters.AX == 1);

        }
    }
}
