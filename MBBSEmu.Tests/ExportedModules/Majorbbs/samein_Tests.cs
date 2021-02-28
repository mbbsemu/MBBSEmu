using MBBSEmu.Memory;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public partial class Majorbbs_Tests
    {
        private const int SAMEIN_ORDINAL = 521;

        [Theory]
        [InlineData("test", "test", 1)]
        [InlineData("test", "XtestX",  1)]
        [InlineData("TEST", "XtestX",  1)]
        [InlineData("test", "XTESTX", 1)]
        [InlineData("test", "\0",  0)]
        [InlineData("\0", "test", 0)]
        [InlineData("\0", "\0", 0)]
        [InlineData("test", "t3st",  0)]
        [InlineData("t", "test",  1)]
        [InlineData("testing", "test", 0)]
        public void samein_Test(string substring, string stringToSearch, ushort expected)
        {
            //Reset State
            Reset();

            //Set Argument Values to be Passed In
            var str1Pointer = mbbsEmuMemoryCore.AllocateVariable("STR1", (ushort)(substring.Length + 1));
            mbbsEmuMemoryCore.SetArray(str1Pointer, Encoding.ASCII.GetBytes(substring));

            var str2Pointer = mbbsEmuMemoryCore.AllocateVariable("STR2", (ushort)(stringToSearch.Length + 1));
            mbbsEmuMemoryCore.SetArray(str2Pointer, Encoding.ASCII.GetBytes(stringToSearch));

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, SAMEIN_ORDINAL, new List<FarPtr> { str1Pointer, str2Pointer });

            Assert.Equal(expected, mbbsEmuCpuRegisters.AX);
        }
    }
}
