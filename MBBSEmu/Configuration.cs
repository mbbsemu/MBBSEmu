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
        public string BBSTitle => GetBBSTitleSettings("BBS.Title");
        public TimeSpan CleanupTime => GetCleanUpTimeSettings("Cleanup.Time");
        public string GSBLActivation => GetGSBLAppSettings("GSBL.Activation");
        public string GSBLModuleActivation => GetGSBLModuleAppSettings($"GSBL.Activation"); // (Tuday) NOT WORKING -- can't figure out how to get .<module> and create a string for each?
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
        ///     Sets a default GSBL value if none provided
        /// </summary>
        /// <param name="key">GSBL.Activation</param>
        /// <returns></returns>
        private string GetGSBLModuleAppSettings(string key)
        {
            var result = "11111111";

            var GSBLActivationModule = ConfigurationRoot.GetSection(key);

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
