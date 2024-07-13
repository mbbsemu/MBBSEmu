using MBBSEmu.Memory;
using MBBSEmu.Module;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class tokopt_Tests : ExportedModuleTestBase
    {
        private const int TOKPT_ORDINAL = 602;

        [Theory]
        [InlineData(new[] { "TEST1", "TEST2", "TEST3" }, "TEST", 0)]
        [InlineData(new[] { "TEST1", "TEST2", "TEST3" }, "TEST2", 2)]
        [InlineData(new[] { "TEST1", "TEST2", "TEST3" }, "Testing Multiple Words: TEST", 0)]
        [InlineData(new[] { "TEST1", "TEST2", "TEST3" }, "Testing Multiple Words: TEST3", 3)]
        public void stgopt_Test(string[] tokens, string valueToTest, ushort expectedResult)
        {
            //Reset State
            Reset();

            //Allocate Memory for each Token which will be passed in as params to the function
            var tokenPointers = new List<FarPtr>();
            foreach(var token in tokens)
            {
                var tokenPointer =
                    mbbsEmuMemoryCore.AllocateVariable($"TOKEN{tokenPointers.Count + 1}", (ushort)(token.Length + 1));
                mbbsEmuMemoryCore.SetArray(tokenPointer, Encoding.ASCII.GetBytes(token));

                tokenPointers.Add(tokenPointer);
            }


            //Set Argument Values to be Passed In
            var mcvPointer = (ushort)majorbbs.McvPointerDictionary.Allocate(new McvFile("TEST.MCV",
                new Dictionary<int, byte[]> { { 0, Encoding.ASCII.GetBytes(valueToTest) } }));

            mbbsEmuMemoryCore.SetPointer("CURRENT-MCV", new FarPtr(0xFFFF, mcvPointer));

            //Build Parameters List
            var parameters = new List<ushort>();
            parameters.Add(0); //Ordinal of MCV Value
            //Add Token Pointers
            foreach (var tokenPointer in tokenPointers)
            {
                parameters.Add(tokenPointer.Offset);
                parameters.Add(tokenPointer.Segment);
            }
            //Terminate with Null Pointer
            parameters.Add(0);
            parameters.Add(0);

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, TOKPT_ORDINAL, parameters);

            //Verify Results
            Assert.Equal(expectedResult, mbbsEmuCpuRegisters.AX);
        }
    }
}
