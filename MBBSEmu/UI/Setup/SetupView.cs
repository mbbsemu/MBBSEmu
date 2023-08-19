using System;
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

            var unicodeCheckBox = new CheckBox("Run Login Routines (Global)")
            {
                X = 1,
                Y = Pos.Bottom(lbl) + 1,
                Checked = true
            };
            secondStep.Add(unicodeCheckBox);

            wizard.AddStep(secondStep);

            base.MainWindow.Add(wizard);
        }

    }
}
