using System.Collections.Generic;
using System.Text;
using MBBSEmu.Memory;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class strchr_Tests : ExportedModuleTestBase
    {
        private const int STRCHR_ORDINAL = 572;

        [Theory]
        [InlineData("", 0, null)]
        [InlineData("", 'a', null)]
        [InlineData("a", 'b', null)]
        [InlineData("abc", 'c', "c")]
        [InlineData("abc", 'b', "bc")]
        [InlineData("abc", 'a', "abc")]
        [InlineData("abc", 'z', null)]
        public void strchrTest(string a, char toFind, string expected)
        {
            //Reset State
            Reset();

            //Set Argument Values to be Passed In
            var strPointer = mbbsEmuMemoryCore.AllocateVariable("STR", (ushort)(a.Length + 1));
            mbbsEmuMemoryCore.SetArray(strPointer, Encoding.ASCII.GetBytes(a));

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, STRCHR_ORDINAL, new List<ushort> {strPointer.Offset, strPointer.Segment, toFind});

            if (expected == null)
            {
              Assert.Equal(0, mbbsEmuCpuRegisters.AX);
              Assert.Equal(0, mbbsEmuCpuRegisters.DX);
            }
            else
            {
              Assert.Equal(strPointer.Segment, mbbsEmuCpuRegisters.DX);
              Assert.Equal(strPointer.Offset + a.IndexOf(toFind), mbbsEmuCpuRegisters.AX);

              var returnString = mbbsEmuMemoryCore.GetString(mbbsEmuCpuRegisters.GetPointer(), /* stripNull= */ true);
              Assert.Equal(expected, Encoding.ASCII.GetString(returnString));
            }
        }
    }
}
