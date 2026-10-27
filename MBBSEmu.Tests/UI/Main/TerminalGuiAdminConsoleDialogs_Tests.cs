using FluentAssertions;
using MBBSEmu.UI.Main;
using System;
using System.IO;
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

        /// <summary>
        ///     Waits for the picker to show something, keeps what's on screen, then closes it without picking
        /// </summary>
        private Func<string> CloseWhenShown(string waitFor)
        {
            string screen = null;
            var started = DateTime.UtcNow;
            _harness.App.AddTimeout(TimeSpan.FromMilliseconds(100), () =>
            {
                _harness.App.LayoutAndDraw(true);
                var current = _harness.App.ToString();
                if (!current!.Contains(waitFor) && DateTime.UtcNow - started < TimeSpan.FromSeconds(3))
                    return true;

                screen = current;
                _harness.App.RequestStop(_harness.App.TopRunnable);
                return false;
            });
            return () => screen;
        }

        [Fact]
        public void PickModuleDefinition_ShowsOnlyMdfFiles()
        {
            var folder = Path.Combine(Path.GetTempPath(), $"mbbsemu-picker-{Guid.NewGuid():N}");
            Directory.CreateDirectory(folder);
            try
            {
                foreach (var file in new[] { "GAME.MDF", "GAME.DLL", "game2.mdf" })
                    File.WriteAllText(Path.Combine(folder, file), "");
                var screen = CloseWhenShown("GAME.MDF");

                var picked = _dialogs.PickModuleDefinition(folder);

                picked.Should().BeNull("the picker was closed without choosing");
                screen().Should().Contain("GAME.MDF").And.Contain("game2.mdf").And.NotContain("GAME.DLL");
            }
            finally
            {
                Directory.Delete(folder, recursive: true);
            }
        }

        [Fact]
        public void PickModuleDefinition_MissingStartFolder_OpensAnyway()
        {
            var screen = CloseWhenShown("Load Module");

            var picked = _dialogs.PickModuleDefinition(Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}"));

            picked.Should().BeNull();
            screen().Should().Contain("Load Module");
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
