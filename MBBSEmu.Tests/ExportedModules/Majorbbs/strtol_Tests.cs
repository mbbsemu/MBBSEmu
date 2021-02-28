using System.Collections.Generic;
using System.Text;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public partial class Majorbbs_Tests
    {
        private const int STRTOL_ORDINAL = 586;

        [Theory]
        [InlineData("123456 Hello some text", 7, 10, 123456)]
        [InlineData("2345678 YESSS", 8, 10, 2345678)]
        [InlineData("+2345678 YESSS", 9, 10, 2345678)]
        [InlineData("   2345678 YESSS", 11, 10, 2345678)]
        [InlineData("78 A", 3, 10, 78)]
        [InlineData("0x23CACE A very big number", 9, 16, 2345678)]
        [InlineData("-2345678 A very big number", 9, 10, -2345678)]
        [InlineData("FFFFF is my favorite number", 6, 16, 1048575)]
        [InlineData("0x7FFFFFFF is pretty big", 11, 16, int.MaxValue)]
        [InlineData("", 0, 10, 0)]
        [InlineData("ThereIsNoNumberHere", 0, 10, 0)]
        public void strtol_Test(string srcString, ushort ushortPtr, ushort numBase, int expectedValue)
        {
            //Reset State
            Reset();

            //Allocate Variable to be Passed In
            var sourceStringPointer = mbbsEmuMemoryCore.AllocateVariable("SRC", (ushort)(srcString.Length));
            mbbsEmuMemoryCore.SetArray("SRC", Encoding.ASCII.GetBytes(srcString));

            //Get initial suffixPointer
            var sourceStringSuffixPointer = mbbsEmuMemoryCore.GetVariablePointer("SRC");

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, STRTOL_ORDINAL,
                new List<ushort>
                {
                    sourceStringPointer.Offset,
                    sourceStringPointer.Segment,
                    sourceStringSuffixPointer.Offset,
                    sourceStringSuffixPointer.Segment,
                    numBase
                });

            //Get returned suffixPointer
            var returnedSuffixPointer = mbbsEmuMemoryCore.GetPointer(sourceStringSuffixPointer);

            //Verify Results
            Assert.Equal((ushort)(expectedValue & 0xFFFF), mbbsEmuCpuRegisters.AX);
            Assert.Equal((ushort)(expectedValue >> 16), mbbsEmuCpuRegisters.DX);
            Assert.Equal(sourceStringPointer.Segment, returnedSuffixPointer.Segment);
            Assert.Equal(sourceStringPointer.Offset + ushortPtr, returnedSuffixPointer.Offset);
        }

        [Fact]
        public void strtol_nullsuffix_Test()
        {
            Reset();
            
            const string srcString = "123456 Hello some text";
            const ushort numBase = 10;
            const int expectedValue = 123456;

            //Allocate Variable to be Passed In
            var sourceStringPointer = mbbsEmuMemoryCore.AllocateVariable("SRC", (ushort)(srcString.Length));
            mbbsEmuMemoryCore.SetArray("SRC", Encoding.ASCII.GetBytes(srcString));

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, STRTOL_ORDINAL,
                new List<ushort>
                {
                    sourceStringPointer.Offset,
                    sourceStringPointer.Segment,
                    0,
                    0,
                    numBase
                });

            //Verify Results
            Assert.Equal((ushort)(expectedValue & 0xFFFF), mbbsEmuCpuRegisters.AX);
            Assert.Equal((ushort)(expectedValue >> 16), mbbsEmuCpuRegisters.DX);
        }
    }
}
