using System.Collections.Generic;
using System.Text;
using MBBSEmu.Memory;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class rmvwht_Tests : ExportedModuleTestBase
    {
        private const int RMVWHT_ORDINAL = 501;

        [Theory]
        [InlineData("upper case", "uppercase\0")]
        [InlineData("   capitalize me", "capitalizeme\0")]
        [InlineData("lower case words", "lowercasewords\0")]
        [InlineData("  ---&&&^^^ hello", "---&&&^^^hello\0")]
        [InlineData("       hi", "hi\0")]
        public void RMVWHT_Test(string inputString, string expectedString)
        {
            //Reset State
            Reset();

            var stringPointer = mbbsEmuMemoryCore.AllocateVariable("INPUT_STRING", (ushort)(inputString.Length + 1));
            mbbsEmuMemoryCore.SetArray("INPUT_STRING", Encoding.ASCII.GetBytes(inputString));

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, RMVWHT_ORDINAL, new List<IntPtr16> { stringPointer });

            //Verify Results
            Assert.Equal(expectedString, Encoding.ASCII.GetString(mbbsEmuMemoryCore.GetString("INPUT_STRING")));
        }
    }
}
