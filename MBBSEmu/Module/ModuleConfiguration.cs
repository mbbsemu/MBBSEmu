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
        ///     Module "Identifier" from moduleConfig.json or commandline
        /// </summary>
        [JsonPropertyName("Identifier")]
        public string ModuleIdentifier { get; set; }

        /// <summary>
        ///     Module "Path" from moduleConfig.json or commandline
        /// </summary>
        [JsonPropertyName("Path")]
        public string ModulePath { get; set; }

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