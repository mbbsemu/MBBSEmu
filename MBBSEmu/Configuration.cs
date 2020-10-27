using System;
using System.ComponentModel;
using System.IO;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MBBSEmu
{
    public class AppSettings
    {
        public readonly IConfigurationRoot ConfigurationRoot;

        /// <summary>
        ///     Safe loading of appsettings.json for Configuration Builder
        /// </summary>
        /// <returns></returns>
        public AppSettings()
        {
            if (!File.Exists(Program._settingsFileName ?? Program.DefaultEmuSettingsFilename))
                throw new FileNotFoundException($"Unable to locate [{Program._settingsFileName ?? Program.DefaultEmuSettingsFilename}] emulator settings file.");

            if (!IsValidJson(File.ReadAllText(Program._settingsFileName ?? Program.DefaultEmuSettingsFilename)))
                throw new InvalidDataException($"Invalid JSON detected in [{Program._settingsFileName ?? Program.DefaultEmuSettingsFilename}]. Please verify the format & contents of the file are valid JSON.");

            ConfigurationRoot = new ConfigurationBuilder()
                .AddJsonFile(Program._settingsFileName ?? Program.DefaultEmuSettingsFilename, optional: true)
                .Build();
        }

        //Validate Config File
        public string BBSTitle => GetBBSTitleSettings("BBS.Title");
        public TimeSpan CleanupTime => GetCleanUpTimeSettings("Cleanup.Time");
        public string GSBLActivation => GetGSBLActivationSettings("GSBL.Activation");
        public bool ModuleDoLoginRoutine => GetAppSettings<bool>(ConfigurationRoot["Module.DoLoginRoutine"], "Module.DoLoginRoutine");
        public bool TelnetEnabled => GetAppSettings<bool>(ConfigurationRoot["Telnet.Enabled"],"Telnet.Enabled");
        public int TelnetPort => GetAppSettings<int>(ConfigurationRoot["Telnet.Port"],"Telnet.Port");
        public bool RloginEnabled => GetAppSettings<bool>(ConfigurationRoot["Rlogin.Enabled"], "Rlogin.Enabled");
        public int RloginPort => GetAppSettings<int>(ConfigurationRoot["Rlogin.Port"],"Rlogin.Port");
        public string RloginoRemoteIP => GetStringAppSettings("Rlogin.RemoteIP");
        public bool RloginPortPerModule => GetAppSettings<bool>(ConfigurationRoot["Rlogin.PortPerModule"],"Rlogin.PortPerModule");
        public string DatabaseFile => GetStringAppSettings("Database.File");

        //Optional Keys
        public string GetActivation(string moduleId) => ConfigurationRoot[$"GSBL.Activation.{moduleId}"];
        public string ANSILogin => ConfigurationRoot["ANSI.Login"];
        public string ANSILogoff => ConfigurationRoot["ANSI.Logoff"];
        public string ANSISignup => ConfigurationRoot["ANSI.Signup"];
        public string ANSIMenu => ConfigurationRoot["ANSI.Menu"];

        //Default Values not in appSettings
        public string BBSCompanyName = "MBBSEmu\0";
        public string BBSAddress1 = "4101 SW 47th Ave., Suite 101\0";
        public string BBSAddress2 = "Fort Lauderdale, FL 33314\0";
        public string BBSDataPhone = "(305) 583-7808\0";
        public string BBSVoicePhone = "(305) 583-5990\0";

        public static T GetAppSettings<T>(object value, string valueName)
        {
            if (value is T variable) return variable;

            try
            {
                //Handling Nullable types i.e, int?, double?, bool? .. etc
                if (Nullable.GetUnderlyingType(typeof(T)) != null)
                {
                    return (T)TypeDescriptor.GetConverter(typeof(T)).ConvertFrom(value);
                }

                return (T)Convert.ChangeType(value, typeof(T));
            }
            catch (Exception)
            {
                switch (valueName)
                {
                    case "Module.DoLoginRoutine":
                        throw new Exception($"You must specify a value for {valueName} in {Program._settingsFileName ?? Program.DefaultEmuSettingsFilename} -- True or False");
                    case "Telnet.Enabled":
                        throw new Exception($"You must specify a value for {valueName} in {Program._settingsFileName ?? Program.DefaultEmuSettingsFilename} -- True or False");
                    case "Telnet.Port":
                        throw new Exception($"You must specify a value for {valueName} in {Program._settingsFileName ?? Program.DefaultEmuSettingsFilename} -- Example: 23");
                    case "Rlogin.Enabled":
                        throw new Exception($"You must specify a value for {valueName} in {Program._settingsFileName ?? Program.DefaultEmuSettingsFilename} -- True or False");
                    case "Rlogin.Port":
                        throw new Exception($"You must specify a value for {valueName} in {Program._settingsFileName ?? Program.DefaultEmuSettingsFilename} -- Example: 513");
                    case "Rlogin.RemoteIP":
                        throw new Exception($"You must specify a value for {valueName} in {Program._settingsFileName ?? Program.DefaultEmuSettingsFilename} -- Example: 127.0.0.1");
                    case "Rlogin.PortPerModule":
                        throw new Exception($"You must specify a value for {valueName} in {Program._settingsFileName ?? Program.DefaultEmuSettingsFilename} -- True or False");
                    case "Database.File":
                        throw new Exception($"You must specify a value for {valueName} in {Program._settingsFileName ?? Program.DefaultEmuSettingsFilename} -- Example: mbbsemu.db");
                    default:
                        return default(T);
                }
            }
        }

        private string GetStringAppSettings(string key)
        {
            if (string.IsNullOrEmpty(ConfigurationRoot[key]))
            {
                throw key switch
                {
                    "Database.File" => new Exception($"Please set a valid database filename(eg: mbbsemu.db) in the {Program._settingsFileName ?? Program.DefaultEmuSettingsFilename} file before running MBBSEmu"),
                    "Rlogin.RemoteIP" => new Exception("For security reasons, you must specify an authorized Remote IP via Rlogin.Port if you're going to enable Rlogin"),
                    _ => new Exception($"Missing {key} in {Program._settingsFileName ?? Program.DefaultEmuSettingsFilename}"),
                };
            }
            return ConfigurationRoot[key];
        }

        /// <summary>
        ///     Sets a default BBS Title with \0 termination 
        /// </summary>
        /// <param name="key">BBS.Title</param>
        /// <returns></returns>
        private string GetBBSTitleSettings(string key)
        {
            var result = "MBBSEmu\0";
            if (string.IsNullOrEmpty(ConfigurationRoot[key])) return result;
            if (!ConfigurationRoot[key].EndsWith("\0"))
                ConfigurationRoot[key] += "\0";

            result = ConfigurationRoot[key];
            return result;
        }

        /// <summary>
        ///     Sets a default cleanup time if missing or what is provided can't be parsed into TimeSpan
        /// </summary>
        /// <param name="key">Cleanup.Time</param>
        /// <returns></returns>
        private TimeSpan GetCleanUpTimeSettings(string key)
        {
            if (!TimeSpan.TryParse(ConfigurationRoot[key], out var result))
            {
                //Set Default 3am
                result = TimeSpan.Parse("03:00");
            }
            return result;
        }

        /// <summary>
        ///     Sets a default GSBL value if none provided
        /// </summary>
        /// <param name="key">GSBL.Activation</param>
        /// <returns></returns>
        private string GetGSBLActivationSettings(string key)
        {
            var result = "11111111";

            if (!string.IsNullOrEmpty(ConfigurationRoot[key]))
            {
                //Set Default
                result = ConfigurationRoot[key];
            }

            return result;
        }

        /// <summary>
        ///     Validates that a JSON file is a correct Format
        /// </summary>
        /// <param name="strInput"></param>
        /// <returns></returns>
        private static bool IsValidJson(string strInput)
        {
            strInput = strInput.Trim();
            if (strInput.StartsWith("{") && strInput.EndsWith("}") || //For object
                strInput.StartsWith("[") && strInput.EndsWith("]")) //For array
            {
                try
                {
                    JToken.Parse(strInput);
                    return true;
                }
                catch (JsonReaderException jex)
                {
                    //Exception in parsing json
                    Console.WriteLine($"JSON Parsing Error: {jex.Message}");
                    return false;
                }
                catch (Exception ex) //some other exception
                {
                    Console.WriteLine($"JSON Parsing Exception: {ex.Message}");
                    return false;
                }
            }

            return false;
        }
    }
}
