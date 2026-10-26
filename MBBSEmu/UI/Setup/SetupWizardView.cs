using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using Attribute = Terminal.Gui.Drawing.Attribute;

namespace MBBSEmu.UI.Setup
{
    /// <summary>
    ///     The Setup Wizard: seven steps inside the "MBBSEmu Initial Setup" window, writing appsettings.json from
    ///     whatever the sysop entered
    /// </summary>
    public sealed class SetupWizardView : Window
    {
        private static readonly Color Blue = ConsoleTheme.Cga.Blue;

        /// <summary>
        ///     The wizard dialog: bright cyan frame and buttons, with its title (and the focused button) as a gray tab
        /// </summary>
        private static readonly Scheme WizardScheme = new(ConsoleTheme.On(ConsoleTheme.Cga.BrightCyan, Blue))
        {
            Focus = ConsoleTheme.Tab,
            Active = ConsoleTheme.Tab,
            HotNormal = ConsoleTheme.On(ConsoleTheme.Cga.Yellow, Blue),
            HotFocus = ConsoleTheme.On(ConsoleTheme.Cga.Red, ConsoleTheme.Cga.LightGray)
        };

        /// <summary>
        ///     Wizard steps: white labels on blue, with the focused control in black on cyan
        /// </summary>
        private static readonly Scheme StepScheme = new(ConsoleTheme.On(ConsoleTheme.Cga.White, Blue))
        {
            Focus = ConsoleTheme.Selection,
            Active = ConsoleTheme.Selection,
            HotNormal = ConsoleTheme.On(ConsoleTheme.Cga.BrightCyan, Blue),
            HotFocus = ConsoleTheme.Selection,
            Editable = ConsoleTheme.On(ConsoleTheme.Cga.White, Blue),
            Disabled = ConsoleTheme.On(ConsoleTheme.Cga.DarkGray, Blue)
        };

        /// <summary>
        ///     The Welcome and Review text: white on blue in every role, since Terminal.Gui otherwise derives a dim
        ///     "read-only" color that is hard to read
        /// </summary>
        private static readonly Scheme TextScheme = ConsoleTheme.Uniform(ConsoleTheme.On(ConsoleTheme.Cga.White, Blue));

        /// <summary>
        ///     Help text: DOS light gray on blue, quieter than the white labels beside it
        /// </summary>
        internal static readonly Attribute HelpColors = ConsoleTheme.On(ConsoleTheme.Cga.LightGray, Blue);

        private static readonly Scheme HelpScheme = ConsoleTheme.Uniform(HelpColors);

        private static readonly Scheme SeparatorScheme = ConsoleTheme.Uniform(ConsoleTheme.On(ConsoleTheme.Cga.BrightCyan, Blue));

        /// <summary>
        ///     Largest wizard size: bigger terminals get a centered wizard rather than a huge, mostly empty one
        /// </summary>
        public const int MaximumWidth = 110;
        public const int MaximumHeight = 30;

        /// <summary>
        ///     Columns kept for labels and fields to the left of the help panel
        /// </summary>
        private const int FieldsWidth = 46;

        private const int MinimumHelpWidth = 22;

        /// <summary>
        ///     Input fields: a cyan box, bright while focused; greyed out (no box) while disabled
        /// </summary>
        private static readonly Scheme FieldScheme = new(ConsoleTheme.On(ConsoleTheme.Cga.Black, ConsoleTheme.Cga.Cyan))
        {
            Focus = ConsoleTheme.On(ConsoleTheme.Cga.Black, ConsoleTheme.Cga.BrightCyan),
            Editable = ConsoleTheme.On(ConsoleTheme.Cga.Black, ConsoleTheme.Cga.Cyan),
            ReadOnly = ConsoleTheme.On(ConsoleTheme.Cga.DarkGray, Blue),
            Disabled = ConsoleTheme.On(ConsoleTheme.Cga.DarkGray, Blue)
        };

        private readonly SetupWizardModel _model;
        private readonly ISetupWizardDialogs _dialogs;
        private readonly Action<string, string> _writeFile;

        private readonly Dictionary<WizardStep, EnumSetupStep> _stepIds = new();
        private readonly Dictionary<EnumSetupStep, View> _helpPanels = new();
        private readonly List<View> _helpSeparators = new();
        private readonly Dictionary<View, Label> _labels = new();
        private readonly TooSmallView _tooSmall;
        private readonly ObservableCollection<string> _keyItems = new();
        private WizardStep _reviewStep;
        private View _reviewText;

        internal Wizard Wizard { get; }
        internal TextField BbsNameField { get; private set; }
        internal TextField ChannelsField { get; private set; }
        internal TextField TelnetPortField { get; private set; }
        internal OptionSelector TelnetEnabledSelector { get; private set; }
        internal OptionSelector TelnetHeartbeatSelector { get; private set; }
        internal OptionSelector RloginEnabledSelector { get; private set; }
        internal TextField RloginPortField { get; private set; }
        internal TextField RloginRemoteIPField { get; private set; }
        internal OptionSelector RloginPortPerModuleSelector { get; private set; }
        internal TextField KeyField { get; private set; }
        internal Button AddKeyButton { get; private set; }

        /// <summary>
        ///     The step's help panel, which shows the focused field's help
        /// </summary>
        internal View HelpPanel(EnumSetupStep step) => _helpPanels[step];

        internal EnumSetupStep CurrentStep => Wizard.CurrentStep != null && _stepIds.TryGetValue(Wizard.CurrentStep, out var step)
            ? step
            : EnumSetupStep.Welcome;

        /// <summary>
        ///     Set once the wizard ends: saved, or cancelled
        /// </summary>
        public SetupWizardResult Outcome { get; private set; }

        /// <param name="model">Values the wizard edits</param>
        /// <param name="dialogs">Error, confirm and save dialogs</param>
        /// <param name="writeFile">Writes (path, contents) when the sysop saves</param>
        public SetupWizardView(SetupWizardModel model, ISetupWizardDialogs dialogs, Action<string, string> writeFile)
        {
            _model = model;
            _dialogs = dialogs;
            _writeFile = writeFile;

            Title = SetupWizardText.OuterTitle;
            BorderStyle = LineStyle.Double;
            SetScheme(ConsoleTheme.Frame);

            Wizard = new Wizard
            {
                Title = SetupWizardText.WizardTitle,
                X = Pos.Center(),
                Y = Pos.Center()
            };
            Wizard.SetScheme(WizardScheme);

            AddWelcomeStep();
            AddGeneralStep();
            AddTelnetStep();
            AddRloginStep();
            AddAdvancedStep();
            AddSecurityStep();
            AddReviewStep();

            Wizard.MovingNext += (_, e) =>
            {
                var step = CurrentStep;
                var errors = _model.Validate(step);
                if (errors.Count == 0)
                    return;

                e.Cancel = true;
                _dialogs.ShowError(SetupWizardModel.ValidationTitle(step), string.Join("\n", errors));
            };

            Wizard.StepChanged += (_, e) =>
            {
                if (e.NewValue == _reviewStep)
                    _reviewText.Text = SetupWizardText.ReviewIntro + _model.ToJson();
            };

            //Finish on the last step; Accept elsewhere (e.g. Enter in a field) is the wizard's own Next
            Wizard.Accepting += (_, e) =>
            {
                if (Wizard.CurrentStep != _reviewStep)
                    return;

                e.Handled = true;
                Finish();
            };

            KeyDown += (_, key) =>
            {
                if (key.KeyCode != Key.Esc.KeyCode)
                    return;

                key.Handled = true;
                End(new SetupWizardResult(false, null));
            };

            _tooSmall = new TooSmallView(SetupWizardText.TooSmallQuitHint);
            Add(Wizard, _tooSmall);
        }

        /// <summary>
        ///     Shows the wizard, or the "too small" notice below 80×24
        /// </summary>
        public void ApplyScreenSize(int width, int height)
        {
            var tooSmall = ScreenSize.IsBelowMinimum(width, height);
            Wizard.Visible = !tooSmall;

            if (tooSmall)
            {
                _tooSmall.Show(Math.Max(0, width - 2), Math.Max(0, height - 2));
                SetNeedsLayout();
                return;
            }

            _tooSmall.Hide();

            //90% x 80% of the window as before, up to the maximum size
            var wizardWidth = Math.Min(MaximumWidth, (width - 2) * 9 / 10);
            var wizardHeight = Math.Min(MaximumHeight, (height - 2) * 8 / 10);
            Wizard.Width = wizardWidth;
            Wizard.Height = wizardHeight;

            //Help gets what the fields don't need, up to half the step
            var stepWidth = wizardWidth - 2;
            var helpWidth = Math.Clamp(stepWidth - FieldsWidth, MinimumHelpWidth, Math.Max(MinimumHelpWidth, stepWidth / 2));
            foreach (var separator in _helpSeparators)
                separator.X = stepWidth - helpWidth - 1;

            //One column of space after the separator
            foreach (var panel in _helpPanels.Values)
            {
                panel.X = stepWidth - helpWidth + 1;
                panel.Width = helpWidth - 1;
            }

            SetNeedsLayout();
        }

        private void Finish()
        {
            if (!_dialogs.ConfirmWrite())
                return;

            var path = _dialogs.AskSavePath(Directory.GetCurrentDirectory(), SetupWizardText.DefaultFileName);
            if (path == null)
            {
                End(new SetupWizardResult(false, null));
                return;
            }

            try
            {
                _writeFile(path, _model.ToJson());
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                //Keep everything entered so the sysop can pick another location
                _dialogs.ShowError(SetupWizardText.ErrorTitle, SetupWizardText.SaveFailedMessage(path, e.Message));
                return;
            }

            _dialogs.ShowSaved(path);
            End(new SetupWizardResult(true, path));
        }

        private void End(SetupWizardResult result)
        {
            Outcome = result;
            App?.RequestStop(this);
        }

        /// <summary>
        ///     Adds a step with its own help panel on the right (Terminal.Gui's built-in help pane is a fixed 25
        ///     columns), showing helpText until a field takes focus. No helpText means no panel.
        /// </summary>
        private WizardStep AddStep(EnumSetupStep id, string title, string helpText)
        {
            var step = new WizardStep { Title = title };
            step.SetScheme(StepScheme);
            _stepIds[step] = id;

            if (!string.IsNullOrEmpty(helpText))
            {
                var separator = new Line { Orientation = Orientation.Vertical, Y = 0, Height = Dim.Fill() };
                separator.SetScheme(SeparatorScheme);
                var panel = CreateText(helpText, HelpScheme);
                panel.Y = 0;
                panel.Height = Dim.Fill();

                _helpSeparators.Add(separator);
                _helpPanels[id] = panel;
                step.Add(separator, panel);
            }

            Wizard.AddStep(step);
            return step;
        }

        /// <summary>
        ///     The Welcome step has no fields, so its text fills the step
        /// </summary>
        private void AddWelcomeStep()
        {
            var step = AddStep(EnumSetupStep.Welcome, SetupWizardText.WelcomeTitle, string.Empty);

            var welcome = CreateText(SetupWizardText.WelcomeHelp, TextScheme);
            welcome.X = 1;
            welcome.Y = 1;
            welcome.Width = Dim.Fill(1);
            welcome.Height = Dim.Fill();
            step.Add(welcome);
        }

        /// <summary>
        ///     Read-only, word-wrapped text in the step's colors that never takes focus
        /// </summary>
        private static View CreateText(string text, Scheme scheme)
        {
            //TextView is marked obsolete in favor of a separate editor package, but it is still the built-in
            //read-only, word-wrapping text view
#pragma warning disable CS0618
            var view = new TextView { Text = text, ReadOnly = true, WordWrap = true, CanFocus = false };
#pragma warning restore CS0618
            view.SetScheme(scheme);
            return view;
        }

        private void AddGeneralStep()
        {
            var step = AddStep(EnumSetupStep.General, SetupWizardText.GeneralTitle, SetupWizardText.GeneralHelp);
            View previous = null;

            BbsNameField = AddTextField(step, ref previous, SetupWizardText.BbsNameLabel, SetupWizardText.BbsNameHelp, 30,
                _model.BbsName, x => _model.BbsName = x);
            ChannelsField = AddTextField(step, ref previous, SetupWizardText.ChannelsLabel, SetupWizardText.ChannelsHelp, 4,
                _model.Channels, x => _model.Channels = x, digitsOnly: true);
            AddTextField(step, ref previous, SetupWizardText.RegistrationNumberLabel, SetupWizardText.RegistrationNumberHelp, 8,
                _model.RegistrationNumber, x => _model.RegistrationNumber = x, digitsOnly: true);

            //24-hour HH:mm regardless of the machine's culture
            var timeFormat = (DateTimeFormatInfo)CultureInfo.InvariantCulture.DateTimeFormat.Clone();
            timeFormat.LongTimePattern = "HH:mm";
            var cleanupTime = new TimeEditor
            {
                Format = timeFormat,
                Value = _model.CleanupTime
            };
            cleanupTime.SetScheme(FieldScheme);
            cleanupTime.ValueChanged += (_, e) => _model.CleanupTime = e.NewValue;
            AddControl(step, ref previous, SetupWizardText.CleanupTimeLabel, SetupWizardText.CleanupTimeHelp, cleanupTime);

            AddOptions(step, ref previous, SetupWizardText.LoginRoutinesLabel, SetupWizardText.LoginRoutinesHelp,
                SetupWizardText.Yes, SetupWizardText.No, _model.DoLoginRoutine, x => _model.DoLoginRoutine = x);
        }

        private void AddTelnetStep()
        {
            var step = AddStep(EnumSetupStep.Telnet, SetupWizardText.TelnetTitle, SetupWizardText.TelnetHelp);
            View previous = null;

            TelnetEnabledSelector = AddOptions(step, ref previous, SetupWizardText.TelnetServerLabel, SetupWizardText.TelnetEnabledHelp,
                SetupWizardText.Enabled, SetupWizardText.Disabled, _model.TelnetEnabled, x => _model.TelnetEnabled = x);
            TelnetPortField = AddTextField(step, ref previous, SetupWizardText.TelnetPortLabel, SetupWizardText.TelnetPortHelp, 5,
                _model.TelnetPort, x => _model.TelnetPort = x, digitsOnly: true);
            TelnetHeartbeatSelector = AddOptions(step, ref previous, SetupWizardText.TelnetHeartbeatLabel, SetupWizardText.TelnetHeartbeatHelp,
                SetupWizardText.On, SetupWizardText.Off, _model.TelnetHeartbeat, x => _model.TelnetHeartbeat = x);

            EnableWhenServiceIs(TelnetEnabledSelector, () => _model.TelnetEnabled, TelnetPortField, TelnetHeartbeatSelector);
        }

        private void AddRloginStep()
        {
            var step = AddStep(EnumSetupStep.Rlogin, SetupWizardText.RloginTitle, SetupWizardText.RloginHelp);
            View previous = null;

            RloginEnabledSelector = AddOptions(step, ref previous, SetupWizardText.RloginServerLabel, SetupWizardText.RloginEnabledHelp,
                SetupWizardText.Enabled, SetupWizardText.Disabled, _model.RloginEnabled, x => _model.RloginEnabled = x);
            RloginPortField = AddTextField(step, ref previous, SetupWizardText.RloginPortLabel, SetupWizardText.RloginPortHelp, 6,
                _model.RloginPort, x => _model.RloginPort = x, digitsOnly: true);
            RloginRemoteIPField = AddTextField(step, ref previous, SetupWizardText.RloginRemoteIPLabel, SetupWizardText.RloginRemoteIPHelp, 15,
                _model.RloginRemoteIP, x => _model.RloginRemoteIP = x);
            RloginPortPerModuleSelector = AddOptions(step, ref previous, SetupWizardText.RloginPortPerModuleLabel, SetupWizardText.RloginPortPerModuleHelp,
                SetupWizardText.Yes, SetupWizardText.No, _model.RloginPortPerModule, x => _model.RloginPortPerModule = x);

            EnableWhenServiceIs(RloginEnabledSelector, () => _model.RloginEnabled, RloginPortField, RloginRemoteIPField,
                RloginPortPerModuleSelector);
        }

        /// <summary>
        ///     Greys out a service's settings (and their labels) while the service is Disabled: they don't apply, and
        ///     the model saves their defaults
        /// </summary>
        private void EnableWhenServiceIs(OptionSelector service, Func<bool> isEnabled, params View[] settings)
        {
            void Apply()
            {
                foreach (var setting in settings)
                {
                    setting.Enabled = isEnabled();
                    _labels[setting].Enabled = isEnabled();
                }
            }

            //Runs after the selector's own handler has updated the model
            service.ValueChanged += (_, _) => Apply();
            Apply();
        }

        private void AddAdvancedStep()
        {
            var step = AddStep(EnumSetupStep.Advanced, SetupWizardText.AdvancedTitle, SetupWizardText.AdvancedHelp);
            View previous = null;

            AddTextField(step, ref previous, SetupWizardText.DatabaseFilenameLabel, SetupWizardText.DatabaseFilenameHelp, 16,
                _model.DatabaseFilename, x => _model.DatabaseFilename = x);
            AddTextField(step, ref previous, SetupWizardText.BtrieveCacheSizeLabel, SetupWizardText.BtrieveCacheSizeHelp, 3,
                _model.BtrieveCacheSize, x => _model.BtrieveCacheSize = x, digitsOnly: true);
        }

        private void AddSecurityStep()
        {
            var step = AddStep(EnumSetupStep.Security, SetupWizardText.SecurityTitle, SetupWizardText.SecurityHelp);

            //Label, then the field with its +/- buttons, then the list with Defaults beside it: the help pane on the
            //right leaves about 40 columns, so nothing sits to the right of the label
            var label = new Label { Text = SetupWizardText.DefaultKeysLabel, X = 1, Y = 1 };
            KeyField = new TextField { X = 1, Y = Pos.Bottom(label) + 1, Width = 14 };
            KeyField.SetScheme(FieldScheme);
            AddKeyButton = new Button { Text = SetupWizardText.AddKeyButton, X = Pos.Right(KeyField) + 1, Y = Pos.Top(KeyField) };
            var removeKeyButton = new Button { Text = SetupWizardText.RemoveKeyButton, X = Pos.Right(AddKeyButton) + 1, Y = Pos.Top(KeyField) };

            var keyList = new ListView
            {
                X = 1,
                Y = Pos.Bottom(KeyField) + 2,
                Width = 14,
                Height = Dim.Fill(1)
            };
            keyList.SetSource(_keyItems);
            RefreshKeys();

            var defaultsButton = new Button { Text = SetupWizardText.DefaultsButton, X = Pos.Right(keyList) + 1, Y = Pos.Top(keyList) };

            keyList.ValueChanged += (_, e) =>
            {
                if (e.NewValue is { } index && index >= 0 && index < _model.Keys.Count)
                    KeyField.Text = _model.Keys[index];
            };

            AddKeyButton.Accepting += (_, e) =>
            {
                e.Handled = true;
                var error = _model.AddKey(KeyField.Text);
                if (error != null)
                {
                    _dialogs.ShowError(SetupWizardText.ErrorTitle, error);
                    return;
                }

                RefreshKeys();
                KeyField.Text = string.Empty;
            };

            removeKeyButton.Accepting += (_, e) =>
            {
                e.Handled = true;
                if (!_model.RemoveKey(keyList.SelectedItem ?? -1))
                {
                    _dialogs.ShowError(SetupWizardText.ErrorTitle, SetupWizardText.NoKeySelected);
                    return;
                }

                RefreshKeys();
                KeyField.Text = string.Empty;
            };

            defaultsButton.Accepting += (_, e) =>
            {
                e.Handled = true;
                _model.ResetKeys();
                RefreshKeys();
                KeyField.Text = string.Empty;
            };

            step.Add(label, KeyField, AddKeyButton, removeKeyButton, keyList, defaultsButton);
        }

        private void AddReviewStep()
        {
            _reviewStep = AddStep(EnumSetupStep.Review, SetupWizardText.ReviewTitle, string.Empty);

            //TextView is marked obsolete in favor of a separate editor package, but it is still the built-in
            //read-only, scrollable text view, which is all this step needs
#pragma warning disable CS0618
            _reviewText = new TextView
            {
                ReadOnly = true,
                Width = Dim.Fill(),
                Height = Dim.Fill()
            };
#pragma warning restore CS0618
            _reviewText.SetScheme(TextScheme);
            _reviewStep.Add(_reviewText);
        }

        private void RefreshKeys()
        {
            _keyItems.Clear();
            foreach (var key in _model.Keys)
                _keyItems.Add(key);
        }

        /// <summary>
        ///     Adds "label: [field]" on the next row of the step, bound to the model
        /// </summary>
        private TextField AddTextField(WizardStep step, ref View previous, string labelText, string help, int width, string value,
            Action<string> set, bool digitsOnly = false)
        {
            var field = new TextField { Width = width, Text = value };
            field.SetScheme(FieldScheme);

            //Digits-only fields refuse any change that would put a non-digit in them
            if (digitsOnly)
                field.ValueChanging += (_, e) =>
                {
                    if (!string.IsNullOrEmpty(e.NewValue) && !e.NewValue.All(char.IsAsciiDigit))
                        e.Handled = true;
                };

            field.ValueChanged += (_, e) => set(e.NewValue ?? string.Empty);

            AddControl(step, ref previous, labelText, help, field);
            return field;
        }

        /// <summary>
        ///     Adds "label: (•) first ( ) second" on the next rows of the step, bound to the model
        /// </summary>
        private OptionSelector AddOptions(WizardStep step, ref View previous, string labelText, string help, string first, string second,
            bool value, Action<bool> set)
        {
            var options = new OptionSelector
            {
                Labels = new[] { first, second },
                Value = value ? 0 : 1
            };
            options.ValueChanged += (_, e) => set(e.NewValue == 0);

            AddControl(step, ref previous, labelText, help, options);
            return options;
        }

        /// <summary>
        ///     Places a label and its control one row below the previous control, and shows the control's help
        ///     text in the step's help pane while it has focus
        /// </summary>
        private void AddControl(WizardStep step, ref View previous, string labelText, string help, View control)
        {
            var label = new Label { Text = labelText, X = 1, Y = previous == null ? 1 : Pos.Bottom(previous) + 1 };

            control.X = Pos.Right(label);
            control.Y = Pos.Top(label);
            control.HasFocusChanged += (_, e) =>
            {
                if (e.NewValue && _helpPanels.TryGetValue(_stepIds[step], out var panel))
                    panel.Text = help;
            };

            _labels[control] = label;
            step.Add(label, control);
            previous = control;
        }
    }
}
