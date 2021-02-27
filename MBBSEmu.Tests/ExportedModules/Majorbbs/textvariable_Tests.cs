using MBBSEmu.Memory;
using MBBSEmu.TextVariables;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class textvariable_Tests : ExportedModuleTestBase
    {
        private const int FINDTVAR_ORDINAL = 215;
        private const int REGISTER_TEXTVAR_ORDINAL = 494;

        [Theory]
        [InlineData("SYSTEM", 0)]
        [InlineData("UNIT_TEST", 1)]
        [InlineData("XXX", 65535)]
        [InlineData("SYSTEM_NAME", 0)]
        [InlineData("", 2)]
        public void FINDTVAR_Test(string inputString, ushort expectedOrdinal)
        {
            //Reset State
            Reset();
            var textVariableService = _serviceResolver.GetService<ITextVariableService>();
            textVariableService.SetVariable("SYSTEM_NAME", () => "MBBSEmu");

            var stringPointer = mbbsEmuMemoryCore.AllocateVariable("INPUT_STRING", (ushort)(inputString.Length + 1));
            mbbsEmuMemoryCore.SetArray("INPUT_STRING", Encoding.ASCII.GetBytes(inputString));

            //Register a Unit Test Variable
            REGISTER_TEXTVAR("UNIT_TEST", 0xFF, 0x00);

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, FINDTVAR_ORDINAL,
                new List<FarPtr> { stringPointer });

            Assert.Equal(expectedOrdinal, mbbsEmuCpuRegisters.AX);
            Assert.Equal(2, mbbsEmuMemoryCore.GetWord("NTVARS"));
        }

        private void REGISTER_TEXTVAR(string name, ushort segment, ushort offset)
        {

            var stringPointer = mbbsEmuMemoryCore.AllocateVariable("REGISTER_TEXTVAR_INPUT", (ushort)(name.Length + 1));
            mbbsEmuMemoryCore.SetArray("REGISTER_TEXTVAR_INPUT", Encoding.ASCII.GetBytes(name + '\0'));

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, REGISTER_TEXTVAR_ORDINAL,
                new List<FarPtr> { stringPointer, new(segment, offset) });
        }

        [Theory]
        [InlineData(1)]
        [InlineData(20)]
        public void NTVARS_Test(ushort numberToCreate)
        {
            for (ushort i = 0; i < numberToCreate; i++)
            {
                REGISTER_TEXTVAR($"NTVARS_TEST_{i}", 0xFF, i);
            }

            //+1 for the SYSTEM variable pointer that's always present at index 0
            Assert.Equal(numberToCreate + 1, mbbsEmuMemoryCore.GetWord("NTVARS"));
        }

        private ushort CalculateTxtvarOffset(ushort index)
        {
            return (mbbsEmuMemoryCore.GetVariablePointer("TXTVARS") +
                    (index * MBBSEmu.HostProcess.Structs.TextvarStruct.Size)).Offset;
        }

        [Theory]
        [InlineData(10, "Simple Test")]
        public void TXTVARS_Tests(ushort numberToCreate, string testName)
        {
            for (ushort i = 0; i < numberToCreate; i++)
            {
                REGISTER_TEXTVAR($"NTVARS_TEST_{i}", 0xFF, i);
            }

            REGISTER_TEXTVAR(testName, 0xFF, 0xFE);

            //+1 for SYSTEM
            var variableString = mbbsEmuMemoryCore.GetString(
                mbbsEmuMemoryCore.GetVariablePointer("TXTVARS").Segment, CalculateTxtvarOffset((ushort)(1 + numberToCreate)), true);

            Assert.Equal(testName, Encoding.ASCII.GetString(variableString));
        }
    }
}
