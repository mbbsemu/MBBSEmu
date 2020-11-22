using System.Collections.Generic;
using System.Text;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class sortstgs_Tests : ExportedModuleTestBase
    {
        private const int SORTSTGS_ORDINAL = 558;

        [Theory]
        [InlineData(new[] {"This", "is", "an", "array"}, 4, new[] {"an", "array", "is", "This"})]
        public void sortstgs_Test(string[] inputArray, ushort numElements, string[] expectedArray)
        {
            //Reset State
            Reset();

            var stringPointer = mbbsEmuMemoryCore.AllocateVariable("INPUT_STRING", (ushort) (inputArray.Length + 1));

            foreach (var t in inputArray)
                mbbsEmuMemoryCore.SetArray("INPUT_STRING", Encoding.ASCII.GetBytes(t));

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, SORTSTGS_ORDINAL,
                new List<ushort>
                {
                    stringPointer.Offset,
                    stringPointer.Segment,
                    numElements
                });


            var dstArray = mbbsEmuMemoryCore.GetArray("INPUT_STRING", numElements);
            
            //Verify Results
            //Assert.Equal(expectedArray, dstArray.ToArray());
            //Assert.Equal(0, dstArray.ToArray());
        }
    }
}
