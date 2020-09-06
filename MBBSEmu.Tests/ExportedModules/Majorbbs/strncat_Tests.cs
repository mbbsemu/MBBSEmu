using MBBSEmu.Memory;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class strncat_Tests : MajorbbsTestBase
    {
        private const int STRCAT_ORDINAL = 580;

        [Theory]
        [InlineData("TEST1 ", "TEST1", 32, "TEST1 TEST1")]
        [InlineData("", "TEST1", 32, "TEST1")]
        [InlineData("TEST1", "", 32, "TEST1")]
        [InlineData("", "", 32, "")]
        [InlineData("TEST1 ", "TEST1", 0, "TEST1 ")]
        [InlineData("", "TEST1", 0, "")]
        [InlineData("TEST1", "", 0, "TEST1")]
        [InlineData("", "", 0, "")]
        [InlineData("FunTest", "OfStrncat", 5, "FunTestOfStr")]
        public void strncat_Test(string destination, string src, ushort length, string expected)
        {
            //Reset State
            Reset();

            //Set Argument Values to be Passed In
            var destinationStringPointer = mbbsEmuMemoryCore.AllocateVariable("DST", (ushort)(destination.Length + length + 1));
            mbbsEmuMemoryCore.SetArray("DST", Encoding.ASCII.GetBytes(destination));

            var sourceStringPointer = mbbsEmuMemoryCore.AllocateVariable("SRC", (ushort)(src.Length + 1));
            mbbsEmuMemoryCore.SetArray("SRC", Encoding.ASCII.GetBytes(src));

            //Execute Test
            ExecuteApiTest(STRCAT_ORDINAL, new List<ushort> { destinationStringPointer.Offset, destinationStringPointer.Segment, sourceStringPointer.Offset, sourceStringPointer.Segment, length });

            //Verify Results
            Assert.Equal(destinationStringPointer.Segment, mbbsEmuCpuRegisters.DX);
            Assert.Equal(destinationStringPointer.Offset, mbbsEmuCpuRegisters.AX);
            Assert.Equal(expected, Encoding.ASCII.GetString(mbbsEmuMemoryCore.GetString("DST", true)));
        }
    }
}
