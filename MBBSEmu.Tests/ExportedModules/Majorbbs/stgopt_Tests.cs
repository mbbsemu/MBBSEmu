using MBBSEmu.Memory;
using MBBSEmu.Module;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public partial class Majorbbs_Tests
    {
        private const int STGOPT_ORDINAL = 566;

        [Theory]
        [InlineData("Normal")]
        [InlineData("")]
        [InlineData("123456")]
        [InlineData("--==---")]
        [InlineData("!@)#!*$")]
        public void stgopt_Test(string msgValue)
        {
            //Reset State
            Reset();

            //Set Argument Values to be Passed In
            var mcvPointer = (ushort)majorbbs.McvPointerDictionary.Allocate(new McvFile("TEST.MCV",
                new Dictionary<int, byte[]> { { 0, Encoding.ASCII.GetBytes(msgValue) } }));

            mbbsEmuMemoryCore.SetPointer("CURRENT-MCV", new FarPtr(0xFFFF, mcvPointer));

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, STGOPT_ORDINAL, new List<ushort> { 0 });

            //Verify Results
            Assert.Equal(msgValue, Encoding.ASCII.GetString(mbbsEmuMemoryCore.GetString(mbbsEmuCpuRegisters.DX, mbbsEmuCpuRegisters.AX, true)));
        }
    }
}
