using System.Collections.Generic;
using System.Text;
using MBBSEmu.Memory;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class atol_Tests : ExportedModuleTestBase
    {
        private const int ATOL_ORDINAL = 77;

        [Theory]
        [InlineData("123", 123)]
        [InlineData("-123", -123)]
        [InlineData("+123", 123)]
        [InlineData("   -123", -123)]
        [InlineData("   +123", 123)]
        [InlineData("   -123    ", -123)]
        [InlineData("   +123    ", 123)]
        [InlineData("0", 0)]
        [InlineData("crap", 0)]
        [InlineData("   crap", 0)]
        [InlineData("2147483647", 2147483647)]
        [InlineData("-2147483648", 2147483648)]
        [InlineData("4000000000", 0)]
        [InlineData("-4000000000", 0)]
        public void ATOLTest(string input, long expectedValue)
        {
            //Reset State
            Reset();

            //Allocate Variables to be Passed In
            var inputStringPointer = mbbsEmuMemoryCore.AllocateVariable("STRING", (ushort) (input.Length + 1));
            mbbsEmuMemoryCore.SetArray(inputStringPointer, Encoding.ASCII.GetBytes(input));

            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, ATOL_ORDINAL, new List<FarPtr> { inputStringPointer});

            //Verify Results
            Assert.Equal((ushort)(expectedValue & 0xFFFF), mbbsEmuCpuRegisters.AX);
            Assert.Equal((ushort)(expectedValue >> 16), mbbsEmuCpuRegisters.DX);
        }
    }
}
