using System.Collections.Generic;
using System.Text;
using MBBSEmu.Memory;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class strlwr_Tests : MajorbbsTestBase
    {
        private const int STRLWR_ORDINAL = 579;

        [Theory]
        [InlineData("TEST", "test")]
        [InlineData("TEST1", "test1")]
        [InlineData("TEST test TEST", "test test test")]
        [InlineData("TeSt", "test")]
        public void STRLWE_Test(string inputString, string expectedString)
        {
            //Reset State
            Reset();

            //Set Argument Values to be Passed In
            var string1Pointer = mbbsEmuMemoryCore.AllocateVariable("INPUT_STRING", (ushort)(inputString.Length + 1));
            mbbsEmuMemoryCore.SetArray("INPUT_STRING", Encoding.ASCII.GetBytes(inputString));

            //Execute Test
            ExecuteApiTest(STRLWR_ORDINAL, new List<IntPtr16> { string1Pointer });

            //Verify Results
            Assert.Equal(expectedString, Encoding.ASCII.GetString(mbbsEmuMemoryCore.GetString("STRLWR", true)));
            Assert.Equal(expectedString,
                Encoding.ASCII.GetString(
                    mbbsEmuMemoryCore.GetString(new IntPtr16(mbbsEmuCpuRegisters.DX, mbbsEmuCpuRegisters.AX), true)));
        }

        [Fact]
        public void STRLWE_Test_1K()
        {
            var inputString = new string('A', 1024);
            var expectedString = new string('a', 1023);

            //Reset State
            Reset();

            //Set Argument Values to be Passed In
            var string1Pointer = mbbsEmuMemoryCore.AllocateVariable("INPUT_STRING", (ushort)(inputString.Length + 1));
            mbbsEmuMemoryCore.SetArray("INPUT_STRING", Encoding.ASCII.GetBytes(inputString));

            //Execute Test
            ExecuteApiTest(STRLWR_ORDINAL, new List<IntPtr16> { string1Pointer });

            //Verify Results
            Assert.Equal(expectedString, Encoding.ASCII.GetString(mbbsEmuMemoryCore.GetString("STRLWR", true)));
            Assert.Equal(expectedString,
                Encoding.ASCII.GetString(
                    mbbsEmuMemoryCore.GetString(new IntPtr16(mbbsEmuCpuRegisters.DX, mbbsEmuCpuRegisters.AX), true)));
        }
    }
}
