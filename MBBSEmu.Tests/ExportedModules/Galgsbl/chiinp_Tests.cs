using System.Collections.Generic;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Galgsbl
{
    public class chiinp_Tests : ExportedModuleTestBase
    {
        private const ushort CHIINP_ORDINAL = 62;

        [Fact]
        public void chinp_Test()
        {
            Reset();

            ExecuteApiTest(HostProcess.ExportedModules.Galgsbl.Segment, CHIINP_ORDINAL, new List<ushort> { 0, 'X' });

            //Verify Results
            Assert.Equal(1, testSessions[0].InputBuffer.Length);
            Assert.Equal(new[] { (byte)'X' }, testSessions[0].InputBuffer.ToArray());
        }
    }
}
