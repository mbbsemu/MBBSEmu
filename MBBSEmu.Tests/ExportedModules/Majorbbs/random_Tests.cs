using System.Collections.Generic;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class random_Tests : ExportedModuleTestBase
    {
        private const int GENRDN_ORDINAL = 315;
        private const int LNGRND_ORDINAL = 390;
        private const int RAND_ORDINAL = 486;

        [Theory]
        [InlineData(2, 2)]
        [InlineData(1, 3)]
        [InlineData(5, 20)]
        [InlineData(2500, 10000)]
        [InlineData(10000, 65000)]
        [InlineData(1, 6000)]
        public void genrdnTest(ushort valueMin, ushort valueMax)
        {
            Reset();

            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, GENRDN_ORDINAL, new List<ushort> { valueMin, valueMax });

            //Verify Results
            Assert.InRange(mbbsEmuCpuRegisters.AX,valueMin, valueMax);
        }

        [Fact]
        public void genrdnTest_invalid()
        {
            Reset();

            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, GENRDN_ORDINAL, new List<ushort> { 10, 2 });

            var expectedValue = 10;

            //Verify Results
            Assert.Equal(mbbsEmuCpuRegisters.AX, expectedValue);
        }

        [Theory]
        [InlineData(1, 1000000)]
        [InlineData(10000, 80000)]
        [InlineData(10, 32000)]
        [InlineData(32767, 2500000)]
        [InlineData(70000, 100000)]
        public void lngrndTest(int valueMin, int valueMax)
        {
            Reset();

            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, LNGRND_ORDINAL, new List<ushort> { (ushort)(valueMin & 0xFFFF), (ushort)(valueMin >> 16), (ushort)(valueMax & 0xFFFF), (ushort)(valueMax >> 16) });

            //Verify Results
            Assert.InRange(mbbsEmuCpuRegisters.GetLong(), valueMin, valueMax);
        }

        [Fact]
        public void lngrndTest_invalid()
        {
            Reset();

            var valueMin = 1000000;
            var valueMax = 16000;
            var expectedValue = 1000000;

            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, LNGRND_ORDINAL, new List<ushort> { (ushort)(valueMin & 0xFFFF), (ushort)(valueMin >> 16), (ushort)(valueMax & 0xFFFF), (ushort)(valueMax >> 16) });

            //Verify Results
            Assert.Equal(mbbsEmuCpuRegisters.GetLong(), expectedValue);
        }
    }
}
