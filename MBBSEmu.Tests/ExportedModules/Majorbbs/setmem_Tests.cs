using System;
using System.Collections.Generic;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class setmem_Tests : ExportedModuleTestBase
    {
        private const int SETMEM_ORDINAL = 544;

        [Theory]
        [InlineData(new byte[] { 0x4, 0x4 }, 2, 0x3)]
        [InlineData(new byte[] { 0x4, 0x4, 0x3, 0x2 }, 4, 0x3)]
        [InlineData(new byte[] { 0x4, 0x4, 0x3, 0x2, 0x1, 0x0 }, 6, 0xE)]
        [InlineData(new byte[] { 0x4 }, 1, 0x0)]
        [InlineData(new byte[] { }, 0, 0x0)]
        [InlineData(new byte[] { 0xE, 0xF, 0x2, 0x8 }, 4, 0xA)]
        public void setmem_Test(byte[] bufMem, ushort numBytes, ushort valueToFill)
        {
            //Reset State
            Reset();

            //Set Argument Values to be Passed In
            var bufPointer = mbbsEmuMemoryCore.AllocateVariable("SETMEMORY", (ushort)bufMem.Length);
            mbbsEmuMemoryCore.SetArray(bufPointer, bufMem);

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment,
                SETMEM_ORDINAL,
                new List<ushort>
                {
                    bufPointer.Offset,
                    bufPointer.Segment,
                    numBytes,
                    valueToFill
                });

            //Verify Results
            var expected = new byte[numBytes];
            Array.Fill(expected, (byte)valueToFill);

            var dstArray = mbbsEmuMemoryCore.GetArray(bufPointer, numBytes);

            Assert.Equal(expected, dstArray.ToArray());
        }
    }
}
