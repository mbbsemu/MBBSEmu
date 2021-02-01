using System.Collections.Generic;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class alcmem_Tests : ExportedModuleTestBase
    {
        private const int ALCMEM_ORDINAL = 65;

        [Theory]
        [InlineData(3)]
        [InlineData(72)]
        [InlineData(45)]
        [InlineData(0)]
        public void alcmem_Test(ushort numBytes)
        {
            //Reset State
            Reset();

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, ALCMEM_ORDINAL, new List<ushort> { numBytes });

            //Verify Results
            var expected = new byte[numBytes];

            for (var i = 0; i < numBytes; i++)
                expected[i] = 0x0;

            var dstArray = mbbsEmuMemoryCore.GetArray(mbbsEmuCpuRegisters.GetPointer(), numBytes);

            //Verify Results
            Assert.Equal(expected, dstArray.ToArray());
        }
    }
}
