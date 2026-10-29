using MBBSEmu.Memory;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class rstrin_Tests : ExportedModuleTestBase
    {
        private const ushort PARSIN_ORDINAL = 467;
        private const ushort RSTRIN_ORDINAL = 511;

        [Theory]
        [InlineData("TEST1 TEST2 TEST3\0", "TEST1 TEST2 TEST3")]
        [InlineData("\0", "")]
        [InlineData("A B\0", "A B")]
        [InlineData("A TEST B\0", "A TEST B")]
        [InlineData("A   TEST   B\0", "A   TEST   B")]
        [InlineData("A TEST B   \0", "A TEST B")]
        public void rstrin_Test(string inputCommand, string expectedInput)
        {
            //Reset State
            Reset();

            //Set Input Values
            mbbsEmuMemoryCore.SetArray("INPUT", Encoding.ASCII.GetBytes(inputCommand));

            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, PARSIN_ORDINAL, new List<FarPtr>());
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, RSTRIN_ORDINAL, new List<FarPtr>());

            //Verify Results
            Assert.Equal(expectedInput, Encoding.ASCII.GetString(mbbsEmuMemoryCore.GetString("INPUT", true)));
        }

        [Fact]
        public void rstrin_leaves_earlier_input_alone_Test()
        {
            //Reset State
            Reset();

            //A module copies a short command over a longer input, parses it, then restores it
            mbbsEmuMemoryCore.SetArray("INPUT", Encoding.ASCII.GetBytes("wield shimmering greatsword\0"));
            mbbsEmuMemoryCore.SetWord("INPLEN", 27);
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, PARSIN_ORDINAL, new List<FarPtr>());

            mbbsEmuMemoryCore.SetArray("INPUT", Encoding.ASCII.GetBytes("bash w\0"));
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, PARSIN_ORDINAL, new List<FarPtr>());
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, RSTRIN_ORDINAL, new List<FarPtr>());

            Assert.Equal("bash w", Encoding.ASCII.GetString(mbbsEmuMemoryCore.GetString("INPUT", true)));
        }
    }
}
