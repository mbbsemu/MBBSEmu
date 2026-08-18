using FluentAssertions;
using MBBSEmu.Tests.Util;
using System;
using System.Text;
using Xunit;

namespace MBBSEmu.Tests.Integration
{
    /// <summary>
    ///     Wire-invariant test for MajorMUD's CP437 glyph bytes (#553): the FSD
    ///     character-creation worksheet embeds DC1-DC4 values as display glyphs
    ///     (e.g. 0x11 = ◄ in its stat sliders), which real MajorBBS transmits.
    ///     With screen-pause defaults armed host-side, these must still pass
    ///     because FSD painting is GSBL binary-mode output.
    /// </summary>
    [Collection("Non-Parallel")]
    public class Mmud_GlyphPassthrough_Tests : MBBSEmuIntegrationTestBase
    {
        [ModuleFact("WCCMMUD")]
        public void FsdWorksheetGlyphsReachTheWire()
        {
            ExecuteTest("WCCMMUD", ModuleFactAttribute.GetModulePath("WCCMMUD"), (session, host) =>
            {
                //Journey to the FSD stats worksheet: top menu, (E)nter the realm,
                //race, class, decline Lawful — the worksheet paints via fsdbkg
                session.DrainSentData(TimeSpan.FromSeconds(3));
                session.SendToModule(Encoding.ASCII.GetBytes("E\r"));
                session.DrainSentData(TimeSpan.FromSeconds(3));
                session.SendToModule(Encoding.ASCII.GetBytes("1\r"));   //race: Human
                session.DrainSentData(TimeSpan.FromSeconds(3));
                session.SendToModule(Encoding.ASCII.GetBytes("1\r"));   //class: Warrior
                session.DrainSentData(TimeSpan.FromSeconds(3));
                session.SendToModule(Encoding.ASCII.GetBytes("N\r"));   //not Lawful
                var worksheet = session.DrainSentData(TimeSpan.FromSeconds(3));

                worksheet.Length.Should().BeGreaterThan(1000,
                    "the journey should reach and paint the FSD stats worksheet");
                session.SessionState.ToString().Should().Contain("FullScreen",
                    "the session should be in the FSD worksheet when the journey ends");

                //The glyph bytes survive to the wire despite armed screen-pause defaults
                worksheet.Should().Contain((byte)0x11,
                    "the worksheet's ◄ stat-slider glyphs must not be consumed");
            });
        }
    }
}
