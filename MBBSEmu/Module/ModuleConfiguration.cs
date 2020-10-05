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
        public string ModuleIdentifier { get; set; }
        
        /// <summary>
        ///     Module "Path" from moduleConfig.json or commandline
        /// </summary>
        public string ModulePath { get; set; }

        /// <summary>
        ///     Module "MenuOptionKey" from moduleConfig.json or commandline
        /// </summary>
        public string MenuOptionKey { get; set; }
    }
}
