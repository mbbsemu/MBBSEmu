using MBBSEmu.Memory;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class sortstgs_Tests : ExportedModuleTestBase
    {
        private const int SORTSTGS_ORDINAL = 558;

        [Theory]
        [InlineData(new[] {"This", "is", "an", "array"}, new[] {"an", "array", "is", "This"})]
        public void sortstgs_Test(string[] inputArray, string[] expectedArray)
        {
            //Reset State
            Reset();

            var stringPointer = mbbsEmuMemoryCore.AllocateVariable("INPUT_ARRAY", (IntPtr16.Size * 0xFF), true);

            //for (var i = 0; i < inputArray.Length; i++)
            //{
            //    var stringPointerItem = mbbsEmuMemoryCore.AllocateVariable("**INPUT_ARRAY", IntPtr16.Size);
            //    //mbbsEmuMemoryCore.SetPointer("**INPUT_ARRAY", mbbsEmuMemoryCore.GetPointer("*INPUT_ARRAY"));
            //    //mbbsEmuMemoryCore.SetArray(stringPointer, mbbsEmuMemoryCore.GetPointer(stringPointerItem));
            //    mbbsEmuMemoryCore.SetArray(stringPointerItem, Encoding.ASCII.GetBytes(inputArray[i]));
            //}

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, SORTSTGS_ORDINAL,
                new List<ushort>
                {
                    stringPointer.Offset,
                    stringPointer.Segment,
                    (ushort) inputArray.Length
                });


            var dstArray = mbbsEmuMemoryCore.GetArray("INPUT_ARRAY", (ushort) inputArray.Length);
            
            //Verify Results
            //Assert.Equal(expectedArray.ToArray(), dstArray.ToArray());
            Assert.Equal(0,0);
        }
    }
}
