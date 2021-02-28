using System.Collections.Generic;
using System.Text;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public partial class Majorbbs_Tests
    {
        private const int ITOA_ORDINAL = 366;

        [Theory]
        [InlineData(4, 10, "4")]
        [InlineData(500, 10, "500")]
        [InlineData(8500, 8, "20464")]
        [InlineData(8500, 16, "2134")]
        [InlineData(5, 2, "101")]
        [InlineData(1000, 16, "3e8")]
        [InlineData(1000, 2, "1111101000")]
        public void itoa_Test(ushort intConvert, ushort baseInt, string expected)
        {
            //Reset State
            Reset();

            //Set Argument Values to be Passed In
            var destinationStringPointer = mbbsEmuMemoryCore.AllocateVariable("STRDST", (ushort)(expected.Length + 1));
            mbbsEmuMemoryCore.SetArray("STRDST", Encoding.ASCII.GetBytes("STRDST"));

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, ITOA_ORDINAL,
                new List<ushort>
                {
                    intConvert,
                    destinationStringPointer.Offset,
                    destinationStringPointer.Segment,
                    baseInt
                });

            //Verify Results
            Assert.Equal(destinationStringPointer.Segment, mbbsEmuCpuRegisters.DX);
            Assert.Equal(destinationStringPointer.Offset, mbbsEmuCpuRegisters.AX);
            Assert.Equal(expected, Encoding.ASCII.GetString(mbbsEmuMemoryCore.GetString("STRDST", true)));
        }
    }
}
