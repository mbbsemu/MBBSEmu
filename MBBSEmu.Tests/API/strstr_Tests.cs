using MBBSEmu.HostProcess.ExportedModules;
using MBBSEmu.Memory;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace MBBSEmu.Tests.API
{
    public class strstr_Tests : APITestBase
    {
        private static readonly ushort LIBRARY_SEGMENT = Majorbbs.Segment;
        private const int STRSTR_ORDINAL = 584;

        [Theory]
        [InlineData("test", "test", 0)]
        [InlineData("abctest", "test", 3)]
        public void STRSTR_Test(string string1, string string2, long expectedOrdinal)
        {
            //Reset State
            Reset();

            //Set Argument Values to be Passed In
            var string1Pointer = mbbsEmuMemoryCore.AllocateVariable("STRING1", (ushort)(string1.Length + 1));
            mbbsEmuMemoryCore.SetArray("STRING1", Encoding.ASCII.GetBytes(string1));
            var string2Pointer = mbbsEmuMemoryCore.AllocateVariable("STRING2", (ushort)(string2.Length + 1));
            mbbsEmuMemoryCore.SetArray("STRING2", Encoding.ASCII.GetBytes(string2));

            //Execute Test
            executeAPITest(LIBRARY_SEGMENT, STRSTR_ORDINAL, new List<IntPtr16> {string1Pointer, string2Pointer});

            //Verify Results
            Assert.Equal(string1Pointer.Offset + expectedOrdinal, mbbsEmuCpuRegisters.AX);
            Assert.Equal(string1Pointer.Segment, mbbsEmuCpuRegisters.DX);
            
        }
    }
}
