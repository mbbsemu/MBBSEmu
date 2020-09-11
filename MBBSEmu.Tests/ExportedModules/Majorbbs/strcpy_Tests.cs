using MBBSEmu.Memory;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class strcpy_Tests : ExportedModuleTestBase
    {
        private const int STRCPY_ORDINAL = 574;

        [Theory]
        [InlineData("TEST1\0", "TEST1\0")]
        [InlineData("TEST1\0TEST2\0", "TEST1\0")]
        [InlineData("\0", "\0")]
        [InlineData("", "\0")]
        public void strcpy_Test(string sourceString, string expectedDestination)
        {
            //Reset State
            Reset();

            //Set Argument Values to be Passed In
            var destinationStringPointer = mbbsEmuMemoryCore.AllocateVariable("DESTINATION_STRING", 0xFF);
            mbbsEmuMemoryCore.SetArray("DESTINATION_STRING", Encoding.ASCII.GetBytes(new string('X', 0xFF)));

            var sourceStringPointer = IntPtr16.Empty;
            if (!string.IsNullOrEmpty(sourceString))
            {
                sourceStringPointer = mbbsEmuMemoryCore.AllocateVariable("SOURCE_STRING", (ushort)(sourceString.Length + 1));
                mbbsEmuMemoryCore.SetArray("SOURCE_STRING", Encoding.ASCII.GetBytes(sourceString));
            }


            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, STRCPY_ORDINAL, new List<IntPtr16> { destinationStringPointer, sourceStringPointer });

            //Verify Results
            Assert.Equal(expectedDestination, Encoding.ASCII.GetString(mbbsEmuMemoryCore.GetString("DESTINATION_STRING")));
        }
    }
}
