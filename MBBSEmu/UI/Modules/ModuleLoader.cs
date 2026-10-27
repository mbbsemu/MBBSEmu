using MBBSEmu.Module;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace MBBSEmu.UI.Modules
{
    /// <summary>
    ///     Where loaded modules are remembered: the default modules.json, or nowhere and why
    /// </summary>
    public sealed record ModuleSaving(string ModulesFile, string SessionOnlyReason)
    {
        public static ModuleSaving To(string modulesFile) => new(modulesFile, null);

        public static ModuleSaving SessionOnly(string reason) => new(null, reason);
    }

    /// <summary>
    ///     Turns a picked .MDF into a module loaded into the running host, remembers it, and explains the result in
    ///     plain language
    /// </summary>
    public sealed class ModuleLoader : IModuleLoader
    {
        private const string MdfExtension = ".MDF";

        private readonly Func<ModuleConfiguration, Task<ModuleLoadResult>> _loadIntoHost;
        private readonly ModuleSaving _saving;
        private readonly string _playHint;
        private volatile string _startFolder;
        private int _loadedCount;

        /// <param name="loadIntoHost">Loads a module into the running host (IMbbsHost.LoadModule)</param>
        /// <param name="playHint">How to connect and play, from ModuleLoadText.PlayHint</param>
        public ModuleLoader(Func<ModuleConfiguration, Task<ModuleLoadResult>> loadIntoHost, ModuleSaving saving,
            string startFolder, string playHint)
        {
            _loadIntoHost = loadIntoHost;
            _saving = saving;
            _startFolder = startFolder;
            _playHint = playHint;
        }

        public string StartFolder => _startFolder;

        public int LoadedCount => Volatile.Read(ref _loadedCount);

        public async Task<ModuleLoadReport> LoadAsync(string mdfPath)
        {
            var fileName = Path.GetFileName(mdfPath);
            if (!string.Equals(Path.GetExtension(mdfPath), MdfExtension, StringComparison.OrdinalIgnoreCase))
                return new ModuleLoadReport(true, ModuleLoadText.FailedTitle, ModuleLoadText.NotAnMdf(fileName));

            //The module's identifier is its MDF's name, as everywhere else in MBBSEmu
            var fullPath = Path.GetFullPath(mdfPath);
            var folder = Path.GetDirectoryName(fullPath);
            var moduleIdentifier = Path.GetFileNameWithoutExtension(fullPath);
            _startFolder = folder;

            ModuleLoadResult result;
            try
            {
                result = await _loadIntoHost(new ModuleConfiguration
                {
                    ModuleIdentifier = moduleIdentifier,
                    ModulePath = folder,
                    ModuleEnabled = true
                });
            }
            catch (Exception e)
            {
                return new ModuleLoadReport(true, ModuleLoadText.FailedTitle, ModuleLoadText.Failed(moduleIdentifier, e.Message));
            }

            switch (result.Status)
            {
                case EnumModuleLoadStatus.Loaded:
                    Interlocked.Increment(ref _loadedCount);
                    return new ModuleLoadReport(false, ModuleLoadText.LoadedTitle,
                        Save(result.ModuleIdentifier, folder, result.ModuleName, result.MenuOptionKey) + "\n\n" + _playHint);
                case EnumModuleLoadStatus.AlreadyLoaded:
                    return new ModuleLoadReport(false, ModuleLoadText.AlreadyLoadedTitle, ModuleLoadText.AlreadyLoaded(result.ModuleName));
                case EnumModuleLoadStatus.NotFound:
                    return new ModuleLoadReport(true, ModuleLoadText.FailedTitle, ModuleLoadText.NotFound(moduleIdentifier, result.Detail));
                default:
                    return new ModuleLoadReport(true, ModuleLoadText.FailedTitle, ModuleLoadText.Failed(moduleIdentifier, result.Detail));
            }
        }

        /// <summary>
        ///     Loads a module from the saved modules.json at startup; it is already saved, so nothing is written
        /// </summary>
        /// <returns>Null if it loaded, otherwise the line saying why it was skipped</returns>
        public async Task<string> LoadSavedAsync(ModuleConfiguration saved)
        {
            ModuleLoadResult result;
            try
            {
                result = await _loadIntoHost(saved);
            }
            catch (Exception e)
            {
                return ModuleLoadText.SkippedFailed(saved.ModuleIdentifier, e.Message);
            }

            switch (result.Status)
            {
                case EnumModuleLoadStatus.Loaded:
                    Interlocked.Increment(ref _loadedCount);
                    return null;
                case EnumModuleLoadStatus.AlreadyLoaded:
                    return null;
                case EnumModuleLoadStatus.NotFound:
                    return ModuleLoadText.SkippedNotFound(saved.ModuleIdentifier, result.Detail);
                default:
                    return ModuleLoadText.SkippedFailed(saved.ModuleIdentifier, result.Detail);
            }
        }

        /// <summary>
        ///     Remembers a loaded module, if saving is on
        /// </summary>
        /// <returns>The message saying it's loaded and whether it will be loaded again next time</returns>
        private string Save(string moduleIdentifier, string folder, string name, string key)
        {
            if (_saving.ModulesFile == null)
                return ModuleLoadText.LoadedForSession(name, key, _saving.SessionOnlyReason);

            try
            {
                ModuleListFile.Append(_saving.ModulesFile, moduleIdentifier, folder, key);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException)
            {
                return ModuleLoadText.LoadedNotSaved(name, key, e.Message);
            }

            return ModuleLoadText.Loaded(name, key);
        }
    }
}
