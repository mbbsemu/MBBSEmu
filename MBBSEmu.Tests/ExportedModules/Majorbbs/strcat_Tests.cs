using MBBSEmu.Memory;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public partial class Majorbbs_Tests
    {
        private const int STRCAT_ORDINAL = 571;

        [Theory]
        [InlineData("TEST1 ", "TEST1", "TEST1 TEST1")]
        [InlineData("", "TEST1", "TEST1")]
        [InlineData("TEST1", "", "TEST1")]
        [InlineData("", "", "")]
        public void strcat_Test(string destination, string src, string expected)
        {
            //Reset State
            Reset();

            //Set Argument Values to be Passed In
            var destinationStringPointer = mbbsEmuMemoryCore.AllocateVariable("DST", (ushort)(destination.Length + src.Length + 1));
            mbbsEmuMemoryCore.SetArray("DST", Encoding.ASCII.GetBytes(destination));

            var sourceStringPointer = mbbsEmuMemoryCore.AllocateVariable("SRC", (ushort)(src.Length + 1));
            mbbsEmuMemoryCore.SetArray("SRC", Encoding.ASCII.GetBytes(src));

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, STRCAT_ORDINAL, new List<FarPtr> { destinationStringPointer, sourceStringPointer });

            //Verify Results
            Assert.Equal(destinationStringPointer.Segment, mbbsEmuCpuRegisters.DX);
            Assert.Equal(destinationStringPointer.Offset, mbbsEmuCpuRegisters.AX);
            Assert.Equal(expected, Encoding.ASCII.GetString(mbbsEmuMemoryCore.GetString("DST", true)));
        }
    }
}
