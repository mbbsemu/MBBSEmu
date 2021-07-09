using Microsoft.Extensions.Configuration;
using NLog;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;

namespace MBBSEmu
{
    public class AppSettings
    {
        public readonly IConfigurationRoot ConfigurationRoot;

        private readonly ILogger _logger;

        /// <summary>
        ///     Safe loading of appsettings.json for Configuration Builder
        /// </summary>
        /// <returns></returns>
        public AppSettings(ILogger logger)
        {
            _logger = logger;

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
        public int BBSChannels => GetAppSettingsFromConfiguration<int>("BBS.Channels");
        public TimeSpan CleanupTime => GetCleanUpTimeSettings("Cleanup.Time");
        public string GSBLBTURNO => GetGSBLBTURNOSettings("GSBL.BTURNO");
        public bool ModuleDoLoginRoutine => GetAppSettingsFromConfiguration<bool>("Module.DoLoginRoutine");
        public bool TelnetEnabled => GetAppSettingsFromConfiguration<bool>("Telnet.Enabled");
        public int TelnetPort => GetAppSettingsFromConfiguration<int>("Telnet.Port");
        public bool TelnetHeartbeat => GetAppSettingsFromConfiguration<bool>("Telnet.Heartbeat");
        public bool RloginEnabled => GetAppSettingsFromConfiguration<bool>("Rlogin.Enabled");
        public int RloginPort => GetAppSettingsFromConfiguration<int>("Rlogin.Port");
        public string RloginRemoteIP => GetIPAppSettings("Rlogin.RemoteIP");
        public bool RloginPortPerModule => GetAppSettingsFromConfiguration<bool>("Rlogin.PortPerModule");
        public string DatabaseFile => GetFileNameAppSettings("Database.File");
        public int BtrieveCacheSize => GetAppSettingsFromConfiguration<int>("Btrieve.CacheSize");
        public int TimerHertz => GetTimerHertz("Timer.Hertz");

        //Optional Keys
        public string GetBTURNO(string moduleId) => ConfigurationRoot[$"GSBL.BTURNO.{moduleId}"];
        public string ANSILogin => ConfigurationRoot["ANSI.Login"];
        public string ANSILogoff => ConfigurationRoot["ANSI.Logoff"];
        public string ANSISignup => ConfigurationRoot["ANSI.Signup"];
        public string ANSIMenu => ConfigurationRoot["ANSI.Menu"];
        public string ConsoleLogLevel => ConfigurationRoot["Console.LogLevel"];
        public string FileLogName => GetFileNameAppSettings("File.LogName");
        public string FileLogLevel => ConfigurationRoot["File.LogLevel"];
        public string TelnetIPAddress => GetIPAppSettings("Telnet.IP");
        public string RloginIPAddress => GetIPAppSettings("Rlogin.IP");
        
        public Session.Rlogin.EnumRloginCompatibility RloginCompatibility
        {
            get
            {
                if (!ConfigurationRoot.GetSection("Rlogin.Compatibility").Exists() || 
                    !Enum.TryParse(typeof(Session.Rlogin.EnumRloginCompatibility), ConfigurationRoot["Rlogin.Compatibility"], out var result ))
                    return Session.Rlogin.EnumRloginCompatibility.Default;

                return (Session.Rlogin.EnumRloginCompatibility)result;
            }
        }

        public IEnumerable<string> DefaultKeys
        {
            get
            {
                if (!ConfigurationRoot.GetSection("Account.DefaultKeys").Exists())
                    return new[] { "DEMO", "NORMAL", "USER" };

                return ConfigurationRoot.GetSection("Account.DefaultKeys").GetChildren()
                    .ToArray().Select(c => c.Value).ToArray();
            }
        }

        public T GetAppSettingsFromConfiguration<T>(string valueName) => GetAppSettings<T>(ConfigurationRoot[valueName], valueName);

        //Default Values not in appSettings
        public string BBSCompanyName = "MBBSEmu\0";
        public string BBSAddress1 = "4101 SW 47th Ave., Suite 101\0";
        public string BBSAddress2 = "Fort Lauderdale, FL 33314\0";
        public string BBSDataPhone = "(305) 583-7808\0";
        public string BBSVoicePhone = "(305) 583-5990\0";

        public T GetAppSettings<T>(object value, string valueName)
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
                        _logger.Warn($"{valueName} not specified in {Program._settingsFileName ?? Program.DefaultEmuSettingsFilename} -- setting default value: {value}");
                        return (T)value;
                    case "Module.DoLoginRoutine":
                        value = true;
                        _logger.Warn($"{valueName} not specified in {Program._settingsFileName ?? Program.DefaultEmuSettingsFilename} -- setting default value: {value}");
                        return (T)value;
                    case "Telnet.Enabled":
                        value = false;
                        _logger.Warn($"{valueName} not specified in {Program._settingsFileName ?? Program.DefaultEmuSettingsFilename} -- setting default value: {value}");
                        return (T)value;
                    case "Telnet.Heartbeat":
                        value = false;
                        _logger.Warn($"{valueName} not specified in {Program._settingsFileName ?? Program.DefaultEmuSettingsFilename} -- setting default value: {value}");
                        return (T)value;
                    case "Telnet.Port":
                        value = 23;
                        _logger.Warn($"{valueName} not specified in {Program._settingsFileName ?? Program.DefaultEmuSettingsFilename} -- setting default value: {value}");
                        return (T)value; ;
                    case "Rlogin.Enabled":
                        value = false;
                        _logger.Warn($"{valueName} not specified in {Program._settingsFileName ?? Program.DefaultEmuSettingsFilename} -- setting default value: {value}");
                        return (T)value;
                    case "Rlogin.Port":
                        value = 513;
                        _logger.Warn($"{valueName} not specified in {Program._settingsFileName ?? Program.DefaultEmuSettingsFilename} -- setting default value: {value}");
                        return (T)value;
                    case "Rlogin.PortPerModule":
                        value = false;
                        _logger.Warn($"{valueName} not specified in {Program._settingsFileName ?? Program.DefaultEmuSettingsFilename} -- setting default value: {value}");
                        return (T)value;
                    case "Btrieve.CacheSize":
                        value = 4;
                        _logger.Warn($"{valueName} not specified in {Program._settingsFileName ?? Program.DefaultEmuSettingsFilename} -- setting default value: {value}");
                        return (T)value;
                    default:
                        return default;
                }
            }
        }

        private string GetFileNameAppSettings(string key)
        {
            //strip paths
            var pathFile = Path.GetFileName(ConfigurationRoot[key]);

            if (!string.IsNullOrEmpty(pathFile))
                return pathFile;

            switch (key)
            {
                case "Database.File":
                    _logger.Warn($"No valid database filename set in {Program._settingsFileName ?? Program.DefaultEmuSettingsFilename} -- setting default value: mbbsemu.db");
                    pathFile = "mbbsemu.db";
                    return pathFile;
                case "File.LogName":
                    //File Logging Disabled
                    pathFile = "";
                    return pathFile;
                default:
                    return default;
            }
        }

        private int GetTimerHertz(string key)
        {
            if (!int.TryParse(ConfigurationRoot[key], out var timerHertz))
            {
                _logger.Warn($"{key} not specified in {Program._settingsFileName ?? Program.DefaultEmuSettingsFilename} -- setting default value: 36");
                timerHertz = 36;
            }

            if (timerHertz < 0 || timerHertz > 1000)
            {
                _logger.Warn("Timer.Hertz outside of valid range of 0-1000 - defaulting to 36");
                timerHertz = 36;
            }
            return timerHertz;
        }

        private string GetIPAppSettings(string key)
        {
            if (IPAddress.TryParse(ConfigurationRoot[key], out var result))
            {
                switch (key)
                {
                    case "Rlogin.RemoteIP":
                        return result.ToString();
                    case "Telnet.IP":
                        if (IsValidHostIP(result))
                            return result.ToString();
                        _logger.Warn($"{key} {result} not found on system -- setting default value: 0.0.0.0");
                        return "0.0.0.0";
                    case "Rlogin.IP":
                        if (IsValidHostIP(result))
                            return result.ToString();
                        _logger.Warn($"{key} {result} not found on system -- setting default value: 0.0.0.0");
                        return "0.0.0.0";
                }
            }

            switch (key)
            {
                case "RLogin.RemoteIP":
                    _logger.Warn($"{key} not specified in {Program._settingsFileName ?? Program.DefaultEmuSettingsFilename} -- setting default value: 127.0.0.1");
                    return "127.0.0.1";
                case "Telnet.IP":
                    //Return default "any"
                    return "0.0.0.0";
                case "Rlogin.IP":
                    //Return default "any"
                    return "0.0.0.0";
            }

            return "0.0.0.0";
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
                _logger.Warn($"{key} not specified in {Program._settingsFileName ?? Program.DefaultEmuSettingsFilename} -- setting default value: {result}");
            }
            return result;
        }

        /// <summary>
        ///     Sets a default GSBL value if none provided
        /// </summary>
        /// <param name="key">GSBL.BTURNO</param>
        /// <returns></returns>
        private string GetGSBLBTURNOSettings(string key)
        {
            var rnd = new Random();
            var result = rnd.Next(10000000, 99999999).ToString();

            if (!string.IsNullOrEmpty(ConfigurationRoot[key]))
            {
                //Set Default
                result = ConfigurationRoot[key];
            }
            else
            {
                _logger.Warn($"{key} not specified in {Program._settingsFileName ?? Program.DefaultEmuSettingsFilename} -- setting random value: {result}");
            }

            return result;
        }

        /// <summary>
        ///     Validates that an IP Address exists on the host
        /// </summary>
        /// <param name="checkIPAddress">IPAddress</param>
        /// <returns></returns>
        private bool IsValidHostIP(IPAddress checkIPAddress)
        {
            return Dns.GetHostEntry(Dns.GetHostName()).AddressList.Any(x => x.Equals(checkIPAddress));
        }

        /// <summary>
        ///     Validates that a JSON file is a correct Format
        /// </summary>
        /// <param name="strInput"></param>
        /// <returns></returns>
        private bool IsValidJson(string strInput)
        {
            strInput = strInput.Trim();
            if (strInput.StartsWith("{") && strInput.EndsWith("}") || //For object
                strInput.StartsWith("[") && strInput.EndsWith("]")) //For array
            {
                try
                {
                    JsonDocument.Parse(strInput);
                    return true;
                }
                catch (JsonException jex)
                {
                    //Exception in parsing json
                    _logger.Warn($"JSON Parsing Error: {jex.Message}");
                    return false;
                }
                catch (Exception ex) //some other exception
                {
                    _logger.Warn($"JSON Parsing Exception: {ex.Message}");
                    return false;
                }
            }

            return false;
        }
    }
}
