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
        [InlineData("A", 65)]
        [InlineData("R", 82)]
        [InlineData("", 0)]
        [InlineData("!", 33)]
        [InlineData("z", 122)]
        public void chropt_Test(string input, ushort expectedValue)
        {
            //Reset State
            Reset();

            //Set Argument Values to be Passed In
            var mcvPointer = (ushort)majorbbs.McvPointerDictionary.Allocate(new McvFile("TEST.MCV",
                new Dictionary<int, byte[]> { { 0, Encoding.ASCII.GetBytes(input) } }));

            mbbsEmuMemoryCore.SetPointer("CURRENT-MCV", new FarPtr(0xFFFF, mcvPointer));

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, CHROPT_ORDINAL, new List<ushort> { 0 });

            //Verify Results
            Assert.Equal(expectedValue, mbbsEmuCpuRegisters.AX);
        }
    }
}
