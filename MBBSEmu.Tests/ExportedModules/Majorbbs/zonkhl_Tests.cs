using System.Collections.Generic;
using System.Text;
using MBBSEmu.Memory;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class zonkhl_Tests : ExportedModuleTestBase
    {
        private const int ZONKHL_ORDINAL = 652;

        [Theory]
        [InlineData("upper case", "Upper Case\0")]
        [InlineData("   capitalize me", "   Capitalize Me\0")]
        [InlineData("lower case words", "Lower Case Words\0")]
        [InlineData("  ---&&&^^^ hello", "  ---&&&^^^ Hello\0")]
        [InlineData("       hi", "       Hi\0")]
        public void ZONKHL_Test(string inputString, string expectedString)
        {
            //Reset State
            Reset();

            var stringPointer = mbbsEmuMemoryCore.AllocateVariable("INPUT_STRING", (ushort)(inputString.Length + 1));
            mbbsEmuMemoryCore.SetArray("INPUT_STRING", Encoding.ASCII.GetBytes(inputString));

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, ZONKHL_ORDINAL, new List<IntPtr16> { stringPointer });

            //Verify Results
            Assert.Equal(expectedString, Encoding.ASCII.GetString(mbbsEmuMemoryCore.GetString("INPUT_STRING")));
        }
    }
}
