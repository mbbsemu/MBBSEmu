﻿using MBBSEmu.Memory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class parsin_Tests : ExportedModuleTestBase
    {
        private const ushort PARSIN_ORDINAL = 467;

        [Theory]
        [InlineData("TEST1 TEST2 TEST3\0", 3, 17)]
        [InlineData("\0", 0, 0)]
        [InlineData("A B\0", 2, 3)]
        [InlineData("A TEST B\0", 3, 8)]
        [InlineData("A      TEST       B\0", 3, 19)]
        [InlineData("A   TEST                        B\0", 3, 33)]
        [InlineData("TEST \0", 1, 5)]
        public void parsin_Test(string inputCommand, int expectedMargc, int expectedInputLength)
        {
            //Reset State
            Reset();

            //Set Input Values
            mbbsEmuMemoryCore.SetArray("INPUT", Encoding.ASCII.GetBytes(inputCommand));
            mbbsEmuMemoryCore.SetWord("INPLEN", (ushort)inputCommand.Replace("\0", string.Empty).Length);

            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, PARSIN_ORDINAL, new List<FarPtr>());

            //Verify Results
            var expectedParsedInput = Encoding.ASCII.GetBytes(inputCommand.Replace(' ', '\0')[..expectedInputLength]);
            var actualInputLength = mbbsEmuMemoryCore.GetWord("INPLEN");
            var actualMargc = mbbsEmuMemoryCore.GetWord("MARGC");

            var actualInput = mbbsEmuMemoryCore.GetArray("INPUT", actualInputLength).ToArray();

            Assert.Equal(expectedMargc, actualMargc); //Verify Correct Number of Commands Parsed
            Assert.Equal(expectedInputLength, actualInputLength); //Verify Length is Correct
            Assert.True(expectedParsedInput
                .SequenceEqual(actualInput)); //Verify spaces replaced with nulls


            //Replicate parsin() by replacing space with nulls, then get string components
            var inputComponents = inputCommand.Replace(' ', '\0').Split('\0', StringSplitOptions.RemoveEmptyEntries);

            //Get Argument Starting Points
            var margvPointer = mbbsEmuMemoryCore.GetVariablePointer("MARGV");
            var margnPointer = mbbsEmuMemoryCore.GetVariablePointer("MARGN");

            for (var i = 0; i < expectedMargc; i++)
            {
                var currentMargvPointer = mbbsEmuMemoryCore.GetPointer(margvPointer.Segment, (ushort)(margvPointer.Offset + (ushort)(i * FarPtr.Size)));
                var currentMargnPointer = mbbsEmuMemoryCore.GetPointer(margnPointer.Segment, (ushort)(margnPointer.Offset + (ushort)(i * FarPtr.Size)));

                //Since we split the input on null, we'll strip null from the string
                var currentArg = Encoding.ASCII.GetString(mbbsEmuMemoryCore.GetString(currentMargvPointer, true));

                Assert.Equal(inputComponents[i], currentArg);
                Assert.Equal(inputComponents[i].Length, currentMargnPointer.Offset - currentMargvPointer.Offset);
            }
        }
    }
}
