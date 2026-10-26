using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text.Json;

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

        private readonly List<string> _keys = new(DefaultKeys);

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
        public string TelnetPort { get; set; } = "23";
        public bool TelnetHeartbeat { get; set; }

        public bool RloginEnabled { get; set; }
        public string RloginPort { get; set; } = "513";
        public string RloginRemoteIP { get; set; } = "127.0.0.1";
        public bool RloginPortPerModule { get; set; } = true;

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

                case EnumSetupStep.Telnet:
                    ValidatePort(TelnetEnabled, TelnetPort, SetupWizardText.TelnetPortRequired, SetupWizardText.ValidTelnetPort, errors);
                    break;

                case EnumSetupStep.Rlogin:
                    ValidatePort(RloginEnabled, RloginPort, SetupWizardText.RloginPortRequired, SetupWizardText.ValidRloginPort, errors);

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

        public AppSettings ToAppSettings() => new()
        {
            BBSTitle = BbsName,
            BBSChannels = Channels,
            CleanupTime = CleanupTime.ToString(@"hh\:mm", CultureInfo.InvariantCulture),
            RegistrationNumber = RegistrationNumber,
            ModuleDoLoginRoutine = DoLoginRoutine,
            TelnetEnabled = TelnetEnabled,
            TelnetPort = TelnetPort,
            TelnetHeartbeat = TelnetHeartbeat,
            RloginEnabled = RloginEnabled,
            RloginPort = RloginPort,
            RloginRemoteIP = RloginRemoteIP,
            RloginPortPerModule = RloginPortPerModule,
            UserDatabaseFilename = DatabaseFilename,
            BtrieveCacheSize = TryParseNumber(BtrieveCacheSize, out var cacheSize) ? cacheSize : 0,
            AccountDefaultKeys = _keys.ToArray()
        };

        /// <summary>
        ///     The appsettings.json that will be written
        /// </summary>
        public string ToJson() => JsonSerializer.Serialize(ToAppSettings(), new JsonSerializerOptions { WriteIndented = true });

        private static void ValidatePort(bool enabled, string port, string requiredMessage, string invalidMessage, List<string> errors)
        {
            if (string.IsNullOrEmpty(port))
            {
                if (enabled)
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
