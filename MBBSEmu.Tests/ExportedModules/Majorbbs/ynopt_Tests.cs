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
        private const int YNOPT_ORDINAL = 650;

        [Theory]
        [InlineData(" Yes", 1, false)]
        [InlineData(" No", 0, false)]
        [InlineData(" ", 0, true)]
        [InlineData("", 0, true)]
        public void ynopt_Test(string msgValue, ushort expectedValue, bool shouldThrowException)
        {
            //Reset State
            Reset();

            //Set Argument Values to be Passed In
            var mcvPointer = (ushort)majorbbs.McvPointerDictionary.Allocate(new McvFile("TEST.MCV",
                new Dictionary<int, byte[]> { { 0, Encoding.ASCII.GetBytes(msgValue) } }));

            mbbsEmuMemoryCore.SetPointer("CURRENT-MCV", new FarPtr(0xFFFF, mcvPointer));

            //Execute Test
            try
            {
                ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, YNOPT_ORDINAL, new List<ushort> { 0 });
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
