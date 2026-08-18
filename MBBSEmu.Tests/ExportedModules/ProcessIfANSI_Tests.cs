using Xunit;

namespace MBBSEmu.Tests.ExportedModules
{
    /// <summary>
    ///     Regression coverage for ExportedModuleBase.ProcessIfANSI. A buffer that ends
    ///     mid-sequence (a lone ESC, a truncated CSI prefix "ESC[", or a lone "~") must
    ///     not read past the end of the input span. Before the bounds guard a buffer
    ///     ending in the two bytes "ESC[" threw IndexOutOfRangeException.
    /// </summary>
    public class ProcessIfANSI_Tests : ExportedModuleTestBase
    {
        [Theory]
        // standalone ESC at end of buffer (CP437 left-arrow glyph, preserved as-is)
        [InlineData(new byte[] { 0x1B })]
        // truncated CSI prefix (ESC[) at end of buffer — the over-read case
        [InlineData(new byte[] { 0x1B, 0x5B })]
        // a complete, normal CSI passes through untouched
        [InlineData(new byte[] { 0x1B, 0x5B, (byte)'1', (byte)';', (byte)'3', (byte)'1', (byte)'m' })]
        // plain text with a lone '~' at the end (the ~~ escape guard)
        [InlineData(new byte[] { (byte)'H', (byte)'i', (byte)'~' })]
        public void ProcessIfANSI_TruncatedOrPlainAnsi_PassesThroughWithoutOverread(byte[] input)
        {
            Reset();

            var result = galgsbl.ProcessIfANSI(input).ToArray();

            Assert.Equal(input, result);
        }

        [Fact]
        public void ProcessIfANSI_CompleteIfAnsiSequence_KeepsAnsiComponent()
        {
            Reset();

            // A properly terminated IF-ANSI sequence: ESC[[<ansi>|<non-ansi>]. The
            // non-ANSI alternative terminates on ']', not '|'.
            var input = new byte[]
            {
                0x1B, 0x5B, 0x5B,                                        // ESC[[
                0x1B, 0x5B, (byte)'3', (byte)'1', (byte)'m',            // ansi:      ESC[31m
                (byte)'|', (byte)'X', (byte)']'                         // |non-ansi]
            };

            var result = galgsbl.ProcessIfANSI(input).ToArray();

            // The ANSI component is preserved; the non-ANSI alternative is dropped.
            Assert.Equal(new byte[] { 0x1B, 0x5B, (byte)'3', (byte)'1', (byte)'m' }, result);
        }

        [Fact]
        public void ProcessIfANSI_IfAnsiSegmentStartingWithDelimiter_DoesNotReadBeforeStart()
        {
            Reset();

            // An IF-ANSI segment whose first byte is a delimiter (~, |, ]) previously read
            // substringSpan[j - 1] with j == 0 and threw. It must be treated as an
            // unescaped (empty) segment instead.
            var input = new byte[] { 0x1B, 0x5B, 0x5B, (byte)'~' };     // ESC[[~

            var result = galgsbl.ProcessIfANSI(input).ToArray();

            Assert.Empty(result);
        }
    }
}
