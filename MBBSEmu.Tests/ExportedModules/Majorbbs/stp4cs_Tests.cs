using MBBSEmu.Memory;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class stp4cs_Tests : ExportedModuleTestBase
    {
        private const int STP4CS_ORDINAL = 960;

        [Theory]
        [InlineData("\x01TEST", "TEST")]
        [InlineData("\x21TEST", "\x21TEST")]
        [InlineData("\x7ETEST", "\x7ETEST")]
        [InlineData("TEST\x01", "TEST")]
        [InlineData("TEST\x21", "TEST\x21")]
        [InlineData("TEST\x7E", "TEST\x7E")]
        public void spt4cs_Test(string inputString, string expectedString)
        {
            //Reset State
            Reset();

            //Set Argument Values to be Passed In
            var stringPointer = mbbsEmuMemoryCore.AllocateVariable("INPUT_STRING", (ushort)(inputString.Length + 1));
            mbbsEmuMemoryCore.SetArray("INPUT_STRING", Encoding.ASCII.GetBytes(inputString));

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, STP4CS_ORDINAL, new List<IntPtr16> { stringPointer });

            //Verify Results
            Assert.Equal(stringPointer.Offset, mbbsEmuCpuRegisters.AX);
            Assert.Equal(stringPointer.Segment, mbbsEmuCpuRegisters.DX);
            Assert.Equal(expectedString,
                Encoding.ASCII.GetString(
                    mbbsEmuMemoryCore.GetString(mbbsEmuCpuRegisters.GetPointer(), true)));
        }
    }
}
