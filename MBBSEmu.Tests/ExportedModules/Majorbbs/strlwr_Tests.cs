using MBBSEmu.Memory;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class strlwr_Tests : ExportedModuleTestBase
    {
        private const int STRLWR_ORDINAL = 579;

        [Theory]
        [InlineData("TEST", "test")]
        [InlineData("TEST1", "test1")]
        [InlineData("TEST test TEST", "test test test")]
        [InlineData("TeSt", "test")]
        public void STRLWE_Test(string inputString, string expectedString)
        {
            //Reset State
            Reset();

            //Set Argument Values to be Passed In
            var stringPointer = mbbsEmuMemoryCore.AllocateVariable("INPUT_STRING", (ushort)(inputString.Length + 1));
            mbbsEmuMemoryCore.SetArray("INPUT_STRING", Encoding.ASCII.GetBytes(inputString));

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, STRLWR_ORDINAL, new List<FarPtr> { stringPointer });

            //Verify Results
            Assert.Equal(stringPointer.Offset, mbbsEmuCpuRegisters.AX);
            Assert.Equal(stringPointer.Segment, mbbsEmuCpuRegisters.DX);
            Assert.Equal(expectedString,
                Encoding.ASCII.GetString(
                    mbbsEmuMemoryCore.GetString(mbbsEmuCpuRegisters.GetPointer(), true)));
        }

        /// <summary>
        ///     Every byte from 0x01 to 0xFF: the original kernels' STRLWR (The Major BBS 6.x and Worldgroup
        ///     MAJORBBS.EXE, executed on the same 255 bytes) changes only the 26 ASCII letters, whatever the culture
        ///     the emulator runs under (the Turkish culture maps 'i' and 'I' to letters outside ASCII).
        /// </summary>
        [Theory]
        [InlineData("")]
        [InlineData("tr-TR")]
        public void STRLWR_ChangesOnlyAsciiLetters(string cultureName)
        {
            var previousCulture = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = cultureName.Length == 0 ? CultureInfo.InvariantCulture : new CultureInfo(cultureName);

                Reset();

                var input = new byte[256];
                for (var i = 1; i < 256; i++)
                    input[i - 1] = (byte)i;

                var stringPointer = mbbsEmuMemoryCore.AllocateVariable("INPUT_STRING", (ushort)input.Length);
                mbbsEmuMemoryCore.SetArray("INPUT_STRING", input);

                ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, STRLWR_ORDINAL, new List<FarPtr> { stringPointer });

                var expected = new byte[255];
                for (var i = 0; i < 255; i++)
                {
                    var b = input[i];
                    expected[i] = b is >= (byte)'A' and <= (byte)'Z' ? (byte)(b + 0x20) : b;
                }

                Assert.Equal(expected, mbbsEmuMemoryCore.GetArray(stringPointer, 255).ToArray());
            }
            finally
            {
                CultureInfo.CurrentCulture = previousCulture;
            }
        }
    }
}
