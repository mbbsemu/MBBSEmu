using MBBSEmu.Memory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class parsin_Tests : MajorbbsTestBase
    {
        private const ushort PARSIN_ORDINAL = 467;

        [Theory]
        [InlineData("TEST1 TEST2 TEST3\0", 3, 18)]
        [InlineData("\0", 0, 0)]
        [InlineData("A B\0", 2, 4)]
        [InlineData("A TEST B\0", 3, 9)]
        public void parsin_Test(string inputCommand, int expectedMargc, int expectedInputLength)
        {
            //Reset State
            Reset();

            //Set Input Values
            mbbsEmuMemoryCore.SetArray("INPUT", Encoding.ASCII.GetBytes(inputCommand));
            mbbsEmuMemoryCore.SetWord("INPLEN", (ushort)inputCommand.Length);

            ExecuteApiTest(PARSIN_ORDINAL, new List<IntPtr16>());

            //Verify Results
            var actualInputLength = mbbsEmuMemoryCore.GetWord("INPLEN");
            var actualMargc = mbbsEmuMemoryCore.GetWord("MARGC");

            Assert.Equal(expectedMargc, actualMargc); //Verify Correct Number of Commands Parsed
            Assert.Equal(expectedInputLength, actualInputLength); //Verify Length is Correct
            Assert.True(Encoding.ASCII.GetBytes(inputCommand.Replace(' ', '\0'))
                .SequenceEqual(mbbsEmuMemoryCore.GetArray("INPUT", (ushort) inputCommand.Length).ToArray())); //Verify spaces replaced with nulls

            //Verify Argument Addresses

            //Replicate parsin() by replacing space with nulls, then get string components
            var inputComponents = inputCommand.Replace(' ', '\0').Split('\0', StringSplitOptions.RemoveEmptyEntries);

            //Get Argument Starting Points
            var margvPointer = mbbsEmuMemoryCore.GetVariablePointer("MARGV");
            var margnPointer = mbbsEmuMemoryCore.GetVariablePointer("MARGN");

            for (var i = 0; i < expectedMargc; i++)
            {
                var currentMargvPointer = mbbsEmuMemoryCore.GetPointer(margvPointer.Segment, (ushort)(margvPointer.Offset + (ushort)(i * IntPtr16.Size)));
                var currentMargnPointer = mbbsEmuMemoryCore.GetPointer(margnPointer.Segment, (ushort)(margnPointer.Offset + (ushort)(i * IntPtr16.Size)));

                //Since we split the input on null, we'll strip null from the string
                var currentArg = Encoding.ASCII.GetString(mbbsEmuMemoryCore.GetString(currentMargvPointer, true));

                Assert.Equal(inputComponents[i], currentArg);
                Assert.Equal(inputComponents[i].Length, currentMargnPointer.Offset - currentMargvPointer.Offset);
            }
        }
    }
}
