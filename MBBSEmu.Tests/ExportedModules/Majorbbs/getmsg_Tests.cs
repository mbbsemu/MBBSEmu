using MBBSEmu.Memory;
using MBBSEmu.Module;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class getmsg_Tests : ExportedModuleTestBase
    {
        private const int GETMSG_ORDINAL = 326;

        [Fact]
        public void getmsg_over1000_Test()
        {
            //Reset State
            Reset();

            var input = string.Concat(Enumerable.Repeat("ab", 3000));

            var mcvPointer = (ushort)majorbbs.McvPointerDictionary.Allocate(new McvFile("TEST.MCV",
                new Dictionary<int, byte[]> { { 0, Encoding.ASCII.GetBytes(input) } }));

            mbbsEmuMemoryCore.SetPointer("CURRENT-MCV", new FarPtr(0xFFFF, mcvPointer));

            //Verify Results
            Assert.Throws<Exception>(() => ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, GETMSG_ORDINAL, new List<ushort> { 0 }));
        }
    }
}
