using System;
using System.IO;
using Xunit;
using Xunit.Sdk;

namespace MBBSEmu.Tests.Util
{
    public class GoldenAssert_Tests : IDisposable
    {
        private readonly string _goldenPath = Path.Combine(Path.GetTempPath(), $"golden_{Guid.NewGuid():N}.bin");

        public void Dispose()
        {
            File.Delete(_goldenPath);
        }

        [Fact]
        public void MatchesGolden_IdenticalBytes_Passes()
        {
            var data = new byte[] { 0x1B, 0x5B, 0x32, 0x4A, 0xB0, 0xB1, 0xB2, 0x00, 0xFF };
            File.WriteAllBytes(_goldenPath, data);

            GoldenAssert.MatchesGolden(data, _goldenPath);
        }

        [Fact]
        public void MatchesGolden_DifferentByte_FailsWithOffsetAndHexDump()
        {
            File.WriteAllBytes(_goldenPath, new byte[] { 0x41, 0x42, 0x43, 0x44 });

            var ex = Assert.Throws<XunitException>(() =>
                GoldenAssert.MatchesGolden(new byte[] { 0x41, 0x42, 0x99, 0x44 }, _goldenPath));

            Assert.Contains("offset 0x2", ex.Message);
            Assert.Contains("expected 0x43", ex.Message);
            Assert.Contains("actual 0x99", ex.Message);
        }

        [Fact]
        public void MatchesGolden_LengthMismatch_Fails()
        {
            File.WriteAllBytes(_goldenPath, new byte[] { 0x41, 0x42 });

            var ex = Assert.Throws<XunitException>(() =>
                GoldenAssert.MatchesGolden(new byte[] { 0x41, 0x42, 0x43 }, _goldenPath));

            Assert.Contains("Length differs", ex.Message);
        }

        [Fact]
        public void MatchesGolden_VolatileSpanMasked_Passes()
        {
            //Golden has a date at offset 2..9; actual differs only there
            File.WriteAllBytes(_goldenPath, System.Text.Encoding.ASCII.GetBytes("On 08/17/26 hi"));
            var actual = System.Text.Encoding.ASCII.GetBytes("On 12/25/99 hi");

            GoldenAssert.MatchesGolden(actual, _goldenPath, new[] { (3, 8) });
        }

        [Fact]
        public void MatchesGolden_MissingGoldenFile_Fails()
        {
            Assert.Throws<XunitException>(() =>
                GoldenAssert.MatchesGolden(new byte[] { 0x41 }, _goldenPath + ".missing"));
        }

        [Fact]
        public void ContainsNone_CleanStream_Passes()
        {
            GoldenAssert.ContainsNone(new byte[] { 0x41, 0x42, 0x1B, 0xB0 }, 0x11, 0x12, 0x13, 0x14);
        }

        [Fact]
        public void ContainsNone_ForbiddenBytesPresent_ReportsCountsAndOffsets()
        {
            var data = new byte[] { 0x41, 0x13, 0x42, 0x13, 0x14 };

            var ex = Assert.Throws<XunitException>(() =>
                GoldenAssert.ContainsNone(data, 0x11, 0x12, 0x13, 0x14));

            Assert.Contains("0x13 found 2x", ex.Message);
            Assert.Contains("0x14 found 1x", ex.Message);
            Assert.DoesNotContain("0x11 found", ex.Message);
        }
    }
}
