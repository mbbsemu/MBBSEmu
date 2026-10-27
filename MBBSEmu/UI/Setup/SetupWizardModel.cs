using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace MBBSEmu.UI.Setup
{
    public enum EnumSetupStep
    {
        Welcome,
        General,
        Telnet,
        Rlogin,
        Advanced,
        Security,
        Review
    }

    /// <summary>
    ///     Settings collected by the Setup Wizard
    ///
    ///     The wizard view edits these properties as the sysop types; the written appsettings.json is always built
    ///     from the current values, never from the values the wizard started with.
    /// </summary>
    public sealed class SetupWizardModel
    {
        public static readonly IReadOnlyList<string> DefaultKeys = new[] { "DEMO", "NORMAL", "USER" };

        //Service defaults, also what's saved for a service that is disabled
        public const string DefaultTelnetPort = "23";
        public const bool DefaultTelnetHeartbeat = false;
        public const string DefaultRloginPort = "513";
        public const string DefaultRloginRemoteIP = "127.0.0.1";
        public const bool DefaultRloginPortPerModule = true;

        private readonly List<string> _keys = new(DefaultKeys);

        /// <summary>
        ///     The appsettings.json keys the wizard edits. Only these are read from and written over an existing file;
        ///     every other setting in it is kept as written.
        /// </summary>
        private static readonly string[] EditedKeys =
        {
            "BBS.Title", "BBS.Channels", "GSBL.BTURNO", "Cleanup.Time", "Module.DoLoginRoutine", "Telnet.Enabled",
            "Telnet.Port", "Telnet.Heartbeat", "Rlogin.Enabled", "Rlogin.Port", "Rlogin.RemoteIP", "Rlogin.PortPerModule",
            "Database.File", "Btrieve.CacheSize", "Account.DefaultKeys"
        };

        /// <summary>
        ///     The settings file the wizard started from, or null for a new one
        /// </summary>
        private JsonObject _existing;

        public SetupWizardModel(Random random)
        {
            RegistrationNumber = random.Next(10000000, 99999999).ToString(CultureInfo.InvariantCulture);
        }

        public string BbsName { get; set; } = SetupWizardText.BbsNameDefault;
        public string Channels { get; set; } = "8";

        /// <summary>
        ///     Eight digits, kept as text so leading zeros survive
        /// </summary>
        public string RegistrationNumber { get; set; }

        public TimeSpan CleanupTime { get; set; } = new(3, 0, 0);
        public bool DoLoginRoutine { get; set; } = true;

        public bool TelnetEnabled { get; set; } = true;
        public string TelnetPort { get; set; } = DefaultTelnetPort;
        public bool TelnetHeartbeat { get; set; } = DefaultTelnetHeartbeat;

        public bool RloginEnabled { get; set; }
        public string RloginPort { get; set; } = DefaultRloginPort;
        public string RloginRemoteIP { get; set; } = DefaultRloginRemoteIP;
        public bool RloginPortPerModule { get; set; } = DefaultRloginPortPerModule;

        public string DatabaseFilename { get; set; } = "mbbsemu.db";
        public string BtrieveCacheSize { get; set; } = "4";

        /// <summary>
        ///     Default security keys given to new accounts
        /// </summary>
        public IReadOnlyList<string> Keys => _keys;

        /// <returns>Null if added, otherwise the reason it wasn't</returns>
        public string AddKey(string key)
        {
            if (string.IsNullOrEmpty(key))
                return SetupWizardText.KeyEmpty;

            if (_keys.Contains(key))
                return SetupWizardText.KeyExists;

            _keys.Add(key);
            return null;
        }

        /// <returns>False if index is not a key</returns>
        public bool RemoveKey(int index)
        {
            if (index < 0 || index >= _keys.Count)
                return false;

            _keys.RemoveAt(index);
            return true;
        }

        public void ResetKeys()
        {
            _keys.Clear();
            _keys.AddRange(DefaultKeys);
        }

        /// <summary>
        ///     Reasons the step's values can't be saved, or none
        /// </summary>
        public IReadOnlyList<string> Validate(EnumSetupStep step)
        {
            var errors = new List<string>();

            switch (step)
            {
                case EnumSetupStep.General:
                    if (string.IsNullOrEmpty(BbsName))
                        errors.Add(SetupWizardText.BbsNameRequired);

                    if (!TryParseNumber(Channels, out var channels) || channels < 1 || channels > 255)
                        errors.Add(SetupWizardText.ValidChannelCount);

                    if (RegistrationNumber is not { Length: 8 } || !IsAllDigits(RegistrationNumber))
                        errors.Add(SetupWizardText.ValidRegistrationNumber);
                    break;

                //A disabled service's settings are greyed out and saved as defaults, so they aren't checked
                case EnumSetupStep.Telnet when TelnetEnabled:
                    ValidatePort(TelnetPort, SetupWizardText.TelnetPortRequired, SetupWizardText.ValidTelnetPort, errors);
                    break;

                case EnumSetupStep.Rlogin when RloginEnabled:
                    ValidatePort(RloginPort, SetupWizardText.RloginPortRequired, SetupWizardText.ValidRloginPort, errors);

                    if (!IPAddress.TryParse(RloginRemoteIP, out _))
                        errors.Add(SetupWizardText.ValidRloginRemoteIP);
                    break;

                case EnumSetupStep.Advanced:
                    if (string.IsNullOrEmpty(DatabaseFilename))
                        errors.Add(SetupWizardText.DatabaseFilenameRequired);

                    if (!TryParseNumber(BtrieveCacheSize, out var cacheSize) || cacheSize < 1)
                        errors.Add(SetupWizardText.ValidBtrieveCacheSize);
                    break;

                case EnumSetupStep.Security:
                    if (_keys.Count == 0)
                        errors.Add(SetupWizardText.DefaultKeyRequired);
                    break;
            }

            return errors;
        }

        /// <summary>
        ///     Title for the error dialog when a step doesn't validate
        /// </summary>
        public static string ValidationTitle(EnumSetupStep step) => step switch
        {
            EnumSetupStep.General => SetupWizardText.GeneralValidationTitle,
            EnumSetupStep.Telnet => SetupWizardText.TelnetValidationTitle,
            EnumSetupStep.Rlogin => SetupWizardText.RloginValidationTitle,
            EnumSetupStep.Advanced => SetupWizardText.AdvancedValidationTitle,
            EnumSetupStep.Security => SetupWizardText.SecurityValidationTitle,
            _ => SetupWizardText.ErrorTitle
        };

        /// <summary>
        ///     Settings to save. A disabled service's other settings are saved as their defaults, so whatever was left
        ///     in its greyed-out fields can't end up in the file.
        /// </summary>
        public AppSettings ToAppSettings() => new()
        {
            BBSTitle = BbsName,
            BBSChannels = Channels,
            CleanupTime = CleanupTime.ToString(@"hh\:mm", CultureInfo.InvariantCulture),
            RegistrationNumber = RegistrationNumber,
            ModuleDoLoginRoutine = DoLoginRoutine,
            TelnetEnabled = TelnetEnabled,
            TelnetPort = TelnetEnabled ? TelnetPort : DefaultTelnetPort,
            TelnetHeartbeat = TelnetEnabled ? TelnetHeartbeat : DefaultTelnetHeartbeat,
            RloginEnabled = RloginEnabled,
            RloginPort = RloginEnabled ? RloginPort : DefaultRloginPort,
            RloginRemoteIP = RloginEnabled ? RloginRemoteIP : DefaultRloginRemoteIP,
            RloginPortPerModule = RloginEnabled ? RloginPortPerModule : DefaultRloginPortPerModule,
            UserDatabaseFilename = DatabaseFilename,
            BtrieveCacheSize = TryParseNumber(BtrieveCacheSize, out var cacheSize) ? cacheSize : 0,
            AccountDefaultKeys = _keys.ToArray()
        };

        /// <summary>
        ///     The appsettings.json that will be written
        /// </summary>
        public string ToJson()
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            if (_existing == null)
                return JsonSerializer.Serialize(ToAppSettings(), options);

            var edited = JsonSerializer.SerializeToNode(ToAppSettings())!.AsObject();
            var merged = _existing.DeepClone().AsObject();
            foreach (var key in EditedKeys)
                merged[key] = edited[key]?.DeepClone();

            return merged.ToJsonString(options);
        }

        /// <summary>
        ///     Starts from an existing appsettings.json: its values for the settings the wizard edits, and everything
        ///     else in it kept for when it is saved. Values that can't be used keep the wizard's defaults; text the
        ///     wizard checks itself (ports, names) is taken as written and checked on Next.
        /// </summary>
        public void LoadFrom(JsonObject settings)
        {
            _existing = settings.DeepClone().AsObject();

            if (Text(settings, "BBS.Title") is { } bbsName)
                BbsName = bbsName;
            if (Text(settings, "BBS.Channels") is { } channels)
                Channels = channels;
            if (Text(settings, "GSBL.BTURNO") is { } registrationNumber && Regex.IsMatch(registrationNumber, @"^\d{8}$"))
                RegistrationNumber = registrationNumber;
            if (TimeSpan.TryParseExact(Text(settings, "Cleanup.Time"), @"hh\:mm", CultureInfo.InvariantCulture, out var cleanupTime))
                CleanupTime = cleanupTime;
            if (bool.TryParse(Text(settings, "Module.DoLoginRoutine"), out var doLoginRoutine))
                DoLoginRoutine = doLoginRoutine;

            if (bool.TryParse(Text(settings, "Telnet.Enabled"), out var telnetEnabled))
                TelnetEnabled = telnetEnabled;
            if (Text(settings, "Telnet.Port") is { } telnetPort)
                TelnetPort = telnetPort;
            if (bool.TryParse(Text(settings, "Telnet.Heartbeat"), out var telnetHeartbeat))
                TelnetHeartbeat = telnetHeartbeat;

            if (bool.TryParse(Text(settings, "Rlogin.Enabled"), out var rloginEnabled))
                RloginEnabled = rloginEnabled;
            if (Text(settings, "Rlogin.Port") is { } rloginPort)
                RloginPort = rloginPort;
            if (Text(settings, "Rlogin.RemoteIP") is { } rloginRemoteIP)
                RloginRemoteIP = rloginRemoteIP;
            if (bool.TryParse(Text(settings, "Rlogin.PortPerModule"), out var rloginPortPerModule))
                RloginPortPerModule = rloginPortPerModule;

            if (Text(settings, "Database.File") is { } databaseFilename)
                DatabaseFilename = databaseFilename;
            if (Text(settings, "Btrieve.CacheSize") is { } btrieveCacheSize)
                BtrieveCacheSize = btrieveCacheSize;

            if (settings["Account.DefaultKeys"] is JsonArray keys)
            {
                _keys.Clear();
                foreach (var key in keys.OfType<JsonValue>())
                    AddKey(key.ToString());
            }
        }

        /// <summary>
        ///     A setting's value as text, whether the file has it as a string, a number or a boolean
        /// </summary>
        private static string Text(JsonObject settings, string key) => settings[key] is JsonValue value ? value.ToString() : null;

        private static void ValidatePort(string port, string requiredMessage, string invalidMessage, List<string> errors)
        {
            if (string.IsNullOrEmpty(port))
            {
                errors.Add(requiredMessage);
                return;
            }

            if (!TryParseNumber(port, out var value) || value < 1 || value > 65535)
                errors.Add(invalidMessage);
        }

        private static bool IsAllDigits(string value) => value.Length > 0 && value.All(char.IsAsciiDigit);

        /// <summary>
        ///     Parses a plain run of digits; signs, spaces and values beyond int range are rejected
        /// </summary>
        private static bool TryParseNumber(string value, out int result)
        {
            result = 0;
            return !string.IsNullOrEmpty(value) && IsAllDigits(value) &&
                   int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out result);
        }
    }
}
