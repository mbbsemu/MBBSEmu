﻿using MBBSEmu.Memory;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class endcnc_Tests : ExportedModuleTestBase
    {
        private const int ENDCNC_ORDINAL = 191;

        //Simple commands with nxtcmd == input
        [Theory]
        [InlineData("123 ABC\0", 1, 0, "23\0ABC\0", 2)]
        [InlineData("123 ABC\0", 3, 0, "ABC\0", 1)]
        [InlineData("123 ABC\0", 4, 0, "ABC\0", 1)]
        [InlineData("123 ABC\0", 5, 0, "BC\0", 1)]
        [InlineData("\0", 0, 1, "\0", 0)]
        public void endcnc_Test(string inputString, ushort nxtcmdStartingOffset, char expectedResult, string expectedInputString, ushort expectedMargc)
        {
            //Reset State
            Reset();

            //Set Input Values
            var inputLength = (ushort)inputString.Length;

            mbbsModule.Memory.SetArray("INPUT", Encoding.ASCII.GetBytes(inputString));
            mbbsModule.Memory.SetWord("INPLEN", (ushort) (inputLength - 1));

            //Set nxtcmd
            var currentNxtcmd = mbbsEmuMemoryCore.GetPointer("NXTCMD");
            currentNxtcmd.Offset += nxtcmdStartingOffset;
            mbbsEmuMemoryCore.SetPointer("NXTCMD", currentNxtcmd);

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, ENDCNC_ORDINAL, new List<FarPtr>());

            //Gather Results
            var actualResult = mbbsEmuCpuCore.Registers.AX;
            var actualInputString = Encoding.ASCII.GetString(mbbsEmuMemoryCore.GetArray("INPUT", (ushort) (mbbsEmuMemoryCore.GetWord("INPLEN") + 1)));
            var actualMargc = mbbsEmuMemoryCore.GetWord("MARGC");
            //Verify Results
            Assert.Equal(expectedInputString, actualInputString);
            Assert.Equal(expectedResult, actualResult);
            Assert.Equal(expectedMargc, actualMargc);
        }
    }
}
