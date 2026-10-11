using MBBSEmu.HostProcess.Enums;
using MBBSEmu.Session;
using System.Linq;
using System.Text;
using Xunit;

namespace MBBSEmu.Tests.Session
{
    /// <summary>
    ///     GSBL output processing: screen-pause and clear-pause characters, paging, XON/XOFF and
    ///     btuclo(), ported from GALGSBL's output unit tests (test_output.cpp)
    /// </summary>
    public class GsblOutput_Tests
    {
        private readonly TestSession _session = new TestSession(null, null);
        private readonly byte[] _translationTable = GsblInput.DefaultTranslationTable.ToArray();

        private GsblChannelOutput Output => _session.GsblOutput;

        private void Xmt(string text) => _session.SendToClient(Encoding.Latin1.GetBytes(text));

        private void Xmn(string text) =>
            _session.SendToClient(Encoding.Latin1.GetBytes(text), EnumGsblOutputKind.ClearProofText);

        private void Type(string input)
        {
            foreach (var c in Encoding.Latin1.GetBytes(input))
                GsblInput.ProcessCharacter(_session, c, _translationTable, null);
        }

        private string Wire() => Encoding.Latin1.GetString(_session.DrainSentData());

        private string Line()
        {
            var line = Encoding.Latin1.GetString(_session.InputBuffer.ToArray());
            _session.InputBuffer.SetLength(0);
            GsblInput.Synchronize(_session);
            return line;
        }

        private void PagingOn(int lines, string prompt = "More?") =>
            Output.SetFlowControl(0, true, lines, Encoding.Latin1.GetBytes(prompt));

        // --- screen-pause and clear-pause characters ---------------------------------------

        [Fact]
        public void ThePauseCharacterIsSwallowed()
        {
            Xmt("a\x14" + "b");

            Assert.Equal("ab", Wire());
        }

        [Fact]
        public void WithNothingOnThePageAClearScreenDoesNotPause()
        {
            PagingOn(10);

            Xmt("a\fb");

            Assert.Equal("a\fb", Wire());
            Assert.False(Output.IsPaused);
        }

        [Fact]
        public void AFormFeedPausesAPartlyFilledPage()
        {
            PagingOn(10, "More");

            Xmt("a\r\n\fb");

            Assert.Equal("a\r\nMore", Wire());
            Assert.Equal(EnumGsblOutputPause.Page, Output.Pause);

            Type(" ");
            Assert.Equal("\b\b\b\b\fb", Wire());
            Assert.False(Output.IsPaused);
        }

        [Fact]
        public void AnAnsiClearScreenPausesAPartlyFilledPage()
        {
            PagingOn(10, "More");

            Xmt("a\r\n\x1b[2J\x1b[2K");

            Assert.Equal("a\r\nMore", Wire());
            Type(" ");
            Assert.Equal("\b\b\b\b\x1b[2J\x1b[2K", Wire());
        }

        [Fact]
        public void ThePauseCharacterPausesAPartlyFilledPage()
        {
            PagingOn(10, "More");

            Xmt("a\r\n\x14" + "b");

            Assert.Equal("a\r\nMore", Wire());
            Type(" ");
            Assert.Equal("\b\b\b\bb", Wire());
        }

        [Fact]
        public void WithoutAPromptThePauseCharacterOnlyStartsANewPage()
        {
            Xmt("a\r\n\fb");

            Assert.Equal("a\r\n\fb", Wire());
            Assert.False(Output.IsPaused);
            Assert.Equal(0, Output.PageLineCount);
        }

        [Fact]
        public void AClearScreenIsNotSpecialWithScreenPauseDisabled()
        {
            PagingOn(10, "More");
            _session.ScreenPauseEnabled = false;

            Xmt("a\r\n\fb");

            Assert.Equal("a\r\n\fb", Wire());
            Assert.Equal(1, Output.PageLineCount);
        }

        [Fact]
        public void TheClearPauseCharacterResetsThePageCount()
        {
            Xmt("1\r\n2\r\n");
            Assert.Equal(2, Output.PageLineCount);

            Xmt("\x13" + "x");

            Assert.Equal(0, Output.PageLineCount);
            Assert.Equal("1\r\n2\r\nx", Wire());
        }

        [Fact]
        public void ScreenPauseMarkersTakeNoColumnWhenLinesAreBroken()
        {
            _session.WordWrapWidth = 10;

            Xmt("\x13\x13\x13" + "0123456789 next\r\n");

            Assert.Equal("0123456789\r\nnext\r\n", Wire());
        }

        [Fact]
        public void BinaryOutputIsNotCountedAndKeepsItsMarkers()
        {
            PagingOn(1);
            _session.BinaryOutputMode = true;

            Xmt("\x13" + "1\r\n2\r\n\x14");

            Assert.Equal("\x13" + "1\r\n2\r\n\x14", Wire());
            Assert.Equal(0, Output.PageLineCount);
        }

        // --- paging --------------------------------------------------------------------------

        [Fact]
        public void PausesWhenThePageIsFull()
        {
            PagingOn(2);

            Xmt("1\r\n2\r\n3\r\n4\r\n");

            Assert.Equal("1\r\n2\r\nMore?", Wire());
            Assert.Equal(EnumGsblOutputPause.Page, Output.Pause);

            Type(" ");
            Assert.Equal("\b\b\b\b\b3\r\n4\r\n", Wire());
            Assert.Equal(0, _session.InputBuffer.Length);
        }

        [Fact]
        public void AnExactlyFullLastPageDoesNotPause()
        {
            PagingOn(2);

            Xmt("1\r\n2\r\n");

            Assert.Equal("1\r\n2\r\n", Wire());
            Assert.False(Output.IsPaused);
        }

        [Fact]
        public void OutputWrittenWhilePausedIsSentInOrderOnResume()
        {
            PagingOn(2);

            Xmt("1\r\n2\r\n3\r\n");
            Xmt("4\r\n");
            Wire();

            Type(" ");

            Assert.Equal("\b\b\b\b\b3\r\n4\r\n", Wire());
        }

        [Fact]
        public void HeldOutputCanPauseAgain()
        {
            PagingOn(2);

            Xmt("1\r\n2\r\n3\r\n4\r\n5\r\n6\r\n");
            Assert.Equal("1\r\n2\r\nMore?", Wire());

            Type(" ");
            Assert.Equal("\b\b\b\b\b3\r\n4\r\nMore?", Wire());

            Type(" ");
            Assert.Equal("\b\b\b\b\b5\r\n6\r\n", Wire());
        }

        [Fact]
        public void KeysAfterTheResumingOneAreInput()
        {
            PagingOn(2);
            Xmt("1\r\n2\r\n3\r\n");

            Type(" ab\r");

            Assert.Equal("ab", Line());
        }

        [Fact]
        public void LockedInputCannotResume()
        {
            PagingOn(2);
            Xmt("1\r\n2\r\n3\r\n");

            _session.InputLockout = true;
            Type(" ");
            Assert.False(Output.WaitKey((byte)' '));

            Assert.True(Output.IsPaused);
        }

        [Fact]
        public void LockedInputStopsThePageCount()
        {
            PagingOn(2);
            _session.InputLockout = true;

            Xmt("1\r\n2\r\n3\r\n4\r\n");

            Assert.Equal("1\r\n2\r\n3\r\n4\r\n", Wire());
        }

        [Fact]
        public void CompletedInputRestartsThePage()
        {
            PagingOn(2);
            Xmt("1\r\n");
            Assert.Equal(1, Output.PageLineCount);

            Type("x\r");

            Assert.Equal(0, Output.PageLineCount);
        }

        [Fact]
        public void TheLineEndingTheCompletedInputIsNotCounted()
        {
            PagingOn(2);
            Type("x\r");

            Xmt("\r\n1\r\n");

            Assert.Equal(1, Output.PageLineCount);
        }

        [Fact]
        public void TurningPagingOffResetsThePage()
        {
            PagingOn(2);
            Xmt("1\r\n");

            Output.SetFlowControl(0, false, 99, null);

            Assert.Equal(0, Output.PageLength);
            Assert.Equal(0, Output.PageLineCount);
        }

        [Fact]
        public void TurningPagingOnAgainKeepsThePageCount()
        {
            PagingOn(5);
            Xmt("1\r\n2\r\n");

            PagingOn(10);

            Assert.Equal(10, Output.PageLength);
            Assert.Equal(2, Output.PageLineCount);
        }

        // --- XOFF / XON ----------------------------------------------------------------------

        [Fact]
        public void XoffStopsOutput()
        {
            Type("\x13");
            Assert.Equal(EnumGsblOutputPause.Xoff, Output.Pause);

            Xmt("abc");
            Assert.Equal("", Wire());

            //With no XON character set, any key resumes, and isn't input
            Type("q");
            Assert.Equal("abc", Wire());
            Assert.Equal(0, _session.InputBuffer.Length);
        }

        [Fact]
        public void XoffIgnoresParity()
        {
            Type("\x93");

            Assert.Equal(EnumGsblOutputPause.Xoff, Output.Pause);
        }

        [Fact]
        public void OnlyXonResumesWhenSet()
        {
            Output.SetFlowControl(0x11, false, 0, null);
            Type("\x13");
            Xmt("abc");

            Type("q");
            Assert.True(Output.IsPaused);

            Type("\x11");
            Assert.Equal("abc", Wire());

            Type("xy\r");
            Assert.Equal("xy", Line());
        }

        [Fact]
        public void XoffCanBeDisabled()
        {
            _session.GsblInput.XoffCharacter = 0;

            Type("\x13");

            Assert.False(Output.IsPaused);
        }

        [Fact]
        public void EchoIsHeldWhileOutputIsPaused()
        {
            Type("\x13");
            Xmt("abc");

            _session.SendEchoToClient(Encoding.ASCII.GetBytes("e"));
            Assert.Equal("", Wire());

            Type(" ");
            Assert.Equal("abce", Wire());
        }

        [Fact]
        public void OutputEmptyIsReportedOnlyOnceHeldOutputIsSent()
        {
            _session.OutputEmptyStatus = true;
            Type("\x13");

            Xmt("abc");
            Assert.DoesNotContain(EnumUserStatus.OUTPUT_BUFFER_EMPTY, _session.Status);

            Type(" ");
            Assert.Contains(EnumUserStatus.OUTPUT_BUFFER_EMPTY, _session.Status);
        }

        [Fact]
        public void ResumingWithNothingHeldDoesNotReportOutputEmpty()
        {
            _session.OutputEmptyStatus = true;
            Type("\x13");

            Type(" ");

            Assert.DoesNotContain(EnumUserStatus.OUTPUT_BUFFER_EMPTY, _session.Status);
        }

        // --- btuclo / btuxmn -----------------------------------------------------------------

        [Fact]
        public void BtucloDiscardsBtuxmtText()
        {
            Type("\x13");
            Xmt("aaa");
            Xmt("bbb");

            Output.Clear();
            Type(" ");

            Assert.Equal("", Wire());
        }

        [Fact]
        public void BtucloKeepsBtuxmnText()
        {
            Type("\x13");
            Xmt("AAA");
            Xmn("BBB");
            Xmt("CCC");

            Output.Clear();
            Type(" ");

            Assert.Equal("BBB", Wire());
        }

        [Fact]
        public void BtucloKeepsTextBetweenBtuxmnBlocks()
        {
            Type("\x13");
            Xmn("AAA");
            Xmt("BBB");
            Xmn("CCC");

            Output.Clear();
            Type(" ");

            Assert.Equal("AAABBBCCC", Wire());
        }

        [Fact]
        public void BtucloKeepsEcho()
        {
            Type("\x13");
            Xmt("aaa");
            _session.SendEchoToClient(Encoding.ASCII.GetBytes("e"));

            Output.Clear();
            Type(" ");

            Assert.Equal("e", Wire());
        }

        [Fact]
        public void BtucloResetsPagingAndMarksOutputComplete()
        {
            PagingOn(5);
            Xmt("1\r\n2\r\n");
            Output.NonStop = true;

            Output.Clear();

            Assert.False(Output.NonStop);
            Assert.Equal(0, Output.PageLineCount);
            Assert.True(Output.OutputComplete);
        }

        [Fact]
        public void ResetDiscardsHeldOutputAndUnpauses()
        {
            Type("\x13");
            Xmt("abc");

            Output.Reset();

            Assert.False(Output.IsPaused);
            Assert.False(Output.HasHeldOutput);
        }
    }
}
