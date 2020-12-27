using MBBSEmu.Memory;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class sameto_Tests : ExportedModuleTestBase
    {
        private const int SAMETO_ORDINAL = 521;

        [Theory]
        //Actual examples from the documentation! XD
        [InlineData("good", "gooder", 1)]
        [InlineData("good", "good enough", 1)]
        [InlineData("best", "best", 1)]
        [InlineData("badder", "bad", 0)]
        [InlineData("women", "womanhood", 0)]
        public void sameto_Test(string substring, string stringToSearch, ushort expected)
        {
            //Reset State
            Reset();

            //Set Argument Values to be Passed In
            var str1Pointer = mbbsEmuMemoryCore.AllocateVariable("STR1", (ushort)(substring.Length + 1));
            mbbsEmuMemoryCore.SetArray(str1Pointer, Encoding.ASCII.GetBytes(substring));

            var str2Pointer = mbbsEmuMemoryCore.AllocateVariable("STR2", (ushort)(stringToSearch.Length + 1));
            mbbsEmuMemoryCore.SetArray(str2Pointer, Encoding.ASCII.GetBytes(stringToSearch));

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, SAMETO_ORDINAL, new List<FarPtr> { str1Pointer, str2Pointer });

            Assert.Equal(expected, mbbsEmuCpuRegisters.AX);
        }
    }
}
