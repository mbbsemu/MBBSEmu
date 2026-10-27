using System.Threading.Tasks;

namespace MBBSEmu.UI.Modules
{
    /// <summary>
    ///     What the admin console shows after a load: a message box, or an error box when IsError
    /// </summary>
    public sealed record ModuleLoadReport(bool IsError, string Title, string Message);

    /// <summary>
    ///     Loads modules the sysop picks with File > Load Module
    /// </summary>
    public interface IModuleLoader
    {
        /// <summary>
        ///     Folder the module picker opens in: where the last module was loaded from
        /// </summary>
        string StartFolder { get; }

        /// <summary>
        ///     Modules loaded this session, at startup and since
        /// </summary>
        int LoadedCount { get; }

        /// <summary>
        ///     Loads the module whose .MDF the sysop picked into the running BBS and, when saving is on, adds it to
        ///     modules.json
        /// </summary>
        Task<ModuleLoadReport> LoadAsync(string mdfPath);
    }
}
