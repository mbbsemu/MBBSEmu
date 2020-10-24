using System;
using System.IO;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MBBSEmu
{
    public static class AppSettings
    {
        public static readonly IConfigurationRoot ConfigurationRoot;

        /// <summary>
        ///     Safe loading of appsettings.json for Configuration Builder
        /// </summary>
        /// <returns></returns>
        static AppSettings()
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
        public static string BBSTitle => GetStringAppSettings("BBS.Title");
        public static TimeSpan CleanupTime => GetCleanUpTimeSettings("Cleanup.Time");
        public static string GSBLActivation => GetStringAppSettings("GSBL.Activation");
        public static bool ModuleDoLoginRoutine => GetBoolAppSettings("Module.DoLoginRoutine");
        public static bool TelnetEnabled => GetBoolAppSettings("Telnet.Enabled");
        public static int TelnetPort => GetIntAppSettings("Telnet.Port");
        public static bool RloginEnabled => GetBoolAppSettings("Rlogin.Enabled");
        public static int RloginPort => GetIntAppSettings("Rlogin.Port");
        public static string RloginoRemoteIP => GetStringAppSettings("Rlogin.RemoteIP");
        public static bool RloginPortPerModule => GetBoolAppSettings("Rlogin.PortPerModule");
        public static string DatabaseFile => GetStringAppSettings("Database.File");
        
        //Optional Keys
        public static string ANSILogin => ConfigurationRoot["ANSI.Logon"];
        public static string ANSILogoff => ConfigurationRoot["ANSI.Logoff"];
        public static string ANSISignup => ConfigurationRoot["ANSI.Signup"];
        public static string ANSIMenu => ConfigurationRoot["ANSI.Menu"];

        private static string GetStringAppSettings(string key)
        {
            if (string.IsNullOrEmpty(ConfigurationRoot[key]))
            {
                throw key switch
                {
                    "Database.File" => new Exception($"Please set a valid database filename(eg: mbbsemu.db) in the {Program._settingsFileName ?? Program.DefaultEmuSettingsFilename} file before running MBBSEmu"),
                    "Telnet.Port" => new Exception($"You must specify a port via {key} in {Program._settingsFileName ?? Program.DefaultEmuSettingsFilename} if you're going to enable Telnet"),
                    "Rlogin.Port" => new Exception($"You must specify a port via {key} in {Program._settingsFileName ?? Program.DefaultEmuSettingsFilename} if you're going to enable Rlogin"),
                    "Rlogin.RemoteIP" => new Exception("For security reasons, you must specify an authorized Remote IP via Rlogin.Port if you're going to enable Rlogin"),
                    _ => new Exception($"Missing {key} in {Program._settingsFileName ?? Program.DefaultEmuSettingsFilename}"),
                };
            }
            return ConfigurationRoot[key];
        }

        private static int GetIntAppSettings(string key)
        {
            var value = GetStringAppSettings(key);
            if (!int.TryParse(value, out var result))
            {
                throw key switch
                {
                    "Telnet.Port" => new Exception($"You must specify a valid port number via {key} in {Program._settingsFileName ?? Program.DefaultEmuSettingsFilename} if you're going to enable Telnet"),
                    "Rlogin.Port" => new Exception($"You must specify a valid port number via {key} in {Program._settingsFileName ?? Program.DefaultEmuSettingsFilename} if you're going to enable Rlogin"),
                    "Rlogin.RemoteIP" => new Exception($"For security reasons you must specify a valid authorized Remote IP via {key} in {Program._settingsFileName ?? Program.DefaultEmuSettingsFilename} if you're going to enable Rlogin"),
                    _ => new Exception($"Invalid integer for {key} in {Program._settingsFileName ?? Program.DefaultEmuSettingsFilename}"),
                };
            }
            return result;
        }

        private static bool GetBoolAppSettings(string key)
        {
            var value = GetStringAppSettings(key);
            if (!bool.TryParse(value, out var result))
            {
                throw new Exception($"Invalid boolean for { key } in { Program._settingsFileName ?? Program.DefaultEmuSettingsFilename }");
            }
            return result;
        }

        private static TimeSpan GetCleanUpTimeSettings(string key)
        {
            //var value = GetStringAppSettings(key);
            if (!TimeSpan.TryParse(ConfigurationRoot[key], out var result))
            {
                //Set Default
                result = TimeSpan.Parse("03:00");
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
