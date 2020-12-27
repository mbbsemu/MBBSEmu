using System.Collections.Generic;
using System.Text;
using MBBSEmu.Memory;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class strlen_Tests : ExportedModuleTestBase
    {
        private const int STRLEN_ORDINAL = 578;

        [Theory]
        [InlineData("TEST", 4)]
        [InlineData("TEST1", 5)]
        [InlineData("TEST test TEST", 14)]
        [InlineData("", 0)]
        public void STRLEN_Test(string inputString, int expectedLength)
        {
            //Reset State
            Reset();

            //Set Argument Values to be Passed In
            var stringPointer = mbbsEmuMemoryCore.AllocateVariable("INPUT_STRING", (ushort)(inputString.Length + 1));
            mbbsEmuMemoryCore.SetArray("INPUT_STRING", Encoding.ASCII.GetBytes(inputString));

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, STRLEN_ORDINAL, new List<FarPtr> { stringPointer });

            //Verify Results
            Assert.Equal(mbbsEmuCpuRegisters.AX, expectedLength);
        }
    }
}
