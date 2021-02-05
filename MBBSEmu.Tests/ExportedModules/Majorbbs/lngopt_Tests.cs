using MBBSEmu.Memory;
using MBBSEmu.Module;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class lngopt_Tests : ExportedModuleTestBase
    {
        private const int LNGOPT_ORDINAL = 389;

        [Theory]
        [InlineData("65520", 65520, 65515, 65525, false)]
        [InlineData("2147418110", 2147418110, int.MinValue, int.MaxValue, false)]
        [InlineData("-2147418110", -2147418110, int.MinValue, 0, false)]
        [InlineData("-1147418000", -1147418000, -1147418005, -1147418000, false)]
        [InlineData("500", 0, 1000, 25000, true)]
        [InlineData("", 0, 1000, 25000, true)]
        public void lngopt_Test(string inputValue, int expectedValue, int inputFloor, int inputCeiling, bool shouldThrowException)
        {
            //Reset State
            Reset();

            //Set Argument Values to be Passed In
            var inputFloorLow = (ushort)inputFloor;
            var inputFloorHigh = (ushort)(inputFloor >> 16);

            var inputCeilingLow = (ushort)inputCeiling;
            var inputCeilingHigh = (ushort)(inputCeiling >> 16);

            var mcvPointer = (ushort)majorbbs.McvPointerDictionary.Allocate(new McvFile("TEST.MCV",
                new Dictionary<int, byte[]> { { 0, Encoding.ASCII.GetBytes(inputValue) } }));

            mbbsEmuMemoryCore.SetPointer("CURRENT-MCV", new FarPtr(0xFFFF, mcvPointer));
            //Execute Test
            try
            {
                ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, LNGOPT_ORDINAL, new List<ushort> { 0, inputFloorLow, inputFloorHigh, inputCeilingLow, inputCeilingHigh });
            }
            catch (Exception)
            {
                Assert.True(shouldThrowException);
            }

            //Verify Results
            Assert.Equal(expectedValue, mbbsEmuCpuRegisters.GetLong());
        }
    }
}