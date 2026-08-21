using FluentAssertions;
using MBBSEmu.Tests.Util;
using System;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;

namespace MBBSEmu.Tests.Integration
{
    /// <summary>
    ///     Wire-invariant tests for T-LORD's GSBL screen-pause markers: the module
    ///     embeds 0x13 (clear pause counter) and 0x14 (pause) in its output, relying
    ///     on the host's screen-pause subsystem to consume them. Ground truth: real
    ///     MajorBBS 6.25 emits zero 0x11-0x14 bytes over this same journey
    ///     (joint-mbbs/docs/sessions/2026-08-17/captures/tlord-real-*.bin).
    /// </summary>
    [Collection("Non-Parallel")]
    public class Tlord_ScreenPause_Tests : MBBSEmuIntegrationTestBase
    {
        [ModuleFact("RTSLORD")]
        public void ScreenPauseMarkersConsumed()
        {
            var moduleSourcePath = ModuleFactAttribute.GetModulePath("RTSLORD");

            //Self-check the assertion is non-vacuous: the module's MSG really embeds
            //the markers, so absent host-side consumption they would hit the wire
            var msgBytes = File.ReadAllBytes(Path.Combine(moduleSourcePath, "RTSLORD.MSG"));
            msgBytes.Count(b => b == 0x13 || b == 0x14).Should().BeGreaterThan(0,
                "RTSLORD.MSG should contain GSBL screen-pause markers");

            ExecuteTest("RTSLORD", moduleSourcePath, (session, host) =>
            {
                //Journey mirroring the real-MBBS golden captures: intro art, title
                //page, main menu, (E)nter the realm, join, create a character, page
                //through stats and happenings into the Town Square
                session.DrainSentData(TimeSpan.FromSeconds(2));
                session.SendToModule(new[] { (byte)' ' });             //past <MORE>
                session.DrainSentData(TimeSpan.FromSeconds(2));
                session.SendToModule(new[] { (byte)'E', (byte)'\r' }); //enter the realm
                session.DrainSentData(TimeSpan.FromSeconds(2));
                session.SendToModule(Encoding.ASCII.GetBytes("Y"));    //join the realm
                session.DrainSentData(TimeSpan.FromSeconds(2));
                session.SendToModule(Encoding.ASCII.GetBytes("Testwarrior\r")); //alias
                for (var i = 0; i < 6; i++)
                {
                    //Accept defaults through creation, then page into the realm
                    session.DrainSentData(TimeSpan.FromSeconds(2));
                    session.SendToModule(new[] { (byte)'\r' });
                }
                session.DrainSentData(TimeSpan.FromSeconds(2));
                session.SendToModule(new[] { (byte)'C' });             //past happenings
                session.DrainSentData(TimeSpan.FromSeconds(2));
                session.SendToModule(new[] { (byte)'L' });             //List Warriors pager
                session.DrainSentData(TimeSpan.FromSeconds(2));

                var transcript = session.GetCapturedOutput();

                //Journey completed: we made it into the realm proper
                Encoding.ASCII.GetString(transcript).Should().Contain("Town Square",
                    "the scripted journey should reach the Town Square");

                //The wire invariant, matching the real-MBBS golden captures: the
                //screen-pause markers are host-consumed, never transmitted
                GoldenAssert.ContainsNone(transcript, 0x11, 0x12, 0x13, 0x14);
            });
        }
    }
}
