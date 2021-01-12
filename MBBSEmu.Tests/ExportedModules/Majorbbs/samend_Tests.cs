using MBBSEmu.Memory;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class samend_Tests : ExportedModuleTestBase
    {
        private const int SAMEND_ORDINAL = 813;

        [Theory]
        [InlineData("HoooRay", "ray", 1)]
        [InlineData("Good Gracious", "OUS", 1)]
        [InlineData("For", "for", 1)]
        [InlineData("For he's a jolly good fellow", "FEllOW", 1)]
        [InlineData("YesYes", "fff", 0)]
        [InlineData("NoNo", "On", 0)]
        [InlineData("", "", 1)]
        [InlineData("", "A", 0)]
        public void samend_Test(string stringToSearch, string stringEnd, ushort expected)
        {
            //Reset State
            Reset();

            //Set Argument Values to be Passed In
            var str1Pointer = mbbsEmuMemoryCore.AllocateVariable("STR1", (ushort)(stringToSearch.Length + 1));
            mbbsEmuMemoryCore.SetArray(str1Pointer, Encoding.ASCII.GetBytes(stringToSearch));

            var str2Pointer = mbbsEmuMemoryCore.AllocateVariable("STR2", (ushort)(stringEnd.Length + 1));
            mbbsEmuMemoryCore.SetArray(str2Pointer, Encoding.ASCII.GetBytes(stringEnd));

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, SAMEND_ORDINAL, new List<FarPtr> { str1Pointer, str2Pointer });

            Assert.Equal(expected, mbbsEmuCpuRegisters.AX);
        }
    }
}
