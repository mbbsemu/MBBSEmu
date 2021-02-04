using MBBSEmu.Memory;
using MBBSEmu.Module;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class ynopt_Tests : ExportedModuleTestBase
    {
        private const int YNOPT_ORDINAL = 650;

        [Theory]
        [InlineData(" Yes", true)]
        [InlineData(" No", false)]
        [InlineData(" ", false)]
        public void ynopt_Test(string msgValue, bool msgBool)
        {
            //Reset State
            Reset();

            //Set Argument Values to be Passed In
            var mcvPointer = (ushort)majorbbs.McvPointerDictionary.Allocate(new McvFile("TEST.MCV",
                new Dictionary<int, byte[]> { { 0, Encoding.ASCII.GetBytes(msgValue) } }));

            mbbsEmuMemoryCore.SetPointer("CURRENT-MCV", new FarPtr(0xFFFF, mcvPointer));

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, YNOPT_ORDINAL, new List<ushort> { 0 });

            //Verify Results
            Assert.Equal(msgBool, bool.Parse(mbbsEmuCpuRegisters.AX.ToString()));
        }
    }
}
