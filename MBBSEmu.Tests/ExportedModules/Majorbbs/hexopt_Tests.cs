using MBBSEmu.Memory;
using MBBSEmu.Module;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class hexopt_Tests : ExportedModuleTestBase
    {
        private const int HEXOPT_ORDINAL = 339;

        [Theory]
        [InlineData("FF", 255, 0, 256, false)]
        [InlineData("FFFF", ushort.MaxValue, 0, ushort.MaxValue, false)]
        [InlineData("0", 0, 0, ushort.MaxValue, false)]
        [InlineData("F", 15, 0, ushort.MaxValue, false)]
        [InlineData("F", 0, 16, ushort.MaxValue, true)] //min test
        [InlineData("F", 0, 0, 1, true)] //max test
        public void hexopt_Test(string input, ushort expectedValue, ushort min, ushort max, bool shouldThrowException)
        {
            //Reset State
            Reset();

            var mcvPointer = (ushort)majorbbs.McvPointerDictionary.Allocate(new McvFile("TEST.MCV",
                new Dictionary<int, byte[]> { { 0, Encoding.ASCII.GetBytes(input) } }));

            mbbsEmuMemoryCore.SetPointer("CURRENT-MCV", new FarPtr(0xFFFF, mcvPointer));
            try
            {
                ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, HEXOPT_ORDINAL, new List<ushort> { 0, min, max });
            }
            catch (Exception)
            {
                Assert.True(shouldThrowException);
            }

            //Verify Results
            Assert.Equal(expectedValue, mbbsEmuCpuRegisters.AX);
        }
    }
}
