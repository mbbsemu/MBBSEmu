using FluentAssertions;
using MBBSEmu.UI.Setup;
using System;
using System.Collections.Generic;
using System.Linq;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Xunit;
using Attribute = Terminal.Gui.Drawing.Attribute;

namespace MBBSEmu.Tests.UI.Setup
{
    [Collection("TerminalGui")]
    public class SetupWizardView_Tests : IDisposable
    {
        private sealed class FakeDialogs : ISetupWizardDialogs
        {
            public readonly List<(string Title, string Message)> Errors = new();
            public readonly Queue<bool> ConfirmAnswers = new();
            public readonly List<string> Saved = new();

            public void ShowError(string title, string message) => Errors.Add((title, message));

            public bool ConfirmWrite() => ConfirmAnswers.Count == 0 || ConfirmAnswers.Dequeue();

            public void ShowSaved(string path) => Saved.Add(path);
        }

        private readonly FakeDialogs _dialogs = new();
        private readonly SetupWizardModel _model = new(new Random(1));
        private const string SettingsPath = "/home/appsettings.json";

        private readonly List<(string Path, string Contents)> _writes = new();
        private Exception _writeFailure;
        private SetupWizardView _view;
        private TerminalGuiHarness _harness;

        public void Dispose() => _harness?.Dispose();

        private string[] Show(int width = 80, int height = 24)
        {
            _view = new SetupWizardView(_model, _dialogs, SettingsPath, (path, contents) =>
            {
                if (_writeFailure != null)
                    throw _writeFailure;

                _writes.Add((path, contents));
            });
            _harness = new TerminalGuiHarness(width, height);
            _harness.Show(_view);
            _view.ApplyScreenSize(width, height);
            return _harness.Screen();
        }

        private void Next()
        {
            _view.Wizard.NextFinishButton.InvokeCommand(Command.Accept);
            _harness.Screen();
        }

        private void GoTo(EnumSetupStep step)
        {
            for (var i = 0; i < 7 && _view.CurrentStep != step; i++)
                Next();

            _view.CurrentStep.Should().Be(step);
        }

        /// <summary>
        ///     Pressing Enter through the wizard accepts each step as shown; it must not also pick whichever option the
        ///     focused selector's cursor is on (here turning a disabled Rlogin on)
        /// </summary>
        [Fact]
        public void Enter_OnADisabledServiceStep_KeepsItDisabled()
        {
            Show();
            GoTo(EnumSetupStep.Rlogin);
            _model.RloginEnabled.Should().BeFalse();

            _harness.Key(Key.Enter);

            _model.RloginEnabled.Should().BeFalse();
            _view.CurrentStep.Should().Be(EnumSetupStep.Advanced, "Enter presses Next");
        }

        [Fact]
        public void At80x24_ShowsWizardInsideHostWindow()
        {
            var screen = Show();

            screen.Should().Contain(x => x.Contains("MBBSEmu Initial Setup"));
            screen.Should().Contain(x => x.Contains("Welcome to MBBSEmu!"));
        }

        [Fact]
        public void Below80x24_ShowsTooSmallScreen()
        {
            var screen = Show(79, 24);

            screen.Should().Contain(x => x.Contains("Terminal too small"));
            screen.Should().Contain(x => x.Contains("Esc cancels setup."));
            screen.Should().NotContain(x => x.Contains("Welcome to MBBSEmu!"));
        }

        [Fact]
        public void PortField_AcceptsDigitsOnly()
        {
            Show();
            GoTo(EnumSetupStep.Telnet);
            _view.TelnetPortField.SetFocus();
            _view.TelnetPortField.Text = "";

            _harness.Key(Key.D2);
            _harness.Key(Key.D3);
            _harness.Key(Key.A);

            _view.TelnetPortField.Text.Should().Be("23");
            _model.TelnetPort.Should().Be("23");
        }

        [Fact]
        public void TelnetDisabled_GreysOutItsSettings()
        {
            Show();
            GoTo(EnumSetupStep.Telnet);

            _view.TelnetEnabledSelector.Value = 1;

            _model.TelnetEnabled.Should().BeFalse();
            _view.TelnetPortField.Enabled.Should().BeFalse();
            _view.TelnetHeartbeatSelector.Enabled.Should().BeFalse();

            _view.TelnetEnabledSelector.Value = 0;

            _view.TelnetPortField.Enabled.Should().BeTrue();
            _view.TelnetHeartbeatSelector.Enabled.Should().BeTrue();
        }

        /// <summary>
        ///     Rlogin starts disabled, so its settings start greyed out
        /// </summary>
        [Fact]
        public void RloginDisabledByDefault_StartsGreyedOut()
        {
            Show();
            GoTo(EnumSetupStep.Rlogin);

            _view.RloginPortField.Enabled.Should().BeFalse();
            _view.RloginRemoteIPField.Enabled.Should().BeFalse();
            _view.RloginPortPerModuleSelector.Enabled.Should().BeFalse();

            _view.RloginEnabledSelector.Value = 0;

            _view.RloginPortField.Enabled.Should().BeTrue();
            _view.RloginRemoteIPField.Enabled.Should().BeTrue();
            _view.RloginPortPerModuleSelector.Enabled.Should().BeTrue();
        }

        [Fact]
        public void TypingInField_UpdatesModel()
        {
            Show();
            GoTo(EnumSetupStep.General);
            _view.BbsNameField.SetFocus();
            _view.BbsNameField.Text = "";

            _harness.Key(Key.X.WithShift);
            _harness.Key(Key.Y.WithShift);

            _model.BbsName.Should().Be("XY");
        }

        [Fact]
        public void FieldFocus_ShowsItsHelp()
        {
            Show();
            GoTo(EnumSetupStep.General);

            _view.ChannelsField.SetFocus();

            _view.HelpPanel(EnumSetupStep.General).Text.Should().Be(SetupWizardText.ChannelsHelp);
        }

        /// <summary>
        ///     The Welcome step has no fields, so its text is the step's content rather than a narrow help column
        /// </summary>
        [Fact]
        public void WelcomeText_UsesTheWholeStep()
        {
            var screen = Show(132, 40);

            screen.Should().Contain(x => x.Contains("Welcome to The MajorBBS Emulation Project!"));
            screen.Should().Contain(x => x.Contains("MBBSEmu was created to allow quick, easy access to the Games we all love"));
        }

        /// <summary>
        ///     Terminal.Gui's built-in help pane is a fixed 25 columns drawn in dark grey on black; the wizard's own
        ///     panel uses the step's colors and grows with the terminal
        /// </summary>
        [Fact]
        public void HelpPanel_IsReadableAndWidensOnLargeTerminals()
        {
            Show(200, 60);
            GoTo(EnumSetupStep.General);
            _view.BbsNameField.SetFocus();

            _harness.Screen().Should().Contain(x =>
                x.Contains("The Name of your BBS or the Name you want to give"));

            //DOS light gray (#AAAAAA) on DOS blue (#0000AA) in every role, rather than Terminal.Gui's derived dim
            //"read-only" color
            var scheme = _view.HelpPanel(EnumSetupStep.General).GetScheme();
            var helpColors = new Attribute(new Color(0xAA, 0xAA, 0xAA), new Color(0x00, 0x00, 0xAA));
            scheme.Normal.Should().Be(helpColors);
            scheme.ReadOnly.Should().Be(helpColors);
            scheme.Editable.Should().Be(helpColors);
        }

        [Fact]
        public void At80x24_FieldsStillFitBesideTheHelp()
        {
            Show();
            GoTo(EnumSetupStep.General);

            var screen = _harness.Screen();

            screen.Should().Contain(x => x.Contains("BBS Name: Emulated MajorBBS System"));
            screen.Should().Contain(x => x.Contains("Run Login Routines (Global): ◉ Yes"));
        }

        [Fact]
        public void LargeTerminal_WizardSizeIsCapped()
        {
            Show(200, 60);

            _view.Wizard.Frame.Width.Should().Be(SetupWizardView.MaximumWidth);
            _view.Wizard.Frame.Height.Should().Be(SetupWizardView.MaximumHeight);
        }

        [Fact]
        public void NextWithInvalidChannels_ShowsErrorAndStays()
        {
            Show();
            GoTo(EnumSetupStep.General);
            _view.ChannelsField.Text = "0";

            Next();

            _dialogs.Errors.Should().Equal(("BBS Settings", SetupWizardText.ValidChannelCount));
            _view.CurrentStep.Should().Be(EnumSetupStep.General);
        }

        [Fact]
        public void AddDuplicateKey_ShowsError()
        {
            Show();
            GoTo(EnumSetupStep.Security);
            _view.KeyField.Text = "DEMO";

            _view.AddKeyButton.InvokeCommand(Command.Accept);

            _dialogs.Errors.Should().Equal((SetupWizardText.ErrorTitle, SetupWizardText.KeyExists));
            _model.Keys.Should().Equal("DEMO", "NORMAL", "USER");
        }

        /// <summary>
        ///     The input box only adds keys; deleting works on the list's selection, so picking a key in the list
        ///     must not put it in the input box
        /// </summary>
        [Fact]
        public void SelectingAKey_LeavesTheInputBoxAlone()
        {
            Show();
            GoTo(EnumSetupStep.Security);

            _view.KeysList.SelectedItem = 1;

            _view.KeyField.Text.Should().BeEmpty();
        }

        [Fact]
        public void DeleteButton_RemovesTheSelectedKey()
        {
            Show();
            GoTo(EnumSetupStep.Security);
            _view.KeyField.Text = "SYSOP";
            _view.KeysList.SelectedItem = 1;

            _view.DeleteKeyButton.InvokeCommand(Command.Accept);

            _model.Keys.Should().Equal("DEMO", "USER");
            _view.KeyField.Text.Should().Be("SYSOP", "deleting a key doesn't touch what's being typed");
        }

        [Fact]
        public void DeleteButton_WithNothingSelected_ShowsError()
        {
            Show();
            GoTo(EnumSetupStep.Security);
            _view.KeysList.SelectedItem = null;

            _view.DeleteKeyButton.InvokeCommand(Command.Accept);

            _dialogs.Errors.Should().Equal((SetupWizardText.ErrorTitle, SetupWizardText.NoKeySelected));
            _model.Keys.Should().Equal("DEMO", "NORMAL", "USER");
        }

        [Fact]
        public void DeleteButton_SitsBesideTheListNotTheInputBox()
        {
            Show();
            GoTo(EnumSetupStep.Security);
            _harness.Screen();

            _view.DeleteKeyButton.Frame.Y.Should().Be(_view.KeysList.Frame.Y);
            _view.DeleteKeyButton.Frame.X.Should().BeGreaterThan(_view.KeysList.Frame.Right - 1);
        }

        [Fact]
        public void ReviewStep_ShowsCurrentValues()
        {
            Show();
            GoTo(EnumSetupStep.General);
            _view.BbsNameField.Text = "Edited";

            GoTo(EnumSetupStep.Review);

            _harness.Screen().Should().Contain(x => x.Contains("\"BBS.Title\": \"Edited\""));
        }

        [Fact]
        public void FinishDeclined_StaysOnReview()
        {
            Show();
            GoTo(EnumSetupStep.Review);
            _dialogs.ConfirmAnswers.Enqueue(false);

            Next();

            _writes.Should().BeEmpty();
            _view.CurrentStep.Should().Be(EnumSetupStep.Review);
            _view.Outcome.Should().BeNull();
        }

        [Fact]
        public void FinishSaved_WritesJsonAndReports()
        {
            Show();
            GoTo(EnumSetupStep.Review);

            Next();

            _writes.Should().Equal((SettingsPath, _model.ToJson()));
            _dialogs.Saved.Should().Equal(SettingsPath);
            _view.Outcome.Should().Be(new SetupWizardResult(true, SettingsPath));
        }

        /// <summary>
        ///     A read-only or protected folder must not throw away everything the sysop entered
        /// </summary>
        [Fact]
        public void SaveFails_ShowsErrorAndStaysOnReview()
        {
            Show();
            GoTo(EnumSetupStep.Review);
            _writeFailure = new UnauthorizedAccessException("Access to the path is denied.");

            Next();

            _dialogs.Errors.Should().Equal((SetupWizardText.ErrorTitle, $"Unable to write {SettingsPath}:\nAccess to the path is denied."));
            _dialogs.Saved.Should().BeEmpty();
            _view.CurrentStep.Should().Be(EnumSetupStep.Review);
            _view.Outcome.Should().BeNull();
        }

        [Fact]
        public void Esc_CancelsWizard()
        {
            Show();

            _harness.Key(Key.Esc);

            _view.Outcome.Should().Be(new SetupWizardResult(false, null));
        }
    }
}
