using System;
using NStack;
using Terminal.Gui;
using Terminal.Gui.TextValidateProviders;

namespace MBBSEmu.UI.Setup
{
    [UIMetadata(name: "Setup Wizard", description: "MBBSEmu Setup")]
    public class SetupView : UIBase
    {
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

            wizard.MovingBack += (args) => {
                //args.Cancel = true;
            };

            wizard.MovingNext += (args) => {
                //args.Cancel = true;
            };

            wizard.Finished += (args) => {
                //args.Cancel = true;
            };

            wizard.Cancelled += (args) => {
                //args.Cancel = true;
            };

            //Initial Setep (Welcome Message)
            var firstStep = new Wizard.WizardStep("Welcome to MBBSEmu!");
            firstStep.HelpText = "Welcome to The MajorBBS Emulation Project!\n\n" + 
                                 "MBBSEmu was created to allow quick, easy access to the Games we all love and remember that were written for The Major BBS 6.25 and Worldgroup for DOS!\n\n" +
                                 "Our project is Open Source, Free, and Community Supported!\n\n" + 
                                 "This Setup Wizard will guide you through the initial setup of MBBSEmu and the base settings. Click or Select Next below to start!";
            wizard.AddStep(firstStep);

            //Next Step - General Settings
            var secondStep = new Wizard.WizardStep("General Settings");
            secondStep.HelpText = "General Settings for MBBSEmu";

            var lbl = new Label() { Text = "BBS Name: ", X = 1, Y = 1 };
            var bbsNameField = new TextField() { Text = "Emulated MajorBBS System", Width = 30, X = Pos.Right(lbl), Y = Pos.Top(lbl) };
            bbsNameField.Enter += _ =>
            {
                secondStep.HelpText =
                    "BBS Name:\n\nThe Name of your BBS or the Name you want to give your MBBSEmu setup. Some Modules require this field or display it to the end user.";
            };
            secondStep.Add(lbl, bbsNameField);

            lbl = new Label() { Text = "Channels: ", X = 1, Y = Pos.Bottom(lbl) + 1 };
            var channelsMask = new NetMaskedTextProvider("999");
            var bbsChannelsField = new TextValidateField(channelsMask) { Text = "8", Width = 3, X = Pos.Right(lbl), Y = Pos.Top(lbl) };
            bbsChannelsField.Enter += _ =>
            {
                secondStep.HelpText =
                    "Channels:\n\nThe Number of Lines/Simultaneous Users your MBBSEmu instance will support. Depending on the modules you decide to run, a higher number of channels could result in slow performance.\n\n" +
                    "We recommend you set this number to a reasonable, realistic number based on the number of users you expect to be using MBBSEmu at the same time.";
            };
            secondStep.Add(lbl, bbsChannelsField);

            lbl = new Label() { Text = "Registration Number: ", X = 1, Y = Pos.Bottom(lbl) + 1 };
            var bturnoMaskk = new NetMaskedTextProvider("99999999");
            var bbsRegistrationNumberField = new TextValidateField(bturnoMaskk) { Text = "12345678", Width = 8, X = Pos.Right(lbl), Y = Pos.Top(lbl) };
            bbsRegistrationNumberField.Enter += _ =>
            {
                secondStep.HelpText =
                    "Registration Number:\n\nA unique, eight digit numeric value that was assigned to every MajorBBS / Worldgroup Sysop, derived from their original activation number.\n\n" +
                    "You can set this to your own Registration Number or just a random eight digit value.\n\n" + 
                    "If you have purchased or activated MBBS/WG modules in the past, set this value to the Registration Number you used to Activate those modules so you can Activate them in MBBSEmu as well.";
            };
            secondStep.Add(lbl, bbsRegistrationNumberField);

            lbl = new Label() { Text = "Cleanup Time: ", X = 1, Y = Pos.Bottom(lbl) + 1 };
            var cleanupTimeField = new TimeField(new TimeSpan(0,3,0,0))
            {
                X = Pos.Right(lbl),
                Y = Pos.Top(lbl),
                IsShortFormat = true,
                ReadOnly = false,
                TextAlignment = TextAlignment.Left
            };
            cleanupTimeField.Enter += _ =>
            {
                secondStep.HelpText =
                    "Cleanup Time:\n\nThe Time of Day (24-hour) that MBBSEmu will emulate the MajorBBS/Worldgroup Nightly Cleanup and invoke the cleanup routines within the running modules.";
            };
            secondStep.Add(lbl, cleanupTimeField);

            lbl = new Label() { Text = "Run Login Routines (Global): ", X = 1, Y = Pos.Bottom(lbl) + 1 };
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
            var thirdStep = new Wizard.WizardStep("Telnet Settings");
            thirdStep.HelpText = "Telnet Settings for MBBSEmu";

            lbl = new Label() { Text = "Telnet Server: ", X = 1, Y = 1 };
            var telnetEnabledRadio = new RadioGroup(new ustring[] { "Enabled", "Disabled" })
            {
                X = Pos.Right(lbl),
                Y = Pos.Top(lbl),
                SelectedItem = 0
            };
            telnetEnabledRadio.Enter += _ =>
            {
                thirdStep.HelpText =
                    "Telnet Enabled\n\nEnabling this option enables the MBBSEmu Telnet Daemon, allowing users to connect using their preferred Telnet client.";
            };
            thirdStep.Add(lbl, telnetEnabledRadio);

            lbl = new Label { Text = "Telnet Port: ", X = 1, Y = Pos.Bottom(lbl) + 2 };
            var telnetPortMask = new NetMaskedTextProvider("99999");
            var telnetPortField = new TextValidateField(telnetPortMask) { Text = "21", Width = 5, X = Pos.Right(lbl), Y = Pos.Top(lbl) };
            telnetPortField.Enter += _ =>
            {
                thirdStep.HelpText =
                    "Telnet Port\n\nPort Number that MBBSEmu will listen on for Telnet Connections.\n\n" + 
                    "On Linux, port numbers below 1024 will require you run MBBSEmu with elevated privileges.";
            };
            thirdStep.Add(lbl, telnetPortField);

            lbl = new Label() { Text = "Telnet Heartbeat: ", X = 1, Y = Pos.Bottom(lbl) + 1 };
            var telnetEnableHeartbeat = new RadioGroup(new ustring[] { "On", "Off" })
            {
                X = Pos.Right(lbl),
                Y = Pos.Top(lbl),
                SelectedItem = 1
            };
            telnetEnableHeartbeat.Enter += _ =>
            {
                thirdStep.HelpText =
                    "Telnet Heartbeat\n\nThis option is to help users whose connection between their Telnet Client and MBBSEmu will be terminated by intermediary network gear (firewalls, etc.) which will terminate a connection due to \"Inactivity\".\n\n" +
                    "Unless you're experiencing disconnects while idle on MBBSEmu, we recommend you leave this option OFF";
            };
            thirdStep.Add(lbl, telnetEnableHeartbeat);

            wizard.AddStep(thirdStep);


            //Next Step - Rlogin Settings
            var fourthStep = new Wizard.WizardStep("Rlogin Settings");
            fourthStep.HelpText = "Rlogin Settings for MBBSEmu";

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
                    "Rlogin Enabled\n\nEnabling this option enables the MBBSEmu Rlogin Daemon, allowing users to connect using Rlogin from another BBS Software such as Mystic or Synchronet.";
            };
            fourthStep.Add(lbl, rloginEnabledRadio);

            lbl = new Label { Text = "Rlogin Port: ", X = 1, Y = Pos.Bottom(lbl) + 2 };
            var rloginPortMask = new NetMaskedTextProvider("99999");
            var rloginPortField = new TextValidateField(rloginPortMask) { Text = "513", Width = 5, X = Pos.Right(lbl), Y = Pos.Top(lbl) };
            rloginPortField.Enter += _ =>
            {
                fourthStep.HelpText =
                    "Rlogin Port\n\nPort Number that MBBSEmu will listen on for Rlogin Connections.\n\n" +
                    "On Linux, Port Numbers below 1024 will require you run MBBSEmu with elevated privileges.";
            };
            fourthStep.Add(lbl, rloginPortField);

            lbl = new Label { Text = "Rlogin Remote IP: ", X = 1, Y = Pos.Bottom(lbl) + 1 };
            var rloginRemoteIPField = new TextField() { Text = "127.0.0.1", Width = 15, X = Pos.Right(lbl), Y = Pos.Top(lbl) };
            rloginRemoteIPField.Enter += _ =>
            {
                fourthStep.HelpText =
                    "Rlogin Remote IP\n\nIP Address of Remote System that is allowed to connect via Rlogin.\n\nRlogin is an old, insecure protocol and this is your only line of security. If you're using Rlogin, please ensure this field is set properly.";
            };
            fourthStep.Add(lbl, rloginRemoteIPField);

            lbl = new Label() { Text = "Port Per Module: ", X = 1, Y = Pos.Bottom(lbl) + 1 };
            var rloginPortPerModuleField = new RadioGroup(new ustring[] { "Yes", "No" })
            {
                X = Pos.Right(lbl),
                Y = Pos.Top(lbl),
                SelectedItem = 0
            };
            rloginPortPerModuleField.Enter += _ =>
            {
                fourthStep.HelpText =
                    "Port Per Field\n\nThis setting gives each Module it's own Rlogin port, starting with the specified Rlogin Port. This allows you to setup Rlogin from your remote system to have users land directly into the MBBS/WG Module without having to login or use the MBBSEmu Menu." + 
                    "For example, if you have three Modules and your specified Rlogin Port is 513 this means the following ports will be configured:\nModule 1: Port 513\nModule 2: Port 514\nModule 3: Port 515";
            };
            fourthStep.Add(lbl, rloginPortPerModuleField);
            wizard.AddStep(fourthStep);

            MainWindow.Add(wizard);
        }
    }
}
