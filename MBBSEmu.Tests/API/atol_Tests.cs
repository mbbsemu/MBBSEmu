using MBBSEmu.CPU;
using MBBSEmu.Extensions;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace MBBSEmu.Tests.API
{
    public class atol_Tests : APITestBase
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
            executeAPITest(ATOL_ORDINAL, core =>
            {
                core.Push(DATA_SEGMENT);
                core.Push(0);

                return input.GetNullTerminatedBytes();
            });
            
            //Verify Results
            Assert.Equal((ushort)(expectedValue & 0xFFFF), mbbsEmuCpuRegisters.AX);
            Assert.Equal((ushort)(expectedValue >> 16), mbbsEmuCpuRegisters.DX);
        }
    }
}
