using System.Collections.Generic;
using System.Text;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class vsprintf_ReturnValue_Tests : ExportedModuleTestBase
    {
        private const int VSPRINTF_ORDINAL = 643;

        /// <summary>
        ///     vsprintf() returns the number of characters written, not counting the null terminator.
        ///     Trade Wars 2002 appends to prfbuf with "prfptr += vsprintf(prfptr, ...)", so counting
        ///     the terminator left a null in prfbuf after every piece of output.
        /// </summary>
        [Theory]
        [InlineData("Sector %d", "Sector 42")]
        [InlineData("No arguments", "No arguments")]
        [InlineData("", "")]
        public void vsprintf_ReturnsLengthWithoutNull_Test(string formatString, string expectedString)
        {
            Reset();

            var destBuffer = mbbsEmuMemoryCore.Malloc((ushort)(expectedString.Length + 8));
            mbbsEmuMemoryCore.SetArray(destBuffer, Encoding.ASCII.GetBytes(new string('X', expectedString.Length + 8)));

            var formatStringPointer = mbbsEmuMemoryCore.Malloc((ushort)(formatString.Length + 1));
            mbbsEmuMemoryCore.SetArray(formatStringPointer, Encoding.ASCII.GetBytes(formatString + "\0"));

            var argumentsPointer = mbbsEmuMemoryCore.Malloc(2);
            mbbsEmuMemoryCore.SetWord(argumentsPointer, 42);

            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, VSPRINTF_ORDINAL, new List<ushort>
            {
                destBuffer.Offset, destBuffer.Segment,
                formatStringPointer.Offset, formatStringPointer.Segment,
                argumentsPointer.Offset, argumentsPointer.Segment
            });

            Assert.Equal(expectedString.Length, mbbsEmuCpuRegisters.AX);
            Assert.Equal(expectedString, Encoding.ASCII.GetString(mbbsEmuMemoryCore.GetString(destBuffer, true)));
            Assert.Equal(0, mbbsEmuMemoryCore.GetByte(destBuffer + expectedString.Length));
        }
    }
}
