using System;
using System.ComponentModel;
using System.IO;
using System.Net;
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

            ConfigurationRoot = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile(Program._settingsFileName ?? Program.DefaultEmuSettingsFilename)
                .Build();
        }

        //Validate Config File
        public string BBSTitle => GetBBSTitleSettings("BBS.Title");
        public int BBSChannels => GetAppSettings<int>(ConfigurationRoot["BBS.Channels"], "BBS.Channels");
        public TimeSpan CleanupTime => GetCleanUpTimeSettings("Cleanup.Time");
        public string GSBLActivation => GetGSBLActivationSettings("GSBL.Activation");
        public bool ModuleDoLoginRoutine => GetAppSettings<bool>(ConfigurationRoot["Module.DoLoginRoutine"], "Module.DoLoginRoutine");
        public bool TelnetEnabled => GetAppSettings<bool>(ConfigurationRoot["Telnet.Enabled"],"Telnet.Enabled");
        public int TelnetPort => GetAppSettings<int>(ConfigurationRoot["Telnet.Port"],"Telnet.Port");
        public bool TelnetHeartbeat => GetAppSettings<bool>(ConfigurationRoot["Telnet.Heartbeat"], "Telnet.Heartbeat");
        public bool RloginEnabled => GetAppSettings<bool>(ConfigurationRoot["Rlogin.Enabled"], "Rlogin.Enabled");
        public int RloginPort => GetAppSettings<int>(ConfigurationRoot["Rlogin.Port"],"Rlogin.Port");
        public string RloginoRemoteIP => GetRemoteIPAppSettings("Rlogin.RemoteIP");
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
                    case "BBS.Channels":
                        value = 4;
                        Console.WriteLine($"{valueName} not specified in {Program._settingsFileName ?? Program.DefaultEmuSettingsFilename} -- setting default value: {value}");
                        return (T)value;
                    case "Module.DoLoginRoutine":
                        value = true;
                        Console.WriteLine($"{valueName} not specified in {Program._settingsFileName ?? Program.DefaultEmuSettingsFilename} -- setting default value: {value}");
                        return (T)value;
                    case "Telnet.Enabled":
                        value = false;
                        Console.WriteLine($"{valueName} not specified in {Program._settingsFileName ?? Program.DefaultEmuSettingsFilename} -- setting default value: {value}");
                        return (T)value;
                    case "Telnet.Heartbeat":
                        value = false;
                        Console.WriteLine($"{valueName} not specified in {Program._settingsFileName ?? Program.DefaultEmuSettingsFilename} -- setting default value: {value}");
                        return (T)value;
                    case "Telnet.Port":
                        value = 23;
                        Console.WriteLine($"{valueName} not specified in {Program._settingsFileName ?? Program.DefaultEmuSettingsFilename} -- setting default value: {value}");
                        return (T)value; ;
                    case "Rlogin.Enabled":
                        value = false;
                        Console.WriteLine($"{valueName} not specified in {Program._settingsFileName ?? Program.DefaultEmuSettingsFilename} -- setting default value: {value}");
                        return (T)value;
                    case "Rlogin.Port":
                        value = 513;
                        Console.WriteLine($"{valueName} not specified in {Program._settingsFileName ?? Program.DefaultEmuSettingsFilename} -- setting default value: {value}");
                        return (T)value;
                    case "Rlogin.PortPerModule":
                        value = false;
                        Console.WriteLine($"{valueName} not specified in {Program._settingsFileName ?? Program.DefaultEmuSettingsFilename} -- setting default value: {value}");
                        return (T)value;
                    default:
                        return default;
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
                    _ => new Exception($"Missing {key} in {Program._settingsFileName ?? Program.DefaultEmuSettingsFilename}"),
                };
            }
            return ConfigurationRoot[key];
        }

        private string GetRemoteIPAppSettings(string key)
        {

            if (IPAddress.TryParse(ConfigurationRoot[key], out var result))
            {
                return result.ToString();
            }
            
            Console.WriteLine($"RLogin.RemoteIP not specified in {Program._settingsFileName ?? Program.DefaultEmuSettingsFilename} -- setting default value: 127.0.0.1");
            return "127.0.0.1";
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
                Console.WriteLine($"Cleanup.Time not specified in {Program._settingsFileName ?? Program.DefaultEmuSettingsFilename} -- setting default value: {result}");
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
            else
            {
                Console.WriteLine($"GSBL.Activation not specified in {Program._settingsFileName ?? Program.DefaultEmuSettingsFilename} -- setting default value: {result}");
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
