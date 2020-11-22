using System.Collections.Generic;
using System.Text;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class itoa_Tests : ExportedModuleTestBase
    {
        private const int ITOA_ORDINAL = 366;

        [Theory]
        [InlineData(4, "STRDST", 10, "4")]
        [InlineData(500, "STRDST", 10, "500")]
        [InlineData(8500, "STRDST", 8, "20464")]
        [InlineData(8500, "STRDST", 16, "2134")]
        [InlineData(5, "STRDST", 2, "101")]
        [InlineData(1000, "STRDST", 16, "3e8")]
        [InlineData(1000, "STRDST", 2, "1111101000")]
        public void itoa_Test(ushort intConvert, string strDestination, ushort baseInt, string expected)
        {
            //Reset State
            Reset();

            //Set Argument Values to be Passed In
            var destinationStringPointer = mbbsEmuMemoryCore.AllocateVariable("STRDST", (ushort)(expected.Length + 1));
            mbbsEmuMemoryCore.SetArray("STRDST", Encoding.ASCII.GetBytes(strDestination));

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
            Assert.Equal(expected, Encoding.ASCII.GetString(mbbsEmuMemoryCore.GetString("STRDST", true)));
        }
    }
}
