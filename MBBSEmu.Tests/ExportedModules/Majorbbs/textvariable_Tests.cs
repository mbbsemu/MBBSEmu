using MBBSEmu.Memory;
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
        public void FINDTVAR_Test(string inputString, ushort expectedOrdinal)
        {
            //Reset State
            Reset();

            var stringPointer = mbbsEmuMemoryCore.AllocateVariable("INPUT_STRING", (ushort) (inputString.Length + 1));
            mbbsEmuMemoryCore.SetArray("INPUT_STRING", Encoding.ASCII.GetBytes(inputString));

            //Register a Unit Test Variable
            REGISTER_TEXTVAR("UNIT_TEST", 0xFF, 0x00);

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, FINDTVAR_ORDINAL,
                new List<FarPtr> {stringPointer});

            Assert.Equal(expectedOrdinal, mbbsEmuCpuRegisters.AX);
            Assert.Equal(2, mbbsEmuMemoryCore.GetWord("NTVARS"));
        }

        private void REGISTER_TEXTVAR(string name, ushort segment, ushort offset)
        {

            var stringPointer = mbbsEmuMemoryCore.AllocateVariable("REGISTER_TEXTVAR_INPUT", (ushort)(name.Length + 1));
            mbbsEmuMemoryCore.SetArray("REGISTER_TEXTVAR_INPUT", Encoding.ASCII.GetBytes(name));

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, REGISTER_TEXTVAR_ORDINAL,
                new List<FarPtr> { stringPointer, new(segment, offset) });
        }
    }
}
