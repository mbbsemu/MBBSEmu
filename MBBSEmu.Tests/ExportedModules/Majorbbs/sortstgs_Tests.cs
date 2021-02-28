using MBBSEmu.Memory;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public partial class Majorbbs_Tests
    {
        private const int SORTSTGS_ORDINAL = 558;

        [Theory]
        [InlineData(new[] { "This", "is", "an", "array" }, new[] { "an", "array", "is", "This" })]
        [InlineData(new[] { "Hello", "my", "name", "is" }, new[] { "Hello", "is", "my", "name" })]
        [InlineData(new[] { "Hello", "Anger" }, new[] { "Anger", "Hello" })]
        [InlineData(new[] { "zzzz", "BBBB", "CCCC", "aaaa", "000AAA" }, new[] { "000AAA", "aaaa", "BBBB", "CCCC", "zzzz" })]
        [InlineData(new[] { "zzzz", "BBBB", "CCCC", "aaaa", "000AAA", "111ZZZ", "!!!XXX" }, new[] { "!!!XXX", "000AAA", "111ZZZ", "aaaa", "BBBB", "CCCC", "zzzz" })]
        [InlineData(new string[] { }, new string[] { })]
        public void sortstgs_Test(string[] inputArray, string[] expectedArray)
        {
            //Reset State
            Reset();

            //Set Argument Values to be Passed In
            var stringPointer = new FarPtr[inputArray.Length];

            for (var i = 0; i < inputArray.Length; i++)
            {
                stringPointer[i] = mbbsEmuMemoryCore.AllocateVariable("INPUT_STRING" + i, (ushort) (inputArray[i].Length + 1));
                mbbsEmuMemoryCore.SetArray("INPUT_STRING" + i, Encoding.ASCII.GetBytes(inputArray[i]));
            }

            var stringPointerArray = stringPointer.ToArray();
            var arrayPointer = mbbsEmuMemoryCore.AllocateVariable("INPUT_ARRAY", (ushort)(FarPtr.Size * inputArray.Length), true);

            for (var i = 0; i < inputArray.Length; i++)
                mbbsEmuMemoryCore.SetPointer(arrayPointer + (i * FarPtr.Size), stringPointerArray[i]);

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, SORTSTGS_ORDINAL,
                new List<ushort>
                {
                    arrayPointer.Offset,
                    arrayPointer.Segment,
                    (ushort) inputArray.Length
                });

            //Return Values
            var resultPointer = new FarPtr[inputArray.Length];
            var resultString = new string[inputArray.Length];

            for (var i = 0; i < inputArray.Length; i++)
            {
                resultPointer[i] = mbbsEmuMemoryCore.GetPointer(arrayPointer + (i * FarPtr.Size));
                resultString[i] = Encoding.ASCII.GetString(mbbsEmuMemoryCore.GetString(resultPointer[i]));
            }

            var resultStringArray = resultString.ToArray();

            //Verify Results
            Assert.Equal(expectedArray, resultStringArray);
        }
    }
}
