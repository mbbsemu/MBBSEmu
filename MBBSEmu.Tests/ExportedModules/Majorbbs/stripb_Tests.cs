using System.Collections.Generic;
using System.Text;
using MBBSEmu.Memory;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class stripb_Tests : ExportedModuleTestBase
    {
        private const int STRIPB_ORDINAL = 577;

        [Theory]
        [InlineData("T", "T\0")]
        [InlineData("T          ", "T\0")]
        [InlineData("TEST test TEST\0", "TEST test TEST\0")]
        [InlineData("TeSt \0", "TeSt\0")]
        public void STRIPB_Test(string inputString, string expectedString)
        {
            //Reset State
            Reset();

            var stringPointer = mbbsEmuMemoryCore.AllocateVariable("INPUT_STRING", (ushort)(inputString.Length + 1));
            mbbsEmuMemoryCore.SetArray("INPUT_STRING", Encoding.ASCII.GetBytes(inputString));

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, STRIPB_ORDINAL, new List<IntPtr16> { stringPointer });

            //Verify Results
            Assert.Equal(expectedString, Encoding.ASCII.GetString(mbbsEmuMemoryCore.GetString("INPUT_STRING")));
        }
    }
}
