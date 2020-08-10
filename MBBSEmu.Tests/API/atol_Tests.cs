using System.Collections.Generic;
using System.Text;
using MBBSEmu.Extensions;
using MBBSEmu.HostProcess.ExportedModules;
using MBBSEmu.Memory;
using Xunit;

namespace MBBSEmu.Tests.API
{
    public class atol_Tests : APITestBase
    {
        private static readonly ushort LIBRARY_SEGMENT = Majorbbs.Segment;
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
            var inputStringPointer = mbbsEmuMemoryCore.AllocateVariable("STRING1", (ushort) (input.Length + 1));
            mbbsEmuMemoryCore.SetArray(inputStringPointer, Encoding.ASCII.GetBytes(input));

            executeAPITest(LIBRARY_SEGMENT, ATOL_ORDINAL, new List<IntPtr16> { inputStringPointer}); 
            
            //Verify Results
            Assert.Equal((ushort)(expectedValue & 0xFFFF), mbbsEmuCpuRegisters.AX);
            Assert.Equal((ushort)(expectedValue >> 16), mbbsEmuCpuRegisters.DX);
        }
    }
}
