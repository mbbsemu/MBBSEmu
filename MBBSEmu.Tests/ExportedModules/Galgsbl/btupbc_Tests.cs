using MBBSEmu.Session;
using MBBSEmu.Session.Enums;
using System.Collections.Generic;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Galgsbl
{
    public class btupbc_Tests : ExportedModuleTestBase
    {
        private const ushort BTUPBC_ORDINAL = 39;
        private const ushort BTUCPC_ORDINAL = 86;

        [Fact]
        public void Defaults_ArmedAtSessionStart_ConsumeMarkersFromAsciiOutput()
        {
            // The Major BBS arms pause (Control-T/0x14) and clear-pause-counter
            // (Control-S/0x13) host-side for every channel; modules like T-LORD embed
            // the markers without ever calling btupbc. Real MajorBBS 6.25 transmits
            // zero of either from the first byte of module output.
            var session = new TestSession(null, null);

            Assert.True(session.ScreenPauseEnabled);
            Assert.Equal(SessionBase.DefaultPauseCharacter, session.PauseCharacter);
            Assert.Equal(SessionBase.DefaultClearPauseCounterCharacter, session.ClearPauseCounterCharacter);

            session.SendToClient(new byte[] { 0x13, (byte)'A', 0x14, (byte)'B', 0x13 });
            Assert.Equal(new byte[] { (byte)'A', (byte)'B' }, session.DrainSentData());
        }

        [Fact]
        public void Defaults_LeaveDc1AndDc2Untouched()
        {
            var session = new TestSession(null, null);

            session.SendToClient(new byte[] { 0x11, (byte)'A', 0x12 });

            Assert.Equal(new byte[] { 0x11, (byte)'A', 0x12 }, session.DrainSentData());
        }

        [Fact]
        public void btupbc_OverridesDefaultPauseCharacter()
        {
            Reset();

            ExecuteApiTest(HostProcess.ExportedModules.Galgsbl.Segment, BTUPBC_ORDINAL, new List<ushort> { 0, 0x7E });

            Assert.True(testSessions[0].ScreenPauseEnabled);
            Assert.Equal((byte)0x7E, testSessions[0].PauseCharacter);
            // The clear-pause-counter default is independent and stays armed.
            Assert.Equal(SessionBase.DefaultClearPauseCounterCharacter, testSessions[0].ClearPauseCounterCharacter);
        }

        [Fact]
        public void btupbc_Zero_DisablesScreenPause_ClearCharStaysArmed()
        {
            Reset();

            ExecuteApiTest(HostProcess.ExportedModules.Galgsbl.Segment, BTUPBC_ORDINAL, new List<ushort> { 0, 0 });

            Assert.False(testSessions[0].ScreenPauseEnabled);

            var session = new TestSession(null, null)
            {
                ScreenPauseEnabled = testSessions[0].ScreenPauseEnabled,
                PauseCharacter = testSessions[0].PauseCharacter,
                ClearPauseCounterCharacter = testSessions[0].ClearPauseCounterCharacter,
            };

            // Pause disabled: 0x14 passes; the independent clear char still consumes.
            session.SendToClient(new byte[] { 0x14, (byte)'A', 0x13 });
            Assert.Equal(new byte[] { 0x14, (byte)'A' }, session.DrainSentData());
        }

        [Fact]
        public void btucpc_OverridesClearPauseCounterCharacter()
        {
            Reset();

            ExecuteApiTest(HostProcess.ExportedModules.Galgsbl.Segment, BTUCPC_ORDINAL, new List<ushort> { 0, 0x05 });

            Assert.Equal((byte)0x05, testSessions[0].ClearPauseCounterCharacter);
        }

        [Fact]
        public void btucpc_Zero_DisablesClearCharacter()
        {
            Reset();

            ExecuteApiTest(HostProcess.ExportedModules.Galgsbl.Segment, BTUCPC_ORDINAL, new List<ushort> { 0, 0 });

            Assert.Equal((byte)0, testSessions[0].ClearPauseCounterCharacter);
        }

        [Fact]
        public void BinaryOutputMode_PassesMarkersThroughAsGlyphs()
        {
            // FSD template painting (fsdbkg) is GSBL binary-mode output: DC1-DC4 are
            // CP437 display glyphs there (e.g. MajorMUD) and must not be consumed.
            var session = new TestSession(null, null) { BinaryOutputMode = true };

            session.SendToClient(new byte[] { 0x11, 0x12, 0x13, 0x14 });

            Assert.Equal(new byte[] { 0x11, 0x12, 0x13, 0x14 }, session.DrainSentData());
        }

        [Fact]
        public void FullScreenSessionStates_PassMarkersThroughAsGlyphs()
        {
            var session = new TestSession(null, null)
            {
                SessionState = EnumSessionState.InFullScreenDisplay,
            };

            session.SendToClient(new byte[] { 0x11, 0x12, 0x13, 0x14 });

            Assert.Equal(new byte[] { 0x11, 0x12, 0x13, 0x14 }, session.DrainSentData());
        }

        [Fact]
        public void ResetScreenPauseState_RestoresDefaults_AcrossDoorVisits()
        {
            // A door overrides the arming (here: disables both entirely)...
            var session = new TestSession(null, null)
            {
                ScreenPauseEnabled = false,
                PauseCharacter = 0,
                ClearPauseCounterCharacter = 0,
            };

            // ...then the user exits it (MbbsHost.ExitModule calls ResetScreenPauseState),
            // which must restore the Major BBS per-channel defaults for the next door.
            session.ResetScreenPauseState();

            Assert.True(session.ScreenPauseEnabled);
            Assert.Equal(SessionBase.DefaultPauseCharacter, session.PauseCharacter);
            Assert.Equal(SessionBase.DefaultClearPauseCounterCharacter, session.ClearPauseCounterCharacter);

            session.SendToClient(new byte[] { 0x13, (byte)'A', 0x14 });
            Assert.Equal(new byte[] { (byte)'A' }, session.DrainSentData());
        }
    }
}
