namespace MBBSEmu.Disassembler.Artifacts
{
    /// <summary>
    ///     Represents a single record in the Imported Name Table
    /// </summary>
    public class ImportedName
    {
        public ushort Ordinal { get; set; }
        public ushort Offset { get; set; }
        public ushort FileOffset { get; set; }
        public string Name { get; set; }
    }
}