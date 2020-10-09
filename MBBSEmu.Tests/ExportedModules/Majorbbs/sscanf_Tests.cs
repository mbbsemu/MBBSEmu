using System.Collections.Generic;
using System.Text;
using MBBSEmu.Memory;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class sscanf_Tests : ExportedModuleTestBase
    {
        private const int SSCANF_ORDINAL = 562;

        [Theory]
        [InlineData("1203", 1203, 1)]
        [InlineData("+1203", 1203, 1)]
        [InlineData("-1203", -1203, 1)]
        [InlineData(" \t1203\t ", 1203, 1)]
        [InlineData(" \t+1203\t ", 1203, 1)]
        [InlineData(" \t-1203\t ", -1203, 1)]
        [InlineData("", 0, 0)]
        [InlineData("a1203", 0, 0)]
        public void sscanf_basicInteger_Test(string inputString, short expectedInteger, int expectedResult)
        {
            //Reset State
            Reset();

            //Set Argument Values to be Passed In
            var stringPointer = mbbsEmuMemoryCore.AllocateVariable("INPUT_STRING", (ushort)(inputString.Length + 1));
            mbbsEmuMemoryCore.SetArray("INPUT_STRING", Encoding.ASCII.GetBytes(inputString));

            var formatPointer = mbbsEmuMemoryCore.AllocateVariable("FORMAT_STRING", 16);
            mbbsEmuMemoryCore.SetArray("FORMAT_STRING", Encoding.ASCII.GetBytes(" %d "));

            var intPointer = mbbsEmuMemoryCore.AllocateVariable("RESULT", 2);
            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, SSCANF_ORDINAL, new List<IntPtr16> { stringPointer, formatPointer, intPointer });

            //Verify Results
            Assert.Equal(expectedResult, mbbsEmuCpuRegisters.AX);
            Assert.Equal((ushort) expectedInteger, mbbsEmuMemoryCore.GetWord(intPointer));
        }

        /*[Theory]
        [InlineData("%s %d %s", "test +45 another", "test", 45, "another", 3)]
        // TODO should the .2 get goobled up and ignored?
        [InlineData("%s %d %s", "yohoho -1.2 another", "yohoho", -1, ".2", 3)]
        [InlineData("%s %d%s", "yohoha +3456another", "yohoha", 3456, "another", 3)]
        public void sscanf_fancy_Test(
            string formatString,
            string inputString,
            string expectedFirstString,
            short expectedInteger,
            string expectedSecondString,
            int expectedResult)
        {
            //Reset State
            Reset();

            //Set Argument Values to be Passed In
            var stringPointer = mbbsEmuMemoryCore.AllocateVariable("INPUT_STRING", (ushort)(inputString.Length + 1));
            mbbsEmuMemoryCore.SetArray("INPUT_STRING", Encoding.ASCII.GetBytes(inputString));

            var formatPointer = mbbsEmuMemoryCore.AllocateVariable("FORMAT_STRING", (ushort)(formatString.Length + 1));
            mbbsEmuMemoryCore.SetArray("FORMAT_STRING", Encoding.ASCII.GetBytes(formatString));

            var firstStringPointer = mbbsEmuMemoryCore.AllocateVariable(null, 129);
            var secondStringPointer = mbbsEmuMemoryCore.AllocateVariable(null, 129);
            var intPointer = mbbsEmuMemoryCore.AllocateVariable("RESULT", 2);
            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, SSCANF_ORDINAL, new List<IntPtr16> { stringPointer, formatPointer, firstStringPointer, intPointer, secondStringPointer });

            //Verify Results
            Assert.Equal(expectedResult, mbbsEmuCpuRegisters.AX);
            Assert.Equal(expectedFirstString, Encoding.ASCII.GetString(mbbsEmuMemoryCore.GetString(firstStringPointer, true)));
            Assert.Equal(expectedSecondString, Encoding.ASCII.GetString(mbbsEmuMemoryCore.GetString(secondStringPointer, true)));
            Assert.Equal((ushort) expectedInteger, mbbsEmuMemoryCore.GetWord(intPointer));
        }*/

        // TODO have a test where format ends with % but no s/d/etc
        [Fact]
        public void test()
        {
            //Reset State
            Reset();

            //Set Argument Values to be Passed In
            var stringPointer = mbbsEmuMemoryCore.AllocateVariable("INPUT_STRING", 32);
            mbbsEmuMemoryCore.SetArray("INPUT_STRING", Encoding.ASCII.GetBytes("abc45"));

            var formatPointer = mbbsEmuMemoryCore.AllocateVariable("FORMAT_STRING", 16);
            mbbsEmuMemoryCore.SetArray("FORMAT_STRING", Encoding.ASCII.GetBytes("abc45%d"));

            var intPointer = mbbsEmuMemoryCore.AllocateVariable("RESULT", 2);
            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, SSCANF_ORDINAL, new List<IntPtr16> { stringPointer, formatPointer, intPointer });

            //Verify Results
            Assert.Equal(1, mbbsEmuCpuRegisters.AX);
            Assert.Equal(45, mbbsEmuMemoryCore.GetWord(intPointer));
        }
    }
}
