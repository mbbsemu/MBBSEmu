﻿using MBBSEmu.Memory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class rstrin_Tests : ExportedModuleTestBase
    {
        private const ushort RSTRIN_ORDINAL = 511;

        [Theory]
        [InlineData("TEST1\0TEST2\0TEST3\0")]
        [InlineData("\0")]
        [InlineData("A\0B\0")]
        [InlineData("A\0TEST\0B\0")]
        [InlineData("A\0\0\0TEST\0\0\0B\0")]
        public void rstrin_Test(string inputCommand)
        {
            //Reset State
            Reset();

            //Set Input Values
            mbbsEmuMemoryCore.SetArray("INPUT", Encoding.ASCII.GetBytes(inputCommand));
            mbbsEmuMemoryCore.SetWord("INPLEN", (ushort) (inputCommand.Length - 1));

            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, RSTRIN_ORDINAL, new List<FarPtr>());

            //Verify Results
            //Simulate rstrin by replacing nulls with spaces, ensuring last character is null
            var expectedRstrin = inputCommand.Replace('\0', ' ').TrimEnd() + '\0';
            Assert.True(Encoding.ASCII.GetBytes(expectedRstrin)
                .SequenceEqual(mbbsEmuMemoryCore.GetArray("INPUT", (ushort)inputCommand.Length).ToArray())); //Verify spaces replaced with nulls
        }
    }
}
