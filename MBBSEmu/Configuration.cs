using System;
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
        public string BBSTitle => GetStringAppSettings("BBS.Title");
        public TimeSpan CleanupTime => GetCleanUpTimeSettings("Cleanup.Time");
        public string GSBLActivation => GetGSBLAppSettings("GSBL.Activation");
        public bool ModuleDoLoginRoutine => GetBoolAppSettings("Module.DoLoginRoutine");
        public bool TelnetEnabled => GetBoolAppSettings("Telnet.Enabled");
        public int TelnetPort => GetIntAppSettings("Telnet.Port");
        public bool RloginEnabled => GetBoolAppSettings("Rlogin.Enabled");
        public int RloginPort => GetIntAppSettings("Rlogin.Port");
        public string RloginoRemoteIP => GetStringAppSettings("Rlogin.RemoteIP");
        public bool RloginPortPerModule => GetBoolAppSettings("Rlogin.PortPerModule");
        public string DatabaseFile => GetStringAppSettings("Database.File");
        
        //Optional Keys
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

        private string GetAppSettings<T>(string key)
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

        private string GetStringAppSettings(string key)
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

        private int GetIntAppSettings(string key)
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

        private bool GetBoolAppSettings(string key)
        {
            var value = GetStringAppSettings(key);
            if (!bool.TryParse(value, out var result))
            {
                throw new Exception($"Invalid boolean for { key } in { Program._settingsFileName ?? Program.DefaultEmuSettingsFilename }");
            }
            return result;
        }

        private TimeSpan GetCleanUpTimeSettings(string key)
        {
            if (!TimeSpan.TryParse(ConfigurationRoot[key], out var result))
            {
                //Set Default 3am
                result = TimeSpan.Parse("03:00");
            }
            return result;
        }
        
        private string GetGSBLAppSettings(string key)
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
