using Xunit;
using MBBSEmu.Extensions;
using System;

namespace MBBSEmu.Tests.Extensions
{
    /// <summary>
    ///     Unit Tests for MBBSEmu's Extension Class for ReadOnlySpan
    /// </summary>
    public class ReadOnlySpanExtensionsTests
    {
        [Fact]
        public void ToCharSpanTest()
        {
            ReadOnlySpan<byte> input = new byte[] { 65, 66, 67 };
            ReadOnlySpan<char> expected = new char[] { 'A', 'B', 'C' };
            var result = input.ToCharSpan();
            Assert.Equal(expected.ToArray(), result.ToArray());
        }

        [Fact]
        public void ContainsOnlyTest()
        {
            ReadOnlySpan<byte> input = new byte[] { 1, 1, 1 };
            var result = input.ContainsOnly(1);
            Assert.True(result);
        }

        [Fact]
        public void ContainsOnlyTest_False()
        {
            ReadOnlySpan<byte> input = new byte[] { 1, 2, 1 };
            var result = input.ContainsOnly(1);
            Assert.False(result);
        }

        [Fact]
        public void ToHexStringTest()
        {
            ReadOnlySpan<byte> input = new byte[] { 1, 2, 3, 4, 5 };
            var result = input.ToHexString(0, 5);
            Assert.DoesNotContain("No Data to Display", result);
            Assert.DoesNotContain("Invalid Address Range", result);
        }

        [Fact]
        public void ToHexStringTest_ZeroLength()
        {
            ReadOnlySpan<byte> input = new byte[] { 1, 2, 3, 4, 5 };
            var result = input.ToHexString(0, 0);
            Assert.Contains("No Data to Display", result);
        }

        [Fact]
        public void ToHexStringTest_InvalidLength()
        {
            ReadOnlySpan<byte> input = new byte[] { 1, 2, 3, 4, 5 };
            var result = input.ToHexString(0, 6);
            Assert.Contains("Invalid Address Range", result);
        }
    }
}