using MBBSEmu.Memory;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public partial class Majorbbs_Tests
    {
        private const int ALCDUP_ORDINAL = 64;

        [Theory]
        [InlineData("TEST1\0", "TEST1\0")]
        [InlineData("TEST1\0TEST2\0", "TEST1\0")]
        [InlineData("TEST TEST test\0", "TEST TEST test\0")]
        [InlineData("\0", "\0")]
        [InlineData("", "\0")]
        [InlineData(null, "\0")]
        public void alcdup_Test(string sourceString, string expectedString)
        {
            //Reset State
            Reset();

            //Set Argument Values to be Passed In
            var sourceStringPointer = FarPtr.Empty;
            if (sourceString != null)
            {
                sourceStringPointer = mbbsEmuMemoryCore.AllocateVariable("SOURCE_STRING", (ushort)(sourceString.Length + 1));
                mbbsEmuMemoryCore.SetArray("SOURCE_STRING", Encoding.ASCII.GetBytes(sourceString));
            }


            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, ALCDUP_ORDINAL, new List<FarPtr> { sourceStringPointer });

            //Verify Results
            Assert.Equal(expectedString, Encoding.ASCII.GetString(mbbsEmuMemoryCore.GetString(mbbsEmuCpuRegisters.GetPointer())));
        }
    }
}
