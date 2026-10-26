using MBBSEmu.IO;
using MBBSEmu.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MBBSEmu.Module
{
    /// <summary>
    ///     One Internal module DLL that must be loaded into an already-loaded host
    ///     module's memory, the way Worldgroup loads both DLLs in one process.
    /// </summary>
    public sealed class CompanionAttachment
    {
        public CompanionAttachment(string companionIdentifier, string parentIdentifier)
        {
            CompanionIdentifier = companionIdentifier;
            ParentIdentifier = parentIdentifier;
        }

        public string CompanionIdentifier { get; }

        public string ParentIdentifier { get; }
    }

    /// <summary>
    ///     Decides which enabled modules get their own MbbsModule, and which Internal
    ///     modules join a host that is already loaded.
    ///
    ///     On Worldgroup every DLL shares one process. MBBSEmu keeps that isolation
    ///     for a normal module: each one still gets its own memory. An Internal module
    ///     (the MDF flag) is not a menu module. MajorMUD Plus is Internal and imports
    ///     WCCMMUD, so it has to bind to the resident copy. A second copy runs _INIT_
    ///     again and collides on LOG.LOG and the Btrieve files.
    ///
    ///     The enabled list is the module list. Needs Me names the host relationship.
    ///     It does not turn a module the operator left out into one that loads.
    /// </summary>
    public sealed class ModuleLoadPlan
    {
        public ModuleLoadPlan(List<ModuleConfiguration> hostModules, List<CompanionAttachment> companions)
        {
            HostModules = hostModules;
            Companions = companions;
        }

        public List<ModuleConfiguration> HostModules { get; }

        public List<CompanionAttachment> Companions { get; }

        public static ModuleLoadPlan Build(IReadOnlyList<ModuleConfiguration> configs, IFileUtility files, IMessageLogger logger)
        {
            configs ??= Array.Empty<ModuleConfiguration>();

            var parsed = configs
                .Where(config => config?.ModuleEnabled != false)
                .Select(config => (Config: config, Mdf: TryReadMdf(files, config)))
                .ToList();

            var hosts = new List<(ModuleConfiguration Config, MdfFile Mdf)>();
            var companionConfigs = new List<(ModuleConfiguration Config, MdfFile Mdf)>();
            foreach (var item in parsed)
            {
                // Same rule Worldgroup uses: Internal is not a separate module.
                // Everything else keeps the MbbsModule it was given.
                if (item.Mdf?.IsInternal == true)
                    companionConfigs.Add(item);
                else
                    hosts.Add(item);
            }

            foreach (var companion in companionConfigs)
            {
                foreach (var needed in NeededHostNames(companion.Mdf))
                {
                    if (hosts.Any(host => SameId(host.Config.ModuleIdentifier, needed)))
                        continue;
                    if (companionConfigs.Any(other => SameId(other.Config.ModuleIdentifier, needed)))
                        continue;

                    var synthesized = TrySynthesizeHost(files, companion.Config, needed);
                    if (synthesized.Mdf != null && !synthesized.Mdf.IsInternal)
                    {
                        // The menu key belongs to the module the player enters.
                        if (string.IsNullOrEmpty(synthesized.Config.MenuOptionKey))
                            synthesized.Config.MenuOptionKey = companion.Config.MenuOptionKey;

                        logger?.Info($"({companion.Config.ModuleIdentifier}) Internal module requires {needed}; loading that module first");
                        hosts.Add(synthesized);
                    }
                }
            }

            var attachments = new List<CompanionAttachment>();
            var attached = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var companion in companionConfigs)
            {
                var parent = FindParent(companion.Config.ModuleIdentifier, companion.Mdf, hosts);
                if (parent.Config == null)
                {
                    logger?.Error($"({companion.Config.ModuleIdentifier}) Internal module has no host module to share memory with");
                    continue;
                }

                if (!string.IsNullOrEmpty(companion.Config.MenuOptionKey)
                    && !SameId(companion.Config.MenuOptionKey, parent.Config.MenuOptionKey))
                {
                    logger?.Warn($"({companion.Config.ModuleIdentifier}) Internal module is not its own menu item; it shares the host module's memory");
                }

                if (!attached.Add(companion.Config.ModuleIdentifier))
                    continue;

                attachments.Add(new CompanionAttachment(companion.Config.ModuleIdentifier, parent.Config.ModuleIdentifier));
            }

            return new ModuleLoadPlan(hosts.Select(host => host.Config).ToList(), attachments);
        }

        private static (ModuleConfiguration Config, MdfFile Mdf) FindParent(
            string companionId,
            MdfFile companionMdf,
            List<(ModuleConfiguration Config, MdfFile Mdf)> hosts)
        {
            foreach (var host in hosts)
            {
                if (host.Mdf?.NeedsMe != null && host.Mdf.NeedsMe.Any(name => SameId(name, companionId)))
                    return host;
            }

            foreach (var needed in NeededHostNames(companionMdf))
            {
                foreach (var host in hosts)
                {
                    if (SameId(host.Config.ModuleIdentifier, needed))
                        return host;
                }
            }

            return (null, null);
        }

        private static IEnumerable<string> NeededHostNames(MdfFile mdf)
        {
            if (mdf == null)
                yield break;

            if (mdf.INeed != null)
            {
                foreach (var name in mdf.INeed)
                    yield return name;
            }

            if (mdf.Requires != null)
            {
                foreach (var name in mdf.Requires)
                    yield return name;
            }
        }

        private static (ModuleConfiguration Config, MdfFile Mdf) TrySynthesizeHost(IFileUtility files, ModuleConfiguration companion, string hostId)
        {
            var config = new ModuleConfiguration
            {
                ModuleIdentifier = hostId,
                ModulePath = companion.ModulePath,
                ModuleEnabled = true
            };
            return (config, TryReadMdf(files, config));
        }

        private static MdfFile TryReadMdf(IFileUtility files, ModuleConfiguration config)
        {
            if (config == null || string.IsNullOrWhiteSpace(config.ModuleIdentifier) || string.IsNullOrWhiteSpace(config.ModulePath))
                return null;
            if (!Directory.Exists(config.ModulePath))
                return null;

            var mdfFile = files.FindFile(config.ModulePath, $"{config.ModuleIdentifier}.MDF");
            var fullPath = Path.Combine(config.ModulePath, mdfFile);
            if (!File.Exists(fullPath))
                return null;

            return new MdfFile(fullPath);
        }

        private static bool SameId(string left, string right) =>
            string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    }
}
