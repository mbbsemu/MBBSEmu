using MBBSEmu.Memory;
using MBBSEmu.Module;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public partial class Majorbbs_Tests
    {
        private const int NUMOPT_ORDINAL = 441;

        [Theory]
        [InlineData("1000", 1000, ushort.MinValue, ushort.MaxValue, false)]
        [InlineData("32000", 32000, 0, ushort.MaxValue, false)]
        [InlineData("0", 0, 0, ushort.MaxValue, false)]
        [InlineData("", 0, 0, ushort.MaxValue, true)]
        [InlineData("-500", 0, 16, ushort.MaxValue, true)]
        [InlineData("-3000", 0, 5000, 5000, true)]
        public void numopt_Test(string input, short expectedValue, ushort min, ushort max, bool shouldThrowException)
        {
            //Reset State
            Reset();

            var mcvPointer = (ushort)majorbbs.McvPointerDictionary.Allocate(new McvFile("TEST.MCV",
                new Dictionary<int, byte[]> { { 0, Encoding.ASCII.GetBytes(input) } }));

            mbbsEmuMemoryCore.SetPointer("CURRENT-MCV", new FarPtr(0xFFFF, mcvPointer));
            try
            {
                ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, NUMOPT_ORDINAL, new List<ushort> { 0, min, max });
            }
            catch (Exception)
            {
                Assert.True(shouldThrowException);
            }

            //Verify Results
            Assert.Equal((ushort)expectedValue, mbbsEmuCpuRegisters.AX);
        }
    }
}
