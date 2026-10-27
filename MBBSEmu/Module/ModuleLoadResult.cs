namespace MBBSEmu.Module
{
    public enum EnumModuleLoadStatus
    {
        Loaded,
        AlreadyLoaded,

        /// <summary>
        ///     The module's folder or .MDF doesn't exist
        /// </summary>
        NotFound,

        /// <summary>
        ///     The module was found but couldn't be loaded or didn't initialize; nothing of it was kept
        /// </summary>
        Failed
    }

    /// <summary>
    ///     Outcome of loading a module into a running host
    /// </summary>
    /// <param name="ModuleName">The module's description, as shown on the main menu</param>
    /// <param name="Detail">The missing path for NotFound, the reason for Failed, otherwise null</param>
    public sealed record ModuleLoadResult(EnumModuleLoadStatus Status, string ModuleIdentifier, string ModuleName,
        string MenuOptionKey, string Detail);
}
