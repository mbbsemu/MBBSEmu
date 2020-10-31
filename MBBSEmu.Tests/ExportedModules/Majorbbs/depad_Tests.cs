using System.Collections.Generic;
using System.Text;
using MBBSEmu.Memory;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class depad_Tests : ExportedModuleTestBase
    {
        private const int DEPAD_ORDINAL = 164;

        [Theory]
        [InlineData("%   ", 3)]
        [InlineData("T    ", 4)]
        [InlineData("T ", 1)]
        [InlineData("TeSt  ", 2)]
        [InlineData("TeSt", 0)]
        [InlineData("", 0)]
        public void DEPAD_Test(string inputString, ushort expected)
        {
            //Reset State
            Reset();

            var stringPointer = mbbsEmuMemoryCore.AllocateVariable("INPUT_STRING", (ushort)(inputString.Length + 1));
            mbbsEmuMemoryCore.SetArray("INPUT_STRING", Encoding.ASCII.GetBytes(inputString));

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, DEPAD_ORDINAL, new List<IntPtr16> { stringPointer });

            //Verify Results
            Assert.Equal(expected, mbbsEmuCpuRegisters.AX);
        }
    }
}
