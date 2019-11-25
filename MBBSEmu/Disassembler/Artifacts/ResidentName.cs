namespace MBBSEmu.Disassembler.Artifacts
{
    /// <summary>
    ///     Represents a single record in the Resident Name Table
    /// </summary>
    public class ResidentName
    {
        public string Name { get; set; }
        public ushort IndexIntoEntryTable { get; set; }
    }
}