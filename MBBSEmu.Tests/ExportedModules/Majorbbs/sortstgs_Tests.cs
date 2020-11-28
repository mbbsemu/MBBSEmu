using MBBSEmu.Memory;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class sortstgs_Tests : ExportedModuleTestBase
    {
        private const int SORTSTGS_ORDINAL = 558;
        
        [Theory]
        [InlineData(new[] { "This", "is", "an", "array" }, new[] { "an", "array", "is", "This" })]
        [InlineData(new[] { "Hello", "my", "name", "is" }, new[] { "Hello", "is", "my", "name" })]
        [InlineData(new[] { "Hello", "Anger" }, new[] { "Anger", "Hello" })]
        [InlineData(new[] { "zzzz", "BBBB", "CCCC", "aaaa", "000AAA" }, new[] { "000AAA", "aaaa", "BBBB", "CCCC", "zzzz" })]
        public void sortstgs_Test(string[] inputArray, string[] expectedArray)
        {
            //Reset State
            Reset();

            //Set Argument Values to be Passed In
            var stringPointer = new IntPtr16[inputArray.Length];

            for (var i = 0; i < inputArray.Length; i++)
                stringPointer[i] = mbbsEmuMemoryCore.AllocateVariable("INPUT_STRING"+ i, (ushort)(inputArray[i].Length + 1));

            for (var i = 0; i < inputArray.Length; i++)
                mbbsEmuMemoryCore.SetArray("INPUT_STRING"+ i, Encoding.ASCII.GetBytes(inputArray[i]));

            var stringPointerArray = stringPointer.ToArray();

            var arrayPointer = mbbsEmuMemoryCore.AllocateVariable("INPUT_ARRAY", (IntPtr16.Size * 4), true);
            
            for (var i = 0; i < inputArray.Length; i++)
                mbbsEmuMemoryCore.SetPointer(arrayPointer + (i * IntPtr16.Size), stringPointerArray[i]);

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, SORTSTGS_ORDINAL,
                new List<ushort>
                {
                    arrayPointer.Offset,
                    arrayPointer.Segment,
                    (ushort) inputArray.Length
                });

            var resultPointer = new IntPtr16[inputArray.Length];
            var resultString = new string[inputArray.Length];

            for (var i = 0; i < inputArray.Length; i++)
                resultPointer[i] = mbbsEmuMemoryCore.GetPointer(arrayPointer + (i * IntPtr16.Size));


            // var resultPointer1 = mbbsEmuMemoryCore.GetPointer(arrayPointer + (0 * IntPtr16.Size));
            // var resultPointer2 = mbbsEmuMemoryCore.GetPointer(arrayPointer + (1 * IntPtr16.Size));
            // var resultPointer3 = mbbsEmuMemoryCore.GetPointer(arrayPointer + (2 * IntPtr16.Size));
            // var resultPointer4 = mbbsEmuMemoryCore.GetPointer(arrayPointer + (3 * IntPtr16.Size));

            for (var i = 0; i < inputArray.Length; i++)
                resultString[i] = Encoding.ASCII.GetString(mbbsEmuMemoryCore.GetString(resultPointer[i]));

            // var resultString1 = Encoding.ASCII.GetString(mbbsEmuMemoryCore.GetString(resultPointer1));
            // var resultString2 = Encoding.ASCII.GetString(mbbsEmuMemoryCore.GetString(resultPointer2));
            // var resultString3 = Encoding.ASCII.GetString(mbbsEmuMemoryCore.GetString(resultPointer3));
            // var resultString4 = Encoding.ASCII.GetString(mbbsEmuMemoryCore.GetString(resultPointer4));

            //var resultStringArray = new [] { resultString1, resultString2, resultString3, resultString4 };
            var resultStringArray = resultString.ToArray();

            //Verify Results
            Assert.Equal(expectedArray, resultStringArray);
        }
    }
}
