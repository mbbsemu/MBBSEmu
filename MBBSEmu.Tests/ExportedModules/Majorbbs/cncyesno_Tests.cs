using MBBSEmu.Memory;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class cncyesno_Tests : MajorbbsTestBase
    {

        private const int CNCYESNO_ORDINAL = 131;

        [Theory]
        [InlineData("YES", 'Y', 4)]
        [InlineData("Y", 'Y', 2)]
        [InlineData("Y TEST", 'Y', 2)]
        [InlineData("YES THIS IS A TEST", 'Y', 4)]
        [InlineData("TEST YES", '\0', 0)]
        [InlineData("TEST", '\0', 0)]
        [InlineData("NO", 'N', 3)]
        [InlineData("N", 'N', 2)]
        [InlineData("N TEST", 'N', 2)]
        [InlineData("NO THIS IS A TEST", 'N', 3)]
        [InlineData("TEST NO", '\0', 0)]
        
        public void CNCYESNO_Test(string inputString, char expectedResult, ushort expectedNxtcmdOffset)
        {
            //Reset State
            Reset();

            //Set Input Values
            mbbsModule.Memory.SetArray("INPUT", Encoding.ASCII.GetBytes(inputString.Replace(' ', '\0')));
            mbbsModule.Memory.SetWord("INPLEN", (ushort)inputString.Length);

            //Execute Test
            ExecuteApiTest(CNCYESNO_ORDINAL, new List<IntPtr16>());

            //Verify Results
            var expectedResultPointer = mbbsEmuMemoryCore.GetVariablePointer("INPUT");
            expectedResultPointer.Offset += expectedNxtcmdOffset;
            Assert.Equal(expectedResult, mbbsEmuCpuCore.Registers.AX);
            Assert.Equal(expectedResultPointer, mbbsEmuMemoryCore.GetPointer("NXTCMD"));
        }
    }
}
