using FluentAssertions;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class sprintf_Tests : ExportedModuleTestBase
    {
        private const int SPRINTF_ORDINAL = 560;

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
        [InlineData("99% of the time, this will print %s", "99% of the time, this will print TEST", "TEST")] //Unescaped %
        [InlineData("Mid 50% Test", "Mid 50% Test", null)] //Unescaped %
        [InlineData("End 50% ", "End 50% ", null)] //Unescaped %
        [InlineData("End 50%", "End 50%", null)] //Unescaped %
        [InlineData("This is 100%% accurate", "This is 100% accurate", null)] //Escaped %
        [InlineData("%%%%", "%%", null)] //Escaped %
        [InlineData("%%%%%", "%%%", null)] //Escaped & Unescaped %
        [InlineData("%%%%% ", "%%% ", null)] //Escaped & Unescaped %
        public void sprintf_Test(string formatString, string expectedString, params object[] values)
        {
            Reset();

            var destBuffer = mbbsEmuMemoryCore.Malloc((ushort)(expectedString.Length * 2));
            var formatStringParameterPointer = mbbsEmuMemoryCore.Malloc((ushort)(formatString.Length + 1));
            mbbsEmuMemoryCore.SetArray(formatStringParameterPointer, Encoding.ASCII.GetBytes(formatString));

            parameters.Add(destBuffer.Offset);
            parameters.Add(destBuffer.Segment);

            parameters.Add(formatStringParameterPointer.Offset);
            parameters.Add(formatStringParameterPointer.Segment);

            if (values != null)
            {
                var parameterList = GenerateParameters(values);
                foreach (var p in parameterList)
                    parameters.Add(p);
            }

            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, SPRINTF_ORDINAL, parameters);

            Encoding.ASCII.GetString(mbbsEmuMemoryCore.GetString(destBuffer, true)).Should().Be(expectedString);
        }
    }
}
