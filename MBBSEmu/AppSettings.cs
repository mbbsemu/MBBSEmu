using System.Text.Json.Serialization;

namespace MBBSEmu
{
    /// <summary>
    ///     Values for the AppSettings.json file
    /// </summary>
    public class AppSettings
    {
        /// <summary>
        ///     Name/Title of the BBS to display within Modules when accessing the BBSTTL Property
        /// </summary>
        [JsonPropertyName("BBS.Title")]
        public string BBSTitle { get; set; }

        /// <summary>
        ///     Number of Channels/Lines your MBBSEmu instance supports
        /// </summary>
        [JsonPropertyName("BBS.Channels")]
        public string BBSChannels { get; set; }

        /// <summary>
        ///     Registration Number of your MBBS/Worldgroup BBS
        /// </summary>
        [JsonPropertyName("GSBL.BTURNO")]
        public string RegistrationNumber { get; set; }

        /// <summary>
        ///     Time of the Day (24-hour Clock) that the MajorBBS/Worldgroup Cleanup Routines will Run
        /// </summary>
        [JsonPropertyName("Cleanup.Time")]
        public string CleanupTime { get; set; }

        /// <summary>
        ///     Toggle if the Login Routines of Modules (LONROU) should be executed when a User logs in
        /// </summary>
        [JsonPropertyName("Module.DoLoginRoutine")]
        public bool ModuleDoLoginRoutine { get; set; }

        /// <summary>
        ///     Specifies if the MBBSEmu Telnet Daemon should be enabled
        /// </summary>
        [JsonPropertyName("Telnet.Enabled")]
        public bool TelnetEnabled { get; set; }

        /// <summary>
        ///     Port to be used by the MBBSEmu Telnet Daemon
        /// </summary>
        [JsonPropertyName("Telnet.Port")]
        public string TelnetPort { get; set; }

        /// <summary>
        ///     Heartbeat for the MBBSEmu Telnet Daemon
        ///
        ///     Assists for connections that are dropped by intermediate firewalls
        /// </summary>
        [JsonPropertyName("Telnet.Heartbeat")]
        public bool TelnetHeartbeat { get; set; }

        /// <summary>
        ///     Convert CP437 extended ASCII (0x80-0xFF) to UTF-8 Unicode before sending to telnet clients.
        ///     Enable this for modern UTF-8 terminals to correctly display box-drawing characters.
        ///     Disable for CP437-capable terminals like SyncTERM.
        /// </summary>
        [JsonPropertyName("Telnet.ConvertCP437ToUTF8")]
        public bool TelnetConvertCP437ToUTF8 { get; set; }

        /// <summary>
        ///     Specifies if the MBBSEmu Rlogin Daemon should be enabled
        /// </summary>
        [JsonPropertyName("Rlogin.Enabled")]
        public bool RloginEnabled { get; set; }

        /// <summary>
        ///     Port to be used by the MBBSEmu Rlogin Daemon
        ///
        ///     When enabling Rlogin Port-Per-Module, this is the starting port number
        /// </summary>
        [JsonPropertyName("Rlogin.Port")]
        public string RloginPort { get; set; }

        /// <summary>
        ///     Remote IP allowed to connect to the MBBSEmu Rlogin Daemon
        /// </summary>
        [JsonPropertyName("Rlogin.RemoteIP")]
        public string RloginRemoteIP { get; set; }

        /// <summary>
        ///     Puts each module loaded by MBBSEmu on its own Rlogin Port, allowing users to directly connect to a module
        ///     from an Rlogin Client.
        /// </summary>
        [JsonPropertyName("Rlogin.PortPerModule")]
        public bool RloginPortPerModule { get; set; }

        /// <summary>
        ///     Database File used by MBBSEmu for the User SQLite Database
        /// </summary>
        [JsonPropertyName("Database.File")]
        public string UserDatabaseFilename { get; set; }

        /// <summary>
        ///     Cache Size (records) for the Btrieve Engine
        /// </summary>
        [JsonPropertyName("Btrieve.CacheSize")]
        public int BtrieveCacheSize { get; set; }

        /// <summary>
        ///     Default Security Keys to give new users
        /// </summary>
        [JsonPropertyName("Account.DefaultKeys")]
        public string[] AccountDefaultKeys { get; set; }
    }
}
