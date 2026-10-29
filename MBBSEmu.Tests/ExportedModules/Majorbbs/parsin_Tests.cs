using MBBSEmu.Memory;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class parsin_Tests : ExportedModuleTestBase
    {
        private const ushort PARSIN_ORDINAL = 467;

        //Expected values follow parsin() in MAJORBBS.C, where only the first space after each word becomes a null
        [Theory]
        [InlineData("TEST1 TEST2 TEST3\0", "TEST1\0TEST2\0TEST3", 17)]
        [InlineData("\0", "", 0)]
        [InlineData("A B\0", "A\0B", 3)]
        [InlineData("A TEST B\0", "A\0TEST\0B", 8)]
        [InlineData("A      TEST       B\0", "A\0     TEST\0      B", 19)]
        [InlineData("A   TEST                        B\0", "A\0  TEST\0                       B", 33)]
        [InlineData("TEST \0", "TEST", 4)]
        [InlineData("  LOOK  \0", "  LOOK", 6)]
        [InlineData("   \0", "   ", 0)]
        public void parsin_Test(string inputCommand, string expectedParsedInput, int expectedInputLength)
        {
            //Reset State
            Reset();

            //Set Input Values
            mbbsEmuMemoryCore.SetArray("INPUT", Encoding.ASCII.GetBytes(inputCommand));
            mbbsEmuMemoryCore.SetWord("INPLEN", (ushort)inputCommand.Replace("\0", string.Empty).Length);

            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, PARSIN_ORDINAL, new List<FarPtr>());

            //Verify Results
            var words = inputCommand.TrimEnd('\0').Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
            var actualInput = mbbsEmuMemoryCore.GetArray("INPUT", (ushort)expectedParsedInput.Length).ToArray();

            Assert.Equal(words.Length, mbbsEmuMemoryCore.GetWord("MARGC"));
            Assert.Equal(expectedInputLength, mbbsEmuMemoryCore.GetWord("INPLEN"));
            Assert.Equal(Encoding.ASCII.GetBytes(expectedParsedInput), actualInput);

            //Get Argument Starting Points
            var margvPointer = mbbsEmuMemoryCore.GetVariablePointer("MARGV");
            var margnPointer = mbbsEmuMemoryCore.GetVariablePointer("MARGN");

            for (var i = 0; i < words.Length; i++)
            {
                var currentMargvPointer = mbbsEmuMemoryCore.GetPointer(margvPointer + i * FarPtr.Size);
                var currentMargnPointer = mbbsEmuMemoryCore.GetPointer(margnPointer + i * FarPtr.Size);

                Assert.Equal(words[i], Encoding.ASCII.GetString(mbbsEmuMemoryCore.GetString(currentMargvPointer, true)));
                Assert.Equal(words[i].Length, currentMargnPointer.Offset - currentMargvPointer.Offset);
                Assert.Equal(0, mbbsEmuMemoryCore.GetByte(currentMargnPointer));
            }

            //With no words, margv[0] is an empty string
            if (words.Length == 0)
                Assert.Equal(0, mbbsEmuMemoryCore.GetByte(mbbsEmuMemoryCore.GetPointer(margvPointer)));
        }

        [Fact]
        public void parsin_shorter_input_sets_inplen_Test()
        {
            //Reset State
            Reset();

            //A module copies a short command over a longer input, then parses it
            mbbsEmuMemoryCore.SetArray("INPUT", Encoding.ASCII.GetBytes("wield shimmering greatsword\0"));
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, PARSIN_ORDINAL, new List<FarPtr>());
            Assert.Equal(27, mbbsEmuMemoryCore.GetWord("INPLEN"));

            mbbsEmuMemoryCore.SetArray("INPUT", Encoding.ASCII.GetBytes("bash w\0"));
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, PARSIN_ORDINAL, new List<FarPtr>());

            Assert.Equal(2, mbbsEmuMemoryCore.GetWord("MARGC"));
            Assert.Equal(6, mbbsEmuMemoryCore.GetWord("INPLEN"));
        }
    }
}
