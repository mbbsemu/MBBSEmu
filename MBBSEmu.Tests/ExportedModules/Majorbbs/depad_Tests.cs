using MBBSEmu.Memory;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public partial class Majorbbs_Tests
    {
        private const int DEPAD_ORDINAL = 164;

        [Theory]
        [InlineData("%   ", 3, "%\0")]
        [InlineData("T    ", 4, "T\0")]
        [InlineData("T ", 1, "T\0")]
        [InlineData("TeSt  ", 2, "TeSt\0")]
        [InlineData("TeSt", 0, "TeSt\0")]
        [InlineData("", 0, "\0")]
        public void DEPAD_Test(string inputString, ushort expected, string expectedString)
        {
            //Reset State
            Reset();

            var stringPointer = mbbsEmuMemoryCore.AllocateVariable("INPUT_STRING", (ushort)(inputString.Length + 1));
            mbbsEmuMemoryCore.SetArray("INPUT_STRING", Encoding.ASCII.GetBytes(inputString));

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, DEPAD_ORDINAL, new List<FarPtr> { stringPointer });

            //Verify Results
            Assert.Equal(expected, mbbsEmuCpuRegisters.AX);
            Assert.Equal(expectedString, Encoding.ASCII.GetString(mbbsEmuMemoryCore.GetString("INPUT_STRING")));
        }
    }
}
