namespace MBBSEmu.Disassembler.Artifacts
{
    /// <summary>
    ///     Represents a single record in the Module Reference Table
    /// </summary>
    public class ModuleReference
    {
        public ushort ImportNameTableOffset { get; set; }
        public string Name { get; set; }
    }
}