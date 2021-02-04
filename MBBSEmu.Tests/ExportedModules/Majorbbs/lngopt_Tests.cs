using MBBSEmu.Memory;
using MBBSEmu.Module;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class lngopt_Tests : ExportedModuleTestBase
    {
        private const int LNGOPT_ORDINAL = 389;

        [Theory]
        [InlineData(65520.5, 65520, 65521 )]
        [InlineData(2147418110.5, 2147418110, 2147418111)]
        [InlineData(-2147418110.5, -2147418111, -2147418110)]
        [InlineData(-1147418000.3, -1147418001, -1147418000)]
        public void lngopt_Test(int inputValue, int inputFloor, int inputCeiling)
        {
            //Reset State
            Reset();

            //Set Argument Values to be Passed In
            var mcvPointer = (ushort)majorbbs.McvPointerDictionary.Allocate(new McvFile("TEST.MCV",
                new Dictionary<int, byte[]> { { 0, Encoding.ASCII.GetBytes(inputValue.ToString()) } }));

            mbbsEmuMemoryCore.SetPointer("CURRENT-MCV", new FarPtr(0xFFFF, mcvPointer));

            var inputFloorLow = (ushort) inputFloor;
            var inputFloorHigh = (ushort) (inputFloor >> 16);

            var inputCeilingLow = (ushort) inputCeiling;
            var inputCeilingHigh = (ushort) (inputCeiling >> 16);

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, LNGOPT_ORDINAL, new List<ushort> { 0, inputFloorLow, inputFloorHigh, inputCeilingLow, inputCeilingHigh });

            //Verify Results
            Assert.Equal(inputValue, mbbsEmuCpuRegisters.GetLong());
        }
    }
}