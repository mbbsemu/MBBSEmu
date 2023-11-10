using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MBBSEmu.Module
{
    /// <summary>
    ///     Module Configuration
    /// </summary>

    public class ModuleConfiguration
    {
        /// <summary>
        ///     Base Path for all Modules defined within the Module Configuration File
        /// </summary>
        public string BasePath;

        /// <summary>
        ///     Backing Field for Module Path
        /// </summary>
        private string _modulePath;

        /// <summary>
        ///     Default Constructor
        /// </summary>
        public ModuleConfiguration()
        {
        }

        /// <summary>
        ///     Constructor with Base Path
        /// </summary>
        /// <param name="basePath">
        ///     Base Path for all Modules defined within the Module Configuration File
        /// </param>
        public ModuleConfiguration(string basePath)
        {
            BasePath = basePath;
        }

        /// <summary>
        ///     Module "Identifier" from moduleConfig.json or commandline
        /// </summary>
        [JsonPropertyName("Identifier")]
        public string ModuleIdentifier { get; set; }

        /// <summary>
        ///     Module "Path" from moduleConfig.json or commandline
        ///
        ///     If Base Path is configured in the parent node, it will be prepended to this value
        /// </summary>
        [JsonPropertyName("Path")]
        public string ModulePath
        {
            get
            {
                //If a base path specified and the module path is relative (both Linux and Windows), combine them
                if (!string.IsNullOrWhiteSpace(BasePath) && !System.IO.Path.IsPathRooted(_modulePath))
                    return System.IO.Path.Combine(BasePath, _modulePath);

                return _modulePath;
            }
            set => _modulePath = value;
        }

        /// <summary>
        ///     Module "MenuOptionKey" from moduleConfig.json or commandline
        /// </summary>
        [JsonPropertyName("MenuOptionKey")]
        public string MenuOptionKey { get; set; }

        /// <summary>
        ///     Flag to determine if module should be loaded
        /// </summary>
        [JsonPropertyName("Enabled")]
        public bool? ModuleEnabled { get; set; }

        /// <summary>
        ///     List of defined patches to apply at runtime
        /// </summary>
        public IEnumerable<ModulePatch> Patches { get; set; }
    }
}