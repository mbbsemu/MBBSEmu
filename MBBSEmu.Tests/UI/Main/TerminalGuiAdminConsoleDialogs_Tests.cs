using FluentAssertions;
using MBBSEmu.UI.Main;
using System;
using Terminal.Gui.Input;
using Terminal.Gui.Testing;
using Xunit;

namespace MBBSEmu.Tests.UI.Main
{
    [Collection("TerminalGui")]
    public class TerminalGuiAdminConsoleDialogs_Tests : IDisposable
    {
        private readonly TerminalGuiHarness _harness = new(80, 24);
        private readonly TerminalGuiAdminConsoleDialogs _dialogs;

        public TerminalGuiAdminConsoleDialogs_Tests()
        {
            _dialogs = new TerminalGuiAdminConsoleDialogs(_harness.App, "test");
        }

        public void Dispose() => _harness.Dispose();

        /// <summary>
        ///     Presses keys once the dialog is up (the prompt blocks until answered)
        /// </summary>
        private void PressWhenShown(params Key[] keys)
        {
            _harness.App.AddTimeout(TimeSpan.FromMilliseconds(100), () =>
            {
                foreach (var key in keys)
                    _harness.App.InjectKey(key);
                return false;
            });

            //If the keys didn't close the dialog, close it so the test fails instead of hanging
            _harness.App.AddTimeout(TimeSpan.FromSeconds(2), () =>
            {
                _harness.App.RequestStop();
                return false;
            });
        }

        [Fact]
        public void ConfirmShutdown_EnterChoosesTheDefaultNo()
        {
            PressWhenShown(Key.Enter);

            _dialogs.ConfirmShutdown().Should().BeFalse();
        }

        [Fact]
        public void ConfirmShutdown_EscChoosesNo()
        {
            PressWhenShown(Key.Esc);

            _dialogs.ConfirmShutdown().Should().BeFalse();
        }

        [Fact]
        public void ConfirmShutdown_YHotkeyChoosesYes()
        {
            PressWhenShown(Key.Y);

            _dialogs.ConfirmShutdown().Should().BeTrue();
        }

        [Fact]
        public void ConfirmForceQuit_EnterChoosesTheDefaultNo()
        {
            PressWhenShown(Key.Enter);

            _dialogs.ConfirmForceQuit().Should().BeFalse();
        }
    }
}
