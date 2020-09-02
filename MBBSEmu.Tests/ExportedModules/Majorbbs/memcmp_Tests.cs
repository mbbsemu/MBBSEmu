using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class memcmp_Tests : MajorbbsTestBase
    {
        private const int MEMCMP_ORDINAL = 408;

        [Theory]
        [InlineData(new byte[] {}, new byte[] {}, 0, 0)]
        [InlineData(new byte[] {(byte)0x1, (byte) 0x2}, new byte[] {(byte) 0x3, (byte) 0x4}, 2, 0xFFFE)]
        [InlineData(new byte[] {(byte)0x4, (byte) 0x4}, new byte[] {(byte) 0x2, (byte) 0x2}, 2, 2)]
        [InlineData(new byte[] {(byte)0x4, (byte) 0x4}, new byte[] {(byte) 0x4, (byte) 0x2}, 1, 0)]
        public void compareTest(byte[] buf1, byte[] buf2, ushort length, ushort expected)
        {
            //Reset State
            Reset();

            //Set Argument Values to be Passed In
            var buf1Pointer = mbbsEmuMemoryCore.AllocateVariable("BUF1", (ushort) buf1.Length);
            mbbsEmuMemoryCore.SetArray(buf1Pointer, buf1);

            var buf2Pointer = mbbsEmuMemoryCore.AllocateVariable("BUF2", (ushort) buf2.Length);
            mbbsEmuMemoryCore.SetArray(buf2Pointer, buf2);

            //Execute Test
            ExecuteApiTest(MEMCMP_ORDINAL, new List<ushort> { buf1Pointer.Offset, buf1Pointer.Segment, buf2Pointer.Offset, buf2Pointer.Segment, length });

            Assert.Equal(expected, mbbsEmuCpuRegisters.AX);
        }
    }
}
