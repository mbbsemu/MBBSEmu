namespace MBBSEmu.UI.Setup
{
    /// <summary>
    ///     User-visible text for the Setup Wizard
    ///
    ///     Kept separate from the view so the wording survives UI rewrites unchanged
    /// </summary>
    public static class SetupWizardText
    {
        public const string OuterTitle = "MBBSEmu Initial Setup";
        public const string WizardTitle = "Initial Setup Wizard";

        //Welcome Step
        public const string WelcomeTitle = "Welcome to MBBSEmu!";
        public const string WelcomeHelp = "Welcome to The MajorBBS Emulation Project!\n\n" +
                                          "MBBSEmu was created to allow quick, easy access to the Games we all love and remember that were written for The Major BBS 6.25 and Worldgroup for DOS!\n\n" +
                                          "Our project is Open Source, Free, and Community Supported!\n\n" +
                                          "This Setup Wizard will guide you through the initial setup of MBBSEmu and the base settings. Click or Select Next below to start!";

        //General Settings Step
        public const string GeneralTitle = "General Settings";
        public const string GeneralHelp = "General Settings for MBBSEmu";

        public const string BbsNameLabel = "BBS Name: ";
        public const string BbsNameDefault = "Emulated MajorBBS System";
        public const string BbsNameHelp =
            "BBS Name:\n\nThe Name of your BBS or the Name you want to give your MBBSEmu setup. Some Modules require this field or display it to the end user.";

        public const string ChannelsLabel = "Channels: ";
        public const string ChannelsHelp =
            "Channels:\n\nThe Number of Lines/Simultaneous Users your MBBSEmu instance will support. Depending on the modules you decide to run, a higher number of channels could result in slow performance.\n\n" +
            "We recommend you set this number to a reasonable, realistic number based on the number of users you expect to be using MBBSEmu at the same time.";

        public const string RegistrationNumberLabel = "Registration Number: ";
        public const string RegistrationNumberHelp =
            "Registration Number:\n\nA unique, eight digit numeric value (including leading zeros) that was assigned to every MajorBBS / Worldgroup Sysop, derived from their original activation number.\n\n" +
            "You can set this to your own Registration Number or the random default value.\n\n" +
            "If you have purchased or activated MBBS/WG modules in the past, set this value to the Registration Number you used to Activate those modules so you can Activate them in MBBSEmu as well.";

        public const string CleanupTimeLabel = "Cleanup Time: ";
        public const string CleanupTimeHelp =
            "Cleanup Time:\n\nThe Time of Day (24-hour) that MBBSEmu will emulate the MajorBBS/Worldgroup Nightly Cleanup and invoke the cleanup routines within the running modules.";

        public const string LoginRoutinesLabel = "Run Login Routines (Global): ";
        public const string LoginRoutinesHelp =
            "Run Login Routines (Global):\n\nWhether or not to run the Login Routines in all modules when a user logs in.\n\n" +
            "While disabling this can result in better performance, some modules run logic on a Users Channel during login that should not be disabled.\n\n" +
            "We recommend you leave this option enabled with YES";

        //Option Labels
        public const string Yes = "Yes";
        public const string No = "No";
        public const string Enabled = "Enabled";
        public const string Disabled = "Disabled";
        public const string On = "On";
        public const string Off = "Off";

        //Telnet Settings Step
        public const string TelnetTitle = "Telnet Settings";
        public const string TelnetHelp = "Telnet Settings for MBBSEmu";

        public const string TelnetServerLabel = "Telnet Server: ";
        public const string TelnetEnabledHelp =
            "Telnet Enabled:\n\nEnabling this option enables the MBBSEmu Telnet Daemon, allowing users to connect using their preferred Telnet client.";

        public const string TelnetPortLabel = "Telnet Port: ";
        public const string TelnetPortHelp =
            "Telnet Port:\n\nPort Number that MBBSEmu will listen on for Telnet Connections.\n\n" +
            "On Linux, port numbers below 1024 will require you run MBBSEmu with elevated privileges.";

        public const string TelnetHeartbeatLabel = "Telnet Heartbeat: ";
        public const string TelnetHeartbeatHelp =
            "Telnet Heartbeat:\n\nThis option is to help users whose connection between their Telnet Client and MBBSEmu will be terminated by intermediary network gear (firewalls, etc.) which will terminate a connection due to \"Inactivity\".\n\n" +
            "Unless you're experiencing disconnects while idle on MBBSEmu, we recommend you leave this option OFF";

        //Rlogin Settings Step
        public const string RloginTitle = "Rlogin Settings";
        public const string RloginHelp = "Rlogin Settings for MBBSEmu";

        public const string RloginServerLabel = "Rlogin Server: ";
        public const string RloginEnabledHelp =
            "Rlogin Enabled:\n\nEnabling this option enables the MBBSEmu Rlogin Daemon, allowing users to connect using Rlogin from another BBS Software such as Mystic or Synchronet.";

        public const string RloginPortLabel = "Rlogin Port: ";
        public const string RloginPortHelp =
            "Rlogin Port:\n\nPort Number that MBBSEmu will listen on for Rlogin Connections.\n\n" +
            "On Linux, Port Numbers below 1024 will require you run MBBSEmu with elevated privileges.";

        public const string RloginRemoteIPLabel = "Rlogin Remote IP: ";
        public const string RloginRemoteIPHelp =
            "Rlogin Remote IP:\n\nIP Address of Remote System that is allowed to connect via Rlogin.\n\nRlogin is an old, insecure protocol and this is your only line of security. If you're using Rlogin, please ensure this field is set properly.";

        public const string RloginPortPerModuleLabel = "Port Per Module: ";
        public const string RloginPortPerModuleHelp =
            "Port Per Module:\n\nThis setting gives each Module it's own Rlogin port, starting with the specified Rlogin Port. This allows you to setup Rlogin from your remote system to have users land directly into the MBBS/WG Module without having to login or use the MBBSEmu Menu." +
            "For example, if you have three Modules and your specified Rlogin Port is 513 this means the following ports will be configured:\nModule 1: Port 513\nModule 2: Port 514\nModule 3: Port 515";

        //Advanced Settings Step
        public const string AdvancedTitle = "Advanced Settings";
        public const string AdvancedHelp = "Advanced Settings for MBBSEmu";

        public const string DatabaseFilenameLabel = "Database Filename: ";
        public const string DatabaseFilenameHelp =
            "Database Filename:\n\nSpecifies the filename to be used for the MBBSEmu internal database.\n\n" +
            "This is a SQLite Database that contains user information (username, hashed passwords, etc.) used by the various services within MBBSEmu.";

        public const string BtrieveCacheSizeLabel = "Btrieve Cache Size: ";
        public const string BtrieveCacheSizeHelp =
            "Btrieve Cache Size:\n\nSpecifies the number of records within an open Btrieve file to keep cached in memory.\n\n" +
            "Btrieve DAT files are converted to SQLite at startup and each record is a binary struct. This value speeds up reads of records queried by the Btrieve API by Modules by keeping records in memory and not querying SQLite for each read.";

        //Security Settings Step
        public const string SecurityTitle = "Security Settings";
        public const string SecurityHelp =
            "Default User Keys\n\nUser Keys are used in MBBS/Worldgroup to give users specific permissions within modules.\n\nThese Keys will be the keys that every new user receives after creating an account within MBBSEmu. You can change a users keys within MBBSEmu as well using the /SYSOP command.";

        public const string DefaultKeysLabel = "Default User Keys: ";
        public const string AddKeyButton = "+";
        public const string DeleteKeyButton = "Delete";
        public const string DefaultsButton = "Defaults";

        public const string ErrorTitle = "Error";
        public const string OkButton = "Ok";
        public const string NoKeySelected = "No Key Selected";
        public const string KeyEmpty = "Key cannot be empty";
        public const string KeyExists = "Key already exists";

        //Review Settings Step
        public const string ReviewTitle = "Review Settings";
        public const string ReviewIntro = "The following settings will be written to appsettings.json:\n\n ";

        //Validation Titles
        public const string GeneralValidationTitle = "BBS Settings";
        public const string TelnetValidationTitle = "Telnet Settings";
        public const string RloginValidationTitle = "Rlogin Settings";
        public const string AdvancedValidationTitle = "Advanced Settings";
        public const string SecurityValidationTitle = "Account Settings";

        //Validation Messages
        public const string BbsNameRequired = "You must enter a BBS Name to continue";
        public const string ValidChannelCount = "You must enter a valid Channel Count between 1 and 255 to continue";
        public const string ValidRegistrationNumber = "You must enter a valid BBS Registration Number to continue";
        public const string TelnetPortRequired = "You must enter a Telnet Port to continue";
        public const string ValidTelnetPort = "You must enter a valid Telnet Port to continue";
        public const string RloginPortRequired = "You must enter a Rlogin Port to continue";
        public const string ValidRloginPort = "You must enter a valid Rlogin Port to continue";
        public const string ValidRloginRemoteIP = "You must enter a valid Rlogin Remote IP to continue";
        public const string DatabaseFilenameRequired = "You must enter a Database Filename to continue";
        public const string ValidBtrieveCacheSize = "You must enter a valid Btrieve Cache Size to continue";
        public const string DefaultKeyRequired = "You must enter at least one Account Default Key to continue";

        //Finish
        public const string ConfirmTitle = "Confirm";
        public const string ConfirmMessage = "Are you sure you want to write these settings to appsettings.json?";

        public const string SuccessTitle = "Success";

        public const string CancelledMessage = "Setup cancelled";
        public const string TooSmallQuitHint = "Esc cancels setup.";
        public const string YesButton = "_Yes";
        public const string NoButton = "_No";

        public static string SuccessMessage(string path) => $"Settings were successfully written to:\n{path}";

        public static string SaveFailedMessage(string path, string reason) => $"Unable to write {path}:\n{reason}";
    }
}
