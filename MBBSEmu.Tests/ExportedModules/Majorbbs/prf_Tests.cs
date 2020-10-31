using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MBBSEmu.Memory;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class prf_Tests : ExportedModuleTestBase
    {
        private const int PRF_ORDINAL = 474;

        private List<ushort> parameters = new List<ushort>();

        [Theory]
        [InlineData("%d", "1", (ushort)1)]
        [InlineData("%s-%d", "TEST-1", "TEST", (ushort)1)]
        [InlineData("%s-%ld", "TEST-2147483647", "TEST", 2147483647)]
        [InlineData("%s-%ld-%d-%s", "TEST-2147483647-1-FOO", "TEST", 2147483647, (ushort)1, "FOO")]
        public void prf_Test(string inputString, string expectedString, params object[] values)
        {
            Reset();

            var inputStingParameterPointer = mbbsEmuMemoryCore.AllocateVariable(Guid.NewGuid().ToString(), (ushort)(inputString.Length + 1));
            mbbsEmuMemoryCore.SetArray(inputStingParameterPointer, Encoding.ASCII.GetBytes(inputString));
            parameters.Add(inputStingParameterPointer.Offset);
            parameters.Add(inputStingParameterPointer.Segment);

            foreach (var v in values)
            {
                switch (v)
                {
                    case string @parameterString:
                    {
                        var stringParameterPointer = mbbsEmuMemoryCore.AllocateVariable(Guid.NewGuid().ToString(), (ushort)(@parameterString.Length + 1));
                        mbbsEmuMemoryCore.SetArray(stringParameterPointer, Encoding.ASCII.GetBytes(@parameterString));
                        parameters.Add(stringParameterPointer.Offset);
                        parameters.Add(stringParameterPointer.Segment);
                        break;
                    }
                    case int @parameterLong:
                    {
                        var longBytes = BitConverter.GetBytes(@parameterLong);
                        parameters.Add(BitConverter.ToUInt16(longBytes, 0));
                        parameters.Add(BitConverter.ToUInt16(longBytes, 2));
                        break;
                    }
                    case ushort @parameterInt:
                        parameters.Add(@parameterInt);
                        break;
                }
            }

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
