using MBBSEmu.HostProcess.Structs;
using MBBSEmu.Memory;
using System.Collections.Generic;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class curusr_Tests : ExportedModuleTestBase
    {
        private const int CURUSR_ORDINAL = 151;

        [Fact]
        public void CURUSR_Test()
        {
            //Reset State
            Reset();

            Assert.Equal(0, mbbsEmuMemoryCore.GetWord("USRNUM"));

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, CURUSR_ORDINAL, new List<ushort> { 1 });

            Assert.Equal(1, mbbsEmuMemoryCore.GetWord("USRNUM"));
        }

        [Fact]
        public void CURUSR_invalid_Test()
        {
            //Reset State
            Reset();

            Assert.Equal(0, mbbsEmuMemoryCore.GetWord("USRNUM"));

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, CURUSR_ORDINAL, new List<ushort> { 1000 });

            Assert.Equal(0, mbbsEmuMemoryCore.GetWord("USRNUM"));
        }

        [Fact]
        public void CURUSR_crossover_Test()
        {
            //Reset State
            Reset();
            majorbbs.SetState(0);

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, CURUSR_ORDINAL, new List<ushort> { 1 });
            SetSubstt(0, 7);
            SetSubstt(1, 9);
            majorbbs.UpdateSession(0);

            //Verify Results
            Assert.Equal(7, testSessions[0].UsrPtr.Substt);
            Assert.Equal(9, testSessions[1].UsrPtr.Substt);
        }

        [Fact]
        public void CURUSR_inactive_Test()
        {
            //Reset State
            Reset();

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, CURUSR_ORDINAL, new List<ushort> { 3 });

            //Verify Results
            var userPointer = new FarPtr(mbbsEmuMemoryCore.GetVariablePointer("USER").Data);
            userPointer.Offset += (ushort)(User.Size * 3);

            Assert.Equal(3, mbbsEmuMemoryCore.GetWord("USRNUM"));
            Assert.Equal(userPointer, mbbsEmuMemoryCore.GetPointer("*USRPTR"));
            Assert.Equal(0, mbbsEmuMemoryCore.GetWord(userPointer.Segment, (ushort)(userPointer.Offset + 6)));
        }

        private void SetSubstt(ushort channel, ushort value)
        {
            var userPointer = new FarPtr(mbbsEmuMemoryCore.GetVariablePointer("USER").Data);
            userPointer.Offset += (ushort)(User.Size * channel + 8);
            mbbsEmuMemoryCore.SetWord(userPointer, value);
        }
    }
}
