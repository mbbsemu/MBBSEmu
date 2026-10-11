using MBBSEmu.Memory;
using MBBSEmu.Session;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Galgsbl
{
    /// <summary>
    ///     GSBL routines that change a channel's output settings, ported from GALGSBL's unit tests
    ///     (test_settings.cpp, test_output.cpp)
    /// </summary>
    public class GsblOutputSettings_Tests : ExportedModuleTestBase
    {
        private const ushort BTUCLO_ORDINAL = 7;
        private const ushort BTUHPK_ORDINAL = 17;
        private const ushort BTUXMN_ORDINAL = 58;
        private const ushort BTUXNF_ORDINAL = 60;

        private SessionBase Channel => testSessions[0];

        private void Call(ushort ordinal, params ushort[] arguments) =>
            ExecuteApiTest(HostProcess.ExportedModules.Galgsbl.Segment, ordinal, new List<ushort>(arguments));

        /// <summary>
        ///     Replaces channel 0 with a session that has no text variables, which need a host
        /// </summary>
        private TestSession OutputChannel()
        {
            var session = new TestSession(null, null);
            testSessions[0] = session;
            return session;
        }

        [Fact]
        public void btuxnf_PositiveXoffTurnsPagingOff()
        {
            Reset();
            Channel.GsblOutput.SetFlowControl(0, true, 24, Encoding.ASCII.GetBytes("More?"));
            Channel.GsblOutput.PageLineCount = 5;
            Channel.GsblOutput.NonStop = true;

            Call(BTUXNF_ORDINAL, 0, 0x11, 0x13, 99);

            Assert.Equal(0x11, Channel.GsblOutput.XonCharacter);
            Assert.Equal(0x13, Channel.GsblInput.XoffCharacter);
            Assert.Equal(0, Channel.GsblOutput.PageLength);
            Assert.Equal(0, Channel.GsblOutput.PageLineCount);
            Assert.False(Channel.GsblOutput.NonStop);
            Assert.Equal(0, mbbsEmuCpuRegisters.AX);
        }

        [Fact]
        public void btuxnf_NegativeXoffTurnsPagingOn()
        {
            Reset();
            var prompt = mbbsEmuMemoryCore.AllocateVariable("PROMPT", 6);
            mbbsEmuMemoryCore.SetArray(prompt, Encoding.ASCII.GetBytes("More?\0"));
            Channel.GsblOutput.PageLineCount = 5;
            Channel.GsblOutput.NonStop = true;

            Call(BTUXNF_ORDINAL, 0, 0x11, unchecked((ushort)-0x13), 24, prompt.Offset, prompt.Segment);

            Assert.Equal(0x13, Channel.GsblInput.XoffCharacter);
            Assert.Equal(24, Channel.GsblOutput.PageLength);
            Assert.Equal("More?", Encoding.ASCII.GetString(Channel.GsblOutput.PausePrompt));
            Assert.Equal(0, Channel.GsblOutput.PageLineCount); //fresh page: paging was off
            Assert.False(Channel.GsblOutput.NonStop);
        }

        [Fact]
        public void btuxnf_StoresXoffAsAChar()
        {
            Reset();

            Call(BTUXNF_ORDINAL, 0, 0, 0x141, 0);

            Assert.Equal(0x41, Channel.GsblInput.XoffCharacter);
        }

        [Fact]
        public void btuhpk_InstallsAndRemovesTheHandler()
        {
            Reset();

            Call(BTUHPK_ORDINAL, 0, 0x1234, 0x0002);
            Assert.Equal(new FarPtr(0x0002, 0x1234), Channel.GsblOutput.PageKeyRoutine);
            Assert.Equal(0, mbbsEmuCpuRegisters.AX);

            Call(BTUHPK_ORDINAL, 0, 0, 0);
            Assert.Null(Channel.GsblOutput.PageKeyRoutine);
        }

        [Fact]
        public void btuhpk_UndefinedChannel()
        {
            Reset();

            Call(BTUHPK_ORDINAL, 99, 0x1234, 0x0002);

            Assert.Equal(unchecked((ushort)-10), mbbsEmuCpuRegisters.AX);
        }

        [Fact]
        public void btuclo_DiscardsHeldBtuxmtText()
        {
            Reset();
            var session = OutputChannel();
            session.GsblOutput.PauseForXoff();
            session.SendToClient(Encoding.ASCII.GetBytes("discarded"));

            Call(BTUCLO_ORDINAL, 0);
            session.GsblOutput.WaitKey((byte)' ');

            Assert.Empty(session.DrainSentData());
            Assert.Equal(0, mbbsEmuCpuRegisters.AX);
        }

        [Fact]
        public void btuxmn_TextSurvivesBtuclo()
        {
            Reset();
            var session = OutputChannel();
            var text = mbbsEmuMemoryCore.AllocateVariable("TEXT", 5);
            mbbsEmuMemoryCore.SetArray(text, Encoding.ASCII.GetBytes("kept\0"));
            session.GsblOutput.PauseForXoff();

            Call(BTUXMN_ORDINAL, 0, text.Offset, text.Segment);
            Call(BTUCLO_ORDINAL, 0);
            session.GsblOutput.WaitKey((byte)' ');

            Assert.Equal("kept", Encoding.ASCII.GetString(session.DrainSentData()));
        }
    }
}
