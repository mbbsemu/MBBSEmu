namespace MBBSEmu.DOS.Structs
{
    /// <summary>
    ///     Enumerator used to specify Directory Attribute Flags
    /// </summary>
    public enum EnumDirectoryAttributeFlags : byte
    {
        ReadOnly = 1,
        Hidden = 1 << 1,
        System = 1 << 2,
        VolumeLabel = 1 << 3,
        Directory = 1 << 4,
        Archive = 1 << 5
    }
}
