using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public partial class Majorbbs_Tests
    {
        private const int STLCPY_ORDINAL = 959;

        [Theory]
        [InlineData(1, "T", "T")]
        [InlineData(0, "T", "")]
        [InlineData(0, "\0", "")]
        [InlineData(0, "", "")]
        [InlineData(4, "TestPhrase\0", "Test")]
        [InlineData(8, "Test\0", "Test\0\0\0\0")]
        [InlineData(5, "Test\0", "Test\0")]
        [InlineData(8, "TestTes\0", "TestTes\0")]
        public void stlcpy_Test(ushort dstChars, string srcString, string expected)
        {
            //Reset State
            Reset();

            //Set Argument Values to be Passed In
            var destinationStringPointer = mbbsEmuMemoryCore.AllocateVariable("DST", dstChars);

            var sourceStringPointer = mbbsEmuMemoryCore.AllocateVariable("SRC", (ushort)(srcString.Length));
            mbbsEmuMemoryCore.SetArray("SRC", Encoding.ASCII.GetBytes(srcString));

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, STLCPY_ORDINAL,
                new List<ushort>
                {
                    destinationStringPointer.Offset,
                    destinationStringPointer.Segment,
                    sourceStringPointer.Offset,
                    sourceStringPointer.Segment,
                    dstChars
                });

            //Verify Results
            Assert.Equal(expected.ToArray(), Encoding.ASCII.GetChars(mbbsEmuMemoryCore.GetArray(destinationStringPointer, dstChars).ToArray()).ToArray());
            Assert.Equal(destinationStringPointer.Segment, mbbsEmuCpuRegisters.DX);
            Assert.Equal(destinationStringPointer.Offset, mbbsEmuCpuRegisters.AX);
        }
    }
}
