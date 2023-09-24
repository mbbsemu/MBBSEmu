using NStack;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using Terminal.Gui;

namespace MBBSEmu.UI.Setup
{
    /// <summary>
    ///     View for the Initial Setup Wizard
    /// </summary>
    [UIMetadata(name: "Setup Wizard", description: "MBBSEmu Setup")]
    public class SetupView : UIBase
    {
        private readonly ColorScheme _inputFieldColorScheme = new()
        {
            Focus = new Terminal.Gui.Attribute(Color.BrightYellow, Color.Blue)
        };

        private readonly ColorScheme _wizardStepColorScheme = new()
        {
            Focus = new Terminal.Gui.Attribute(Color.BrightYellow, Color.Blue),
            HotFocus = new Terminal.Gui.Attribute(Color.BrightYellow, Color.Blue),
            Normal = new Terminal.Gui.Attribute(Color.Black, Color.Gray)
        };

        public SetupView() : base()
        {

        }

        public override void Setup()
        {

            var wizard = new Wizard("Initial Setup Wizard")
            {
                Width = Dim.Percent(90),
                Height = Dim.Percent(75)
            };

            //Initial Setep (Welcome Message)
            var firstStep = new Wizard.WizardStep("Welcome to MBBSEmu!")
            {
                HelpText = "Welcome to The MajorBBS Emulation Project!\n\n" +
                                 "MBBSEmu was created to allow quick, easy access to the Games we all love and remember that were written for The Major BBS 6.25 and Worldgroup for DOS!\n\n" +
                                 "Our project is Open Source, Free, and Community Supported!\n\n" +
                                 "This Setup Wizard will guide you through the initial setup of MBBSEmu and the base settings. Click or Select Next below to start!",
                ColorScheme = _wizardStepColorScheme
            };
            wizard.AddStep(firstStep);

            //Next Step - General Settings
            var secondStep = new Wizard.WizardStep("General Settings")
            {
                HelpText = "General Settings for MBBSEmu",
                ColorScheme = _wizardStepColorScheme
            };

            var lbl = new Label { Text = "BBS Name: ", X = 1, Y = 1 };
            var bbsNameField = new TextField { Text = "Emulated MajorBBS System", Width = 30, X = Pos.Right(lbl), Y = Pos.Top(lbl), ColorScheme = _inputFieldColorScheme };
            bbsNameField.Enter += _ =>
            {
                secondStep.HelpText =
                    "BBS Name:\n\nThe Name of your BBS or the Name you want to give your MBBSEmu setup. Some Modules require this field or display it to the end user.";
            };
            secondStep.Add(lbl, bbsNameField);

            lbl = new Label { Text = "Channels: ", X = 1, Y = Pos.Bottom(lbl) + 1 };
            var bbsChannelsField = new TextField { Text = "8", Width = 4, X = Pos.Right(lbl), Y = Pos.Top(lbl), ColorScheme = _inputFieldColorScheme };
            bbsChannelsField.Enter += _ =>
            {
                secondStep.HelpText =
                    "Channels:\n\nThe Number of Lines/Simultaneous Users your MBBSEmu instance will support. Depending on the modules you decide to run, a higher number of channels could result in slow performance.\n\n" +
                    "We recommend you set this number to a reasonable, realistic number based on the number of users you expect to be using MBBSEmu at the same time.";
            };
            secondStep.Add(lbl, bbsChannelsField);

            lbl = new Label { Text = "Registration Number: ", X = 1, Y = Pos.Bottom(lbl) + 1 };
            var bbsRegistrationNumberField = new TextField
            {
                Text = new Random().Next(10000000, 99999999).ToString(), 
                Width = 8, 
                X = Pos.Right(lbl),
                Y = Pos.Top(lbl), 
                ColorScheme = _inputFieldColorScheme
            };
            bbsRegistrationNumberField.Enter += _ =>
            {
                secondStep.HelpText =
                    "Registration Number:\n\nA unique, eight digit numeric value (including leading zeros) that was assigned to every MajorBBS / Worldgroup Sysop, derived from their original activation number.\n\n" +
                    "You can set this to your own Registration Number or the random default value.\n\n" +
                    "If you have purchased or activated MBBS/WG modules in the past, set this value to the Registration Number you used to Activate those modules so you can Activate them in MBBSEmu as well.";
            };
            secondStep.Add(lbl, bbsRegistrationNumberField);

            lbl = new Label { Text = "Cleanup Time: ", X = 1, Y = Pos.Bottom(lbl) + 1 };
            var cleanupTimeField = new TimeField(new TimeSpan(0, 3, 0, 0))
            {
                X = Pos.Right(lbl),
                Y = Pos.Top(lbl),
                IsShortFormat = true,
                ReadOnly = false,
                TextAlignment = TextAlignment.Left,
                ColorScheme = _inputFieldColorScheme
            };
            cleanupTimeField.Enter += _ =>
            {
                secondStep.HelpText =
                    "Cleanup Time:\n\nThe Time of Day (24-hour) that MBBSEmu will emulate the MajorBBS/Worldgroup Nightly Cleanup and invoke the cleanup routines within the running modules.";
            };
            secondStep.Add(lbl, cleanupTimeField);

            lbl = new Label { Text = "Run Login Routines (Global): ", X = 1, Y = Pos.Bottom(lbl) + 1 };
            var doLoginRoutineRadioGroup = new RadioGroup(new ustring[] { "Yes", "No" })
            {
                X = Pos.Right(lbl),
                Y = Pos.Top(lbl),
                SelectedItem = 0
            };
            doLoginRoutineRadioGroup.Enter += _ =>
            {
                secondStep.HelpText =
                    "Run Login Routines (Global):\n\nWhether or not to run the Login Routines in all modules when a user logs in.\n\n" +
                    "While disabling this can result in better performance, some modules run logic on a Users Channel during login that should not be disabled.\n\n" +
                    "We recommend you leave this option enabled with YES";
            };
            secondStep.Add(lbl, doLoginRoutineRadioGroup);

            wizard.AddStep(secondStep);

            //Next Step - Telnet Settings
            var thirdStep = new Wizard.WizardStep("Telnet Settings")
            {
                HelpText = "Telnet Settings for MBBSEmu",
                ColorScheme = _wizardStepColorScheme
            };

            lbl = new Label { Text = "Telnet Server: ", X = 1, Y = 1 };
            var telnetEnabledRadio = new RadioGroup(new ustring[] { "Enabled", "Disabled" })
            {
                X = Pos.Right(lbl),
                Y = Pos.Top(lbl),
                SelectedItem = 0
            };
            telnetEnabledRadio.Enter += _ =>
            {
                thirdStep.HelpText =
                    "Telnet Enabled:\n\nEnabling this option enables the MBBSEmu Telnet Daemon, allowing users to connect using their preferred Telnet client.";
            };
            thirdStep.Add(lbl, telnetEnabledRadio);

            lbl = new Label { Text = "Telnet Port: ", X = 1, Y = Pos.Bottom(lbl) + 2 };
            var telnetPortField = new TextField { Text = "21", Width = 5, X = Pos.Right(lbl), Y = Pos.Top(lbl), ColorScheme = _inputFieldColorScheme };
            telnetPortField.Enter += _ =>
            {
                thirdStep.HelpText =
                    "Telnet Port:\n\nPort Number that MBBSEmu will listen on for Telnet Connections.\n\n" +
                    "On Linux, port numbers below 1024 will require you run MBBSEmu with elevated privileges.";
            };
            thirdStep.Add(lbl, telnetPortField);

            lbl = new Label { Text = "Telnet Heartbeat: ", X = 1, Y = Pos.Bottom(lbl) + 1 };
            var telnetEnableHeartbeat = new RadioGroup(new ustring[] { "On", "Off" })
            {
                X = Pos.Right(lbl),
                Y = Pos.Top(lbl),
                SelectedItem = 1
            };
            telnetEnableHeartbeat.Enter += _ =>
            {
                thirdStep.HelpText =
                    "Telnet Heartbeat:\n\nThis option is to help users whose connection between their Telnet Client and MBBSEmu will be terminated by intermediary network gear (firewalls, etc.) which will terminate a connection due to \"Inactivity\".\n\n" +
                    "Unless you're experiencing disconnects while idle on MBBSEmu, we recommend you leave this option OFF";
            };
            thirdStep.Add(lbl, telnetEnableHeartbeat);

            wizard.AddStep(thirdStep);

            //Next Step - Rlogin Settings
            var fourthStep = new Wizard.WizardStep("Rlogin Settings")
            {
                HelpText = "Rlogin Settings for MBBSEmu",
                ColorScheme = _wizardStepColorScheme
            };

            lbl = new Label { Text = "Rlogin Server: ", X = 1, Y = 1 };
            var rloginEnabledRadio = new RadioGroup(new ustring[] { "Enabled", "Disabled" })
            {
                X = Pos.Right(lbl),
                Y = Pos.Top(lbl),
                SelectedItem = 1
            };
            rloginEnabledRadio.Enter += _ =>
            {
                fourthStep.HelpText =
                    "Rlogin Enabled:\n\nEnabling this option enables the MBBSEmu Rlogin Daemon, allowing users to connect using Rlogin from another BBS Software such as Mystic or Synchronet.";
            };
            fourthStep.Add(lbl, rloginEnabledRadio);

            lbl = new Label { Text = "Rlogin Port: ", X = 1, Y = Pos.Bottom(lbl) + 2 };
            var rloginPortField = new TextField { Text = "513", Width = 6, X = Pos.Right(lbl), Y = Pos.Top(lbl), ColorScheme = _inputFieldColorScheme };
            rloginPortField.Enter += _ =>
            {
                fourthStep.HelpText =
                    "Rlogin Port:\n\nPort Number that MBBSEmu will listen on for Rlogin Connections.\n\n" +
                    "On Linux, Port Numbers below 1024 will require you run MBBSEmu with elevated privileges.";
            };
            fourthStep.Add(lbl, rloginPortField);

            lbl = new Label { Text = "Rlogin Remote IP: ", X = 1, Y = Pos.Bottom(lbl) + 1 };
            var rloginRemoteIPField = new TextField { Text = "127.0.0.1", Width = 15, X = Pos.Right(lbl), Y = Pos.Top(lbl), ColorScheme = _inputFieldColorScheme };
            rloginRemoteIPField.Enter += _ =>
            {
                fourthStep.HelpText =
                    "Rlogin Remote IP:\n\nIP Address of Remote System that is allowed to connect via Rlogin.\n\nRlogin is an old, insecure protocol and this is your only line of security. If you're using Rlogin, please ensure this field is set properly.";
            };
            fourthStep.Add(lbl, rloginRemoteIPField);

            lbl = new Label { Text = "Port Per Module: ", X = 1, Y = Pos.Bottom(lbl) + 1 };
            var rloginPortPerModuleField = new RadioGroup(new ustring[] { "Yes", "No" })
            {
                X = Pos.Right(lbl),
                Y = Pos.Top(lbl),
                SelectedItem = 0
            };
            rloginPortPerModuleField.Enter += _ =>
            {
                fourthStep.HelpText =
                    "Port Per Module:\n\nThis setting gives each Module it's own Rlogin port, starting with the specified Rlogin Port. This allows you to setup Rlogin from your remote system to have users land directly into the MBBS/WG Module without having to login or use the MBBSEmu Menu." +
                    "For example, if you have three Modules and your specified Rlogin Port is 513 this means the following ports will be configured:\nModule 1: Port 513\nModule 2: Port 514\nModule 3: Port 515";
            };
            fourthStep.Add(lbl, rloginPortPerModuleField);
            wizard.AddStep(fourthStep);

            //Next Step - Advanced Settings
            var fifthStep = new Wizard.WizardStep("Advanced Settings")
            {
                HelpText = "Advanced Settings for MBBSEmu",
                ColorScheme = _wizardStepColorScheme
            };

            lbl = new Label { Text = "Database Filename: ", X = 1, Y = 1 };
            var databaseFilenameField = new TextField { Text = "mbbsemu.db", Width = 16, X = Pos.Right(lbl), Y = Pos.Top(lbl), ColorScheme = _inputFieldColorScheme };
            databaseFilenameField.Enter += _ =>
            {
                fifthStep.HelpText =
                    "Database Filename:\n\nSpecifies the filename to be used for the MBBSEmu internal database.\n\n" +
                    "This is a SQLite Database that contains user information (username, hashed passwords, etc.) used by the various services within MBBSEmu.";
            };
            fifthStep.Add(lbl, databaseFilenameField);

            lbl = new Label { Text = "Btrieve Cache Size: ", X = 1, Y = Pos.Bottom(lbl) + 2 };
            var btrieveCacheSizeField = new TextField { Text = "4", Width = 3, X = Pos.Right(lbl), Y = Pos.Top(lbl), ColorScheme = _inputFieldColorScheme };
            btrieveCacheSizeField.Enter += _ =>
            {
                fifthStep.HelpText =
                    "Btrieve Cache Size:\n\nSpecifies the number of records within an open Btrieve file to keep cached in memory.\n\n" +
                    "Btrieve DAT files are converted to SQLite at startup and each record is a binary struct. This value speeds up reads of records queried by the Btrieve API by Modules by keeping records in memory and not querying SQLite for each read.";
            };
            fifthStep.Add(lbl, btrieveCacheSizeField);
            wizard.AddStep(fifthStep);

            //Next Step - Security Settings
            var sixthStep = new Wizard.WizardStep("Security Settings")
            {
                HelpText = "Default User Keys\n\nUser Keys are used in MBBS/Worldgroup to give users specific permissions within modules.\n\nThese Keys will be the keys that every new user receives after creating an account within MBBSEmu. You can change a users keys within MBBSEmu as well using the /SYSOP command.",
                ColorScheme = _wizardStepColorScheme
            };

            lbl = new Label { Text = "Default User Keys: ", X = 1, Y = 1 };
            var keyToAddField = new TextField
            {
                Width = 16,
                X = Pos.Right(lbl),
                Y = Pos.Top(lbl),
                ColorScheme = _inputFieldColorScheme
            };
            var addKeyButton = new Button("+", true)
            {
                X = Pos.Right(keyToAddField) + 1,
                Y = Pos.Top(lbl)
            };
            var removeKeyButton = new Button("-")
            {
                X = Pos.Right(addKeyButton) + 1,
                Y = Pos.Top(lbl)
            };
            var defaultKeys = new List<ustring> { "DEMO", "NORMAL", "USER" };
            var keyListField = new ListView(defaultKeys)
            {
                X = Pos.Left(keyToAddField),
                Y = Pos.Bottom(lbl) + 1,
                Width = 20,
                Height = Dim.Fill() - 3,
                ColorScheme = new ColorScheme() { Normal = Application.Driver.MakeAttribute(Color.BrightYellow, Color.Blue) }
            };
            var defaultButton = new Button("Defaults")
            {
                X = Pos.Left(keyListField),
                Y = Pos.Bottom(keyListField)
            };

            defaultButton.Clicked += () =>
            {
                defaultKeys = new List<ustring> { "DEMO", "NORMAL", "USER" };
                keyListField.SetSource(defaultKeys);
                keyToAddField.Text = string.Empty;
                Application.Refresh();
            };

            keyListField.SelectedItemChanged += (args) =>
            {
                keyToAddField.Text = defaultKeys[keyListField.SelectedItem];
                removeKeyButton.SetFocus();
            };

            removeKeyButton.Clicked += () =>
            {
                if (keyListField.SelectedItem == -1)
                {
                    MessageBox.ErrorQuery(40, 10, "Error", "No Key Selected", "Ok");
                    return;
                }

                defaultKeys.RemoveAt(keyListField.SelectedItem);
                keyListField.SetSource(defaultKeys);
                keyToAddField.Text = string.Empty;
                Application.Refresh();
            };

            addKeyButton.Clicked += () =>
            {
                if (string.IsNullOrEmpty(keyToAddField.Text.ToString()))
                {
                    MessageBox.ErrorQuery(40, 10, "Error", "Key cannot be empty", "Ok");
                    return;
                }

                if (defaultKeys.Contains(keyToAddField.Text.ToString()))
                {
                    MessageBox.ErrorQuery(40, 10, "Error", "Key already exists", "Ok");
                    return;
                }

                defaultKeys.Add(keyToAddField.Text.ToString());
                keyListField.SetSource(defaultKeys);
                keyToAddField.Text = "";
                Application.Refresh();
            };
            sixthStep.Add(lbl, keyToAddField, addKeyButton, removeKeyButton, keyListField, defaultButton);
            wizard.AddStep(sixthStep);

            //Final Step -- Review JSON Data
            var finalStep = new Wizard.WizardStep("Review Settings") { ColorScheme = _wizardStepColorScheme };

            //Create a new AppSettings object to hold values collected in this Wizard
            var appSettings = new AppSettings()
            {
                BBSTitle = bbsNameField.Text.ToString(),
                BBSChannels = bbsChannelsField.Text.ToString(),
                CleanupTime = cleanupTimeField.Text.ToString()?.Trim(),
                RegistrationNumber = bbsRegistrationNumberField.Text.ToString(),
                TelnetEnabled = telnetEnabledRadio.SelectedItem == 0,
                TelnetPort = telnetPortField.Text.ToString(),
                TelnetHeartbeat = telnetEnableHeartbeat.SelectedItem == 0,
                RloginEnabled = rloginEnabledRadio.SelectedItem == 0,
                RloginPort = rloginPortField.Text.ToString(),
                RloginRemoteIP = rloginRemoteIPField.Text.ToString(),
                RloginPortPerModule = rloginPortPerModuleField.SelectedItem == 0,
                UserDatabaseFilename = databaseFilenameField.Text.ToString(),
                BtrieveCacheSize = int.Parse(btrieveCacheSizeField.Text.ToString()),
                AccountDefaultKeys = defaultKeys.Select(x => x.ToString()).ToArray()
            };

            finalStep.HelpText = $"The following settings will be written to appsettings.json:\n\n {JsonSerializer.Serialize(appSettings, new JsonSerializerOptions() { WriteIndented = true })}";
            wizard.AddStep(finalStep);

            //Generate JSON Object from Specified Fields
            wizard.StepChanging += (args) =>
            {
                if (args.OldStep == secondStep)
                {
                    if (string.IsNullOrEmpty(bbsNameField.Text.ToString()))
                    {
                        args.Cancel = true;
                        MessageBox.ErrorQuery("BBS Settings", "You must enter a BBS Name to continue", "Ok");
                    }

                    if (!int.TryParse(bbsChannelsField.Text.ToString(), out var channelCount) || channelCount < 1 || channelCount > 255)
                    {
                        args.Cancel = true;
                        MessageBox.ErrorQuery("BBS Settings", "You must enter a valid Channel Count between 1 and 255 to continue", "Ok");
                    }

                    if (!int.TryParse(bbsRegistrationNumberField.Text.ToString(), out var regNo))
                    {
                        args.Cancel = true;
                        MessageBox.ErrorQuery("BBS Settings", "You must enter a valid BBS Registration Number to continue", "Ok");
                    }

                    if (bbsRegistrationNumberField.Text.ToString()!.Length != 8)
                    {
                        args.Cancel = true;
                        MessageBox.ErrorQuery("BBS Settings", "You must enter a valid BBS Registration Number to continue", "Ok");
                    }

                }

                if (args.OldStep == thirdStep)
                {
                    if (telnetEnabledRadio.SelectedItem == 0 && string.IsNullOrEmpty(telnetPortField.Text.ToString()))
                    {
                        args.Cancel = true;
                        MessageBox.ErrorQuery("Telnet Settings", "You must enter a Telnet Port to continue", "Ok");
                    }

                    if (int.TryParse(telnetPortField.Text.ToString(), out var telnetPort) &&
                        telnetPort is < 1 or > 65535)
                    {
                        args.Cancel = true;
                        MessageBox.ErrorQuery("Telnet Settings", "You must enter a valid Telnet Port to continue", "Ok");
                    }
                }

                if (args.OldStep == fourthStep)
                {
                    if (rloginEnabledRadio.SelectedItem == 0 && string.IsNullOrEmpty(rloginPortField.Text.ToString()))
                    {
                        args.Cancel = true;
                        MessageBox.ErrorQuery("Rlogin Settings", "You must enter a Rlogin Port to continue", "Ok");
                    }

                    if (int.TryParse(rloginPortField.Text.ToString(), out var rloginPort) &&
                        rloginPort is < 1 or > 65535)
                    {
                        args.Cancel = true;
                        MessageBox.ErrorQuery("Rlogin Settings", "You must enter a valid Rlogin Port to continue", "Ok");
                    }

                    if (!IPAddress.TryParse(rloginRemoteIPField.Text.ToString(), out var rloginRemoteIP))
                    {
                        args.Cancel = true;
                        MessageBox.ErrorQuery("Rlogin Settings", "You must enter a valid Rlogin Remote IP to continue", "Ok");
                    }
                }

                if (args.OldStep == fifthStep)
                {
                    if (string.IsNullOrEmpty(databaseFilenameField.Text.ToString()))
                    {
                        args.Cancel = true;
                        MessageBox.ErrorQuery("Advanced Settings", "You must enter a Database Filename to continue", "Ok");
                    }

                    if (!int.TryParse(btrieveCacheSizeField.Text.ToString(), out var btrieveCacheSize) || btrieveCacheSize < 1)
                    {
                        args.Cancel = true;
                        MessageBox.ErrorQuery("Advanced Settings", "You must enter a valid Btrieve Cache Size to continue", "Ok");
                    }
                }

                if (args.OldStep == sixthStep)
                {
                    if (defaultKeys.Count == 0)
                    {
                        args.Cancel = true;
                        MessageBox.ErrorQuery("Account Settings", "You must enter at least one Account Default Key to continue", "Ok");
                    }
                }
            };

            wizard.Finished += (args) =>
            {
                //Confirm user wants to write settings to appsettings.json
                if (MessageBox.Query("Confirm", "Are you sure you want to write these settings to appsettings.json?",
                        "Yes", "No") == 0)
                {
                    //Write settings to appsettings.json
                    var appSettingsJson = JsonSerializer.Serialize(appSettings, new JsonSerializerOptions() { WriteIndented = true });
                    File.WriteAllText("appsettings.json", appSettingsJson);

                    //Close the Wizard
                    wizard.Running = false;
                    wizard.Visible = false;
                    this.RequestStop();

                    //Show a message box to confirm settings were written
                    MessageBox.Query("Success", "Settings were successfully written to appsettings.json", "Ok");
                }
            };

            MainWindow.Add(wizard);
        }
    }
}
