using MBBSEmu.Memory;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class skpwrd_Tests : ExportedModuleTestBase
    {
        private const int SKPWRD_ORDINAL = 554;

        [Theory]
        [InlineData("testing 123", " 123")]
        [InlineData(" test", " test")]
        [InlineData("\0test", "")]
        public void SKPWRD_Test(string inputString, string expectedString)
        {
            //Reset State
            Reset();

            //Set Argument Values to be Passed In
            var stringPointer = mbbsEmuMemoryCore.AllocateVariable("INPUT_STRING", (ushort)(inputString.Length + 1));
            mbbsEmuMemoryCore.SetArray("INPUT_STRING", Encoding.ASCII.GetBytes(inputString));

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, SKPWRD_ORDINAL, new List<IntPtr16> { stringPointer });

            //Verify Results
            Assert.Equal(expectedString,
                Encoding.ASCII.GetString(
                    mbbsEmuMemoryCore.GetString(mbbsEmuCpuRegisters.GetPointer(), true)));
        }
    }
}
