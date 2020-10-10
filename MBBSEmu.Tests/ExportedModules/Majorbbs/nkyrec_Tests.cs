using MBBSEmu.Memory;
using System.Collections.Generic;
using Xunit;
using System.Text;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class nkyrec_Tests : ExportedModuleTestBase
    {
        private const int NKYREC_ORDINAL = 432;

        [Theory]
        [InlineData("TEST")]
        public void NKYREC_Test(string inputString)
        {
            //Reset State
            Reset();

            //Set Argument Values to be Passed In
            var stringPointer = mbbsEmuMemoryCore.AllocateVariable("INPUT_STRING", (ushort)(inputString.Length + 1));
            mbbsEmuMemoryCore.SetArray("INPUT_STRING", Encoding.ASCII.GetBytes(inputString));

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, NKYREC_ORDINAL, new List<IntPtr16> { stringPointer });
        }
    }
}
