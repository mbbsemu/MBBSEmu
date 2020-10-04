namespace MBBSEmu.Module
{
    /// <summary>
    ///     Module Configuration
    /// </summary>
        
    public class ModuleConfiguration
    {
        /// <summary>
        ///     Module "Identifier" from moduleConfig.json or commandline
        /// </summary>
        public string ModIdentifier { get; set; }
        
        /// <summary>
        ///     Module "Path" from moduleConfig.json or commandline
        /// </summary>
        public string ModPath { get; set; }

        /// <summary>
        ///     Module "MenuOptionKey" from moduleConfig.json or commandline
        /// </summary>
        public string ModMenuOptionKey { get; set; }
    }
}
