using MBBSEmu.Memory;
using MBBSEmu.Module;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class chropt_Tests : ExportedModuleTestBase
    {
        private const int CHROPT_ORDINAL = 107;

        [Theory]
        [InlineData(new byte[] { 38 })]
        [InlineData(new byte[] { 44 })]
        [InlineData(new byte[] { 0 })]
        [InlineData(new byte[] { 56 })]
        [InlineData(new byte[] { 79 })]
        public void chropt_Test(byte[] msgValue)
        {
            //Reset State
            Reset();

            //Set Argument Values to be Passed In
            var mcvPointer = (ushort)majorbbs.McvPointerDictionary.Allocate(new McvFile("TEST.MCV",
                new Dictionary<int, byte[]> { { 0, msgValue } }));

            mbbsEmuMemoryCore.SetPointer("CURRENT-MCV", new FarPtr(0xFFFF, mcvPointer));

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, CHROPT_ORDINAL, new List<ushort> { 0 });

            //Verify Results
            Assert.Equal(msgValue[0], mbbsEmuCpuRegisters.AX);
        }
    }
}
