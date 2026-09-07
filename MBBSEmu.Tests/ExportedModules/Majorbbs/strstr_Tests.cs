using MBBSEmu.Memory;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class strstr_Tests : ExportedModuleTestBase
    {
        private const int STRSTR_ORDINAL = 584;

        [Theory]
        [InlineData("test", "test", 0)]
        [InlineData("abctest", "test", 3)]
        [InlineData("abctest", "not_found", -1)]
        [InlineData("abctest", "Test", -1)]
        [InlineData("abctesttest", "test", 3)]
        public void STRSTR_Test(string string1, string string2, long expectedOffset)
        {
            //Reset State
            Reset();

            //Set Argument Values to be Passed In
            var string1Pointer = mbbsEmuMemoryCore.AllocateVariable("STRING1", (ushort)(string1.Length + 1));
            mbbsEmuMemoryCore.SetArray("STRING1", Encoding.ASCII.GetBytes(string1));
            var string2Pointer = mbbsEmuMemoryCore.AllocateVariable("STRING2", (ushort)(string2.Length + 1));
            mbbsEmuMemoryCore.SetArray("STRING2", Encoding.ASCII.GetBytes(string2));

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, STRSTR_ORDINAL, new List<FarPtr> { string1Pointer, string2Pointer });

            //Verify Results
            if (expectedOffset < 0)
            {
                Assert.Equal(0, mbbsEmuCpuRegisters.AX);
                Assert.Equal(0, mbbsEmuCpuRegisters.DX);
            }
            else
            {
                Assert.Equal(string1Pointer.Offset + expectedOffset, mbbsEmuCpuRegisters.AX);
                Assert.Equal(string1Pointer.Segment, mbbsEmuCpuRegisters.DX);
            }
        }

        [Fact]
        public void STRSTR_FindsNonAsciiNeedle()
        {
            //Reset State
            Reset();

            var haystackPointer = mbbsEmuMemoryCore.AllocateVariable("STRING1", 6);
            mbbsEmuMemoryCore.SetArray("STRING1", new byte[] { (byte)'a', (byte)'b', 0xAD, (byte)'c', (byte)'d', 0x0 });

            var needlePointer = mbbsEmuMemoryCore.AllocateVariable("STRING2", 2);
            mbbsEmuMemoryCore.SetArray("STRING2", new byte[] { 0xAD, 0x0 });

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, STRSTR_ORDINAL, new List<FarPtr> { haystackPointer, needlePointer });

            //Verify Results
            Assert.Equal(haystackPointer.Offset + 2, mbbsEmuCpuRegisters.AX);
            Assert.Equal(haystackPointer.Segment, mbbsEmuCpuRegisters.DX);
        }

        [Fact]
        public void STRSTR_DoesNotFalsePositiveOnDifferentNonAsciiBytes()
        {
            //Reset State
            Reset();

            // 0x81 and 0xA5 are both invalid single-byte UTF-8 sequences, and both get
            // replaced with '?' if decoded via Encoding.ASCII/UTF8 -- a needle containing
            // 0xA5 must not "find" a haystack byte of 0x81.
            var haystackPointer = mbbsEmuMemoryCore.AllocateVariable("STRING1", 4);
            mbbsEmuMemoryCore.SetArray("STRING1", new byte[] { (byte)'a', 0x81, (byte)'b', 0x0 });

            var needlePointer = mbbsEmuMemoryCore.AllocateVariable("STRING2", 2);
            mbbsEmuMemoryCore.SetArray("STRING2", new byte[] { 0xA5, 0x0 });

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, STRSTR_ORDINAL, new List<FarPtr> { haystackPointer, needlePointer });

            //Verify Results
            Assert.Equal(0, mbbsEmuCpuRegisters.AX);
            Assert.Equal(0, mbbsEmuCpuRegisters.DX);
        }
    }
}
