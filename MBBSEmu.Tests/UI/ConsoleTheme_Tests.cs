using FluentAssertions;
using MBBSEmu.UI;
using System;
using System.Linq;
using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using Xunit;

namespace MBBSEmu.Tests.UI
{
    [Collection("TerminalGui")]
    public class ConsoleTheme_Tests
    {
        [Fact]
        public void ApplyRetroStyle_RemovesShadowsFromTheViewAndEverythingInIt()
        {
            var button = new Button { Text = "OK" };
            var dialogButton = new Button { Text = "Cancel" };
            var dialog = new Dialog();
            dialog.Add(new View(), button);
            dialog.AddButton(dialogButton);
            button.ShadowStyle.Should().NotBe(ShadowStyles.None, "Terminal.Gui gives buttons a shadow by default");

            ConsoleTheme.ApplyRetroStyle(dialog);

            dialog.ShadowStyle.Should().Be(ShadowStyles.None);
            button.ShadowStyle.Should().Be(ShadowStyles.None);
            dialogButton.ShadowStyle.Should().Be(ShadowStyles.None);
            dialog.BorderStyle.Should().Be(LineStyle.Double);
        }

        /// <summary>
        ///     Message boxes and the Save dialog are created inside Terminal.Gui, so they're styled as they open
        /// </summary>
        [Fact]
        public void StyleDialogsOf_StylesMessageBoxesAsTheyOpen()
        {
            using var harness = new TerminalGuiHarness(80, 24);
            ConsoleTheme.StyleDialogsOf(harness.App);
            ShadowStyles? dialogShadow = null;
            ShadowStyles?[] buttonShadows = null;
            LineStyle? border = null;
            harness.App.AddTimeout(TimeSpan.FromMilliseconds(100), () =>
            {
                var messageBox = harness.App.TopRunnableView!;
                dialogShadow = messageBox.ShadowStyle;
                border = messageBox.BorderStyle;
                buttonShadows = ((Dialog)messageBox).Buttons.Select(x => x.ShadowStyle).ToArray();
                harness.App.RequestStop();
                return false;
            });

            MessageBox.Query(harness.App, "Shutdown", "Shut down?", "_Yes", "_No");

            dialogShadow.Should().Be(ShadowStyles.None);
            border.Should().Be(LineStyle.Double);
            buttonShadows.Should().NotBeEmpty().And.OnlyContain(x => x == ShadowStyles.None);
        }
    }
}
