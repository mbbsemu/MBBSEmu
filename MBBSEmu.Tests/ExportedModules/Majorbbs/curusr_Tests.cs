using MBBSEmu.HostProcess.Enums;
using MBBSEmu.HostProcess.Structs;
using MBBSEmu.Memory;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{
    public class curusr_Tests : ExportedModuleTestBase
    {
        private const int CURUSR_ORDINAL = 151;
        private const byte SENTINEL = 0x5A;

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

        [Fact]
        public void CURUSR_keeps_input_Test()
        {
            //Reset State
            Reset();
            SetPendingInput(0, "get sword");
            SetPendingInput(1, "look");
            majorbbs.SetState(0);

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, CURUSR_ORDINAL, new List<ushort> { 1 });

            //Verify Results
            Assert.Equal(1, mbbsEmuMemoryCore.GetWord("USRNUM"));
            Assert.Equal("get", Encoding.ASCII.GetString(mbbsEmuMemoryCore.GetString("INPUT", true)));
            Assert.Equal(2, mbbsEmuMemoryCore.GetWord("MARGC"));
        }

        [Fact]
        public void CURUSR_keeps_status_Test()
        {
            //Reset State
            Reset();
            SetPendingInput(0, "look");
            testSessions[1].Status.Enqueue(EnumUserStatus.OUTPUT_BUFFER_EMPTY);
            majorbbs.SetState(0);

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, CURUSR_ORDINAL, new List<ushort> { 1 });
            Assert.Equal((ushort)EnumUserStatus.CR_TERMINATED_STRING_AVAILABLE, mbbsEmuMemoryCore.GetWord("STATUS"));
            majorbbs.UpdateSession(0);

            //Verify Results
            Assert.Equal(new[] { EnumUserStatus.CR_TERMINATED_STRING_AVAILABLE }, testSessions[0].Status.ToArray());
            Assert.Equal(new[] { EnumUserStatus.OUTPUT_BUFFER_EMPTY }, testSessions[1].Status.ToArray());
        }

        [Fact]
        public void CURUSR_keeps_prfbuf_and_nxtcmd_Test()
        {
            //Reset State
            Reset();
            majorbbs.SetState(0);

            const string message = "Alfa waves.";
            var prfPointer = mbbsEmuMemoryCore.GetPointer("PRFPTR");
            mbbsEmuMemoryCore.SetArray("PRFBUF", Encoding.ASCII.GetBytes(message));
            prfPointer.Offset += (ushort)message.Length;
            mbbsEmuMemoryCore.SetPointer("PRFPTR", prfPointer);

            var nxtcmdPointer = mbbsEmuMemoryCore.GetVariablePointer("INPUT");
            nxtcmdPointer.Offset += 4;
            mbbsEmuMemoryCore.SetPointer("NXTCMD", nxtcmdPointer);

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, CURUSR_ORDINAL, new List<ushort> { 1 });

            //Verify Results
            Assert.Equal(message, Encoding.ASCII.GetString(mbbsEmuMemoryCore.GetString("PRFBUF", true)));
            Assert.Equal(prfPointer, mbbsEmuMemoryCore.GetPointer("PRFPTR"));
            Assert.Equal(nxtcmdPointer, mbbsEmuMemoryCore.GetPointer("NXTCMD"));
        }

        [Fact]
        public void CURUSR_from_channel_independent_run_saves_channel_Test()
        {
            //Reset State
            Reset();
            majorbbs.SetState(ushort.MaxValue);

            //Execute Test
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, CURUSR_ORDINAL, new List<ushort> { 1 });
            mbbsEmuMemoryCore.SetByte(mbbsEmuMemoryCore.GetVariablePointer("VDA-1"), SENTINEL);
            SetSubstt(1, 9);
            majorbbs.UpdateSession(ushort.MaxValue);

            //Verify Results
            Assert.Equal(SENTINEL, testSessions[1].VDA[0]);
            Assert.Equal(9, testSessions[1].UsrPtr.Substt);
        }

        private void SetPendingInput(ushort channel, string input)
        {
            testSessions[channel].InputCommand = Encoding.ASCII.GetBytes(input + "\0");
            testSessions[channel].Status.Enqueue(EnumUserStatus.CR_TERMINATED_STRING_AVAILABLE);
        }

        private void SetSubstt(ushort channel, ushort value)
        {
            var userPointer = new FarPtr(mbbsEmuMemoryCore.GetVariablePointer("USER").Data);
            userPointer.Offset += (ushort)(User.Size * channel + 8);
            mbbsEmuMemoryCore.SetWord(userPointer, value);
        }
    }
}
