using System.Collections.Generic;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class memset_Tests : ExportedModuleTestBase
    {
        private const int MEMSET_ORDINAL = 411;

        [Theory]
        [InlineData(new byte[] { 0x4, 0x4 }, 0x3, 2, new byte[] { 0x3, 0x3 })]
        [InlineData(new byte[] { 0x4, 0x4, 0x3, 0x2 }, 0x3, 4, new byte[] { 0x3, 0x3, 0x3, 0x3 })]
        [InlineData(new byte[] { 0x4, 0x4, 0x3, 0x2, 0x1, 0x0 }, 0xE, 6, new byte[] { 0xE, 0xE, 0xE, 0xE, 0xE, 0xE })]
        [InlineData(new byte[] { 0x4 }, 0x0, 1, new byte[] { 0x0 })]
        [InlineData(new byte[] { 0xE, 0xF, 0x2, 0x8 }, 0xA, 4, new byte[] { 0xA, 0xA, 0xA, 0xA })]
        public void memset_Test(byte[] bufMem, ushort valueToFill, ushort memSize, byte[] expected)
        {
            //Reset State
            Reset();

            //Set Argument Values to be Passed In
            var bufPointer = mbbsEmuMemoryCore.AllocateVariable("SETMEMORY", (ushort)bufMem.Length);
            mbbsEmuMemoryCore.SetArray(bufPointer, bufMem);

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment,
                MEMSET_ORDINAL,
                new List<ushort>
                {
                    bufPointer.Offset,
                    bufPointer.Segment,
                    valueToFill,
                    memSize
                });

            //Verify Results
            var dstArray = mbbsEmuMemoryCore.GetArray("SETMEMORY", memSize);

            Assert.Equal(expected, dstArray.ToArray());
        }
    }
}
