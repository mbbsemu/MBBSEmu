using System.Collections.Generic;
using System.Text;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Galgsbl
{
    public class btuimp_Tests : ExportedModuleTestBase
    {
        private const int BTUIMP_ORDINAL = 22;

        [Fact]
        public void btuimp_Test()
        {
            Reset();

            //Allocate Variables to be Passed In
            var outputStringPointer = mbbsEmuMemoryCore.AllocateVariable("STRING", 0xFF);

            //Set Session Input Buffer
            testSessions[0].InputBuffer.Write(Encoding.ASCII.GetBytes("Test Input String"));

            ExecuteApiTest(HostProcess.ExportedModules.Galgsbl.Segment, BTUIMP_ORDINAL, new List<ushort> { 0, outputStringPointer.Offset, outputStringPointer.Segment});

            //Verify Results
            var resultString = Encoding.ASCII.GetString(mbbsEmuMemoryCore.GetString(outputStringPointer, true));
            Assert.Equal("Test Input String", resultString);
            Assert.Equal(resultString.Length, mbbsEmuCpuRegisters.AX);
            Assert.Equal(0, testSessions[0].InputBuffer.Length);
        }
    }
}
