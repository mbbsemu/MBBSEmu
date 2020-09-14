using MBBSEmu.Module;
using System.Collections.Generic;
using System.Text;
using MBBSEmu.Memory;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class hexopt_Tests : ExportedModuleTestBase
    {
        private const int HEXOPT_ORDINAL = 339;

        [Theory]
        [InlineData("FF", 255, 0, 256)]
        [InlineData("FFFF", ushort.MaxValue, 0, ushort.MaxValue)]
        [InlineData("0", 0, 0, ushort.MaxValue)]
        [InlineData("F", 15, 0, ushort.MaxValue)]
        public void hexopt_Test(string input, ushort expectedValue, ushort min, ushort max)
        {
            //Reset State
            Reset();

            var mcvPointer = (ushort)majorbbs.McvPointerDictionary.Allocate(new McvFile("TEST.MCV",
                new Dictionary<int, byte[]> {{0, Encoding.ASCII.GetBytes(input)}}));

            mbbsEmuMemoryCore.SetPointer("CURRENT-MCV", new IntPtr16(0xFFFF, mcvPointer));

            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, HEXOPT_ORDINAL, new List<ushort> { 0, min, max });

            //Verify Results
            Assert.Equal(expectedValue, mbbsEmuCpuRegisters.AX);
        }
    }
}
