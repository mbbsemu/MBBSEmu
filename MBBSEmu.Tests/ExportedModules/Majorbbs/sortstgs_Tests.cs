using MBBSEmu.Memory;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    ///     Signature: void sortstgs(char *stgs[],int num) 558;
    public class sortstgs_Tests : ExportedModuleTestBase
    {
        private const int SORTSTGS_ORDINAL = 558;

        [Theory]
        [InlineData(new [] { "This", "Is", "an", "array" }, 4, new [] { "This", "Is", "an", "array" })]
        public void sortstgs_Test(string[] inputArray, ushort numElements, string[] expectedArray)
        {
            //Reset State
            Reset();

            var stringPointer = mbbsEmuMemoryCore.AllocateVariable("INPUT_STRING", (ushort)(inputArray.Length + 1));
            mbbsEmuMemoryCore.SetArray("INPUT_STRING", Encoding.ASCII.GetBytes(inputArray));

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, SORTSTGS_ORDINAL, new List<IntPtr16> { stringPointer });

            //Verify Results
            Assert.Equal(expectedArray, Encoding.ASCII.GetString(mbbsEmuMemoryCore.GetString("INPUT_STRING")));
        }
    }
