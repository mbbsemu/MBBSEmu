using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MBBSEmu.Memory;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class stzcat_Tests : ExportedModuleTestBase
    {
        private const int STZCAT_ORDINAL = 905;

        [Theory]
        [InlineData("12345", "12345", 7, "123451\0")]
        [InlineData("12345", "12345", 4, "123\0")]
        [InlineData("12345", "12345", 0, "")]
        [InlineData("12345", "12345", 11, "1234512345\0")]
        [InlineData("12345", "12345", 16, "1234512345\0\0\0\0\0\0")]
        [InlineData("", "", 0, "")]
        public void stzcat_Test(string destination, string src, ushort dstLength, string expected)
        {
            //Reset State
            Reset();

            //Set Argument Values to be Passed In
            var destinationStringPointer = mbbsEmuMemoryCore.AllocateVariable("DST", (ushort)dstLength);
            mbbsEmuMemoryCore.FillArray(destinationStringPointer, (ushort)dstLength, 0xFF);

            mbbsEmuMemoryCore.SetArray(destinationStringPointer, Encoding.ASCII.GetBytes(destination));
            mbbsEmuMemoryCore.SetByte(destinationStringPointer + destination.Length, 0);

            var sourceStringPointer = mbbsEmuMemoryCore.AllocateVariable("SRC", (ushort)(src.Length + 1));
            mbbsEmuMemoryCore.SetArray(sourceStringPointer, Encoding.ASCII.GetBytes(src));
            mbbsEmuMemoryCore.SetByte(sourceStringPointer + src.Length, 0);

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, STZCAT_ORDINAL, new List<ushort> { destinationStringPointer.Offset, destinationStringPointer.Segment, sourceStringPointer.Offset, sourceStringPointer.Segment, dstLength });

            //Verify Results
            Assert.Equal(destinationStringPointer.Segment, mbbsEmuCpuRegisters.DX);
            Assert.Equal(destinationStringPointer.Offset, mbbsEmuCpuRegisters.AX);
            Assert.Equal(expected, Encoding.ASCII.GetString(mbbsEmuMemoryCore.GetArray("DST", dstLength)));
        }
    }
}
