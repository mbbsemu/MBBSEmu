using System.Collections.Generic;

namespace MBBSEmu.Module
{
    /// <summary>
    ///     Class Representing a Deserialized Module Configuration File (JSON)
    /// </summary>
    public class ModuleConfigurationFile
    {
        /// <summary>
        ///     Base Path for all Modules defined within the Module Configuration File
        /// </summary>
        public string BasePath { get; set; }

        /// <summary>
        ///     Array of Modules to be loaded by MBBSEmu
        /// </summary>
        public List<ModuleConfiguration> Modules { get; set; }
    }
}
