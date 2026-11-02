using FluentAssertions;
using System;
using System.Text;
using Xunit;

namespace MBBSEmu.Tests.Integration
{
    /// <summary>
    ///     The Major BBS turns word wrap off for full-screen data entry (fsdbkg) and back on at the
    ///     user's screen width when it ends (fsdcof in FSDBBS.C), whatever width a module had set
    /// </summary>
    [Collection("Non-Parallel")]
    public class FullScreenWordWrap_Tests : MBBSEmuIntegrationTestBase
    {
        [Fact]
        public void WordWrapIsOffDuringFullScreenEntryAndBackAtScreenWidthAfter()
        {
            ExecuteTest((session, host) =>
            {
                WaitUntil(':', "Make your selection");
                session.WordWrapWidth = 79; //as MajorMUD sets it

                session.SendToModule(Encoding.ASCII.GetBytes("D\r\n"));
                session.DrainSentData(TimeSpan.FromSeconds(3));
                session.SessionState.ToString().Should().Contain("FullScreen");
                session.WordWrapWidth.Should().Be(0);

                //Enter through every field, filling the ones that need a minimum length, then QUIT at Exit
                for (var i = 0; i < 40 && session.SessionState.ToString().Contains("FullScreen"); i++)
                {
                    session.SendToModule(Encoding.ASCII.GetBytes("\r"));
                    var output = Encoding.ASCII.GetString(session.DrainSentData(TimeSpan.FromMilliseconds(200)));
                    if (output.Contains("Choices: SAVE EDIT QUIT"))
                    {
                        session.SendToModule(Encoding.ASCII.GetBytes("Q"));
                        session.DrainSentData(TimeSpan.FromMilliseconds(200));
                    }
                    else if (output.Contains("Enter at least"))
                    {
                        session.SendToModule(Encoding.ASCII.GetBytes("12345678"));
                        session.DrainSentData(TimeSpan.FromMilliseconds(200));
                    }
                }

                session.SessionState.ToString().Should().NotContain("FullScreen");
                session.WordWrapWidth.Should().Be(session.UsrAcc.scnwid);
            });
        }
    }
}
