using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class Majorbbs_prf_Tests : ExportedModuleTestBase
    {
        private const int PRF_ORDINAL = 474;

        private List<ushort> parameters = new List<ushort>();

        [Theory]
        [InlineData("%d", "1", (ushort)1)]
        [InlineData("%d", "0", (ushort)0)]
        [InlineData("%d", "-1", (ushort)0xFFFF)]
        [InlineData("%u", "1", (ushort)1)]
        [InlineData("%u", "0", (ushort)0)]
        [InlineData("%u", "65535", (ushort)0xFFFF)]
        [InlineData("ITEM%3.3d", "ITEM010", (ushort)10)]
        [InlineData("ITEM%3d", "ITEM 10", (ushort)10)]
        [InlineData("ITEM%3.3d", "ITEM100", (ushort)100)]
        [InlineData("ITEM%3d", "ITEM100", (ushort)100)]
        [InlineData("Level: %5d", "Level:     3", (ushort)3)]
        [InlineData("Level: %-5d", "Level: 3    ", (ushort)3)]
        [InlineData("Level: %5.5d", "Level: 00003", (ushort)3)]
        [InlineData("Level: %-5.5d", "Level: 00003", (ushort)3)]
        [InlineData("%s-%d", "TEST-1", "TEST", (ushort)1)]
        [InlineData("%s-%ld", "TEST-2147483647", "TEST", 2147483647)]
        [InlineData("%s-%ld-%d-%s", "TEST-2147483647-1-FOO", "TEST", 2147483647, (ushort)1, "FOO")]
        [InlineData("%s-%ld-%d-%s", "TEST--1-1-FOO", "TEST", (uint)0xFFFFFFFF, (ushort)1, "FOO")]
        [InlineData("%s-%lu-%d-%s", "TEST-2147483647-1-FOO", "TEST", 2147483647u, (ushort)1, "FOO")]
        [InlineData("%s-%lu-%d-%s", "TEST-3147483647-1-FOO", "TEST", 3147483647u, (ushort)1, "FOO")]
        public void prf_Test(string inputString, string expectedString, params object[] values)
        {
            Reset();

            var inputStingParameterPointer = mbbsEmuMemoryCore.AllocateVariable(Guid.NewGuid().ToString(), (ushort)(inputString.Length + 1));
            mbbsEmuMemoryCore.SetArray(inputStingParameterPointer, Encoding.ASCII.GetBytes(inputString));
            parameters.Add(inputStingParameterPointer.Offset);
            parameters.Add(inputStingParameterPointer.Segment);

            var parameterList = GenerateParameters(values);
            foreach (var p in parameterList)
                parameters.Add(p);

            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, PRF_ORDINAL, parameters);

            Assert.Equal(expectedString, Encoding.ASCII.GetString(mbbsEmuMemoryCore.GetString("PRFBUF", true)));
        }

        protected override void Reset()
        {
            parameters = new List<ushort>();
            base.Reset();

            //Reset PRFPTR
            mbbsEmuMemoryCore.SetPointer("PRFPTR", mbbsEmuMemoryCore.GetVariablePointer("PRFBUF"));
            mbbsEmuMemoryCore.SetZero(mbbsEmuMemoryCore.GetVariablePointer("PRFBUF"), 0x4000);
        }

    }
}
