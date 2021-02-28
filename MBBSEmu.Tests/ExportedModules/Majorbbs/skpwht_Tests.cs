using MBBSEmu.Memory;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public partial class Majorbbs_Tests
    {
        private const int SKPWHT_ORDINAL = 553;

        [Theory]
        [InlineData("  testing 123 hello", "testing 123 hello\0")]
        [InlineData("          Hi", "Hi\0")]
        [InlineData("   #%#@#$%#$!&&^^ Hi", "#%#@#$%#$!&&^^ Hi\0")]
        [InlineData(" test", "test\0")]
        [InlineData("\0test", "\0")]
        [InlineData("NoWhiteSpace", "NoWhiteSpace\0")]
        public void SKPWHT_Test(string inputString, string expectedString)
        {
            //Reset State
            Reset();

            //Set Argument Values to be Passed In
            var stringPointer = mbbsEmuMemoryCore.AllocateVariable("INPUT_STRING", (ushort)(inputString.Length + 1));
            mbbsEmuMemoryCore.SetArray("INPUT_STRING", Encoding.ASCII.GetBytes(inputString));

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, SKPWHT_ORDINAL, new List<FarPtr> { stringPointer });

            //Verify Results
            Assert.Equal(expectedString,
                Encoding.ASCII.GetString(
                    mbbsEmuMemoryCore.GetString(mbbsEmuCpuRegisters.GetPointer())));
        }
    }
}
