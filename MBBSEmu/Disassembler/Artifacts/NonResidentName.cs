namespace MBBSEmu.Disassembler.Artifacts
{
    /// <summary>
    ///     Represents a single record in the Non-Resident Name Table
    /// </summary>
    public class NonResidentName
    {
        public string Name { get; set; }
        public ushort IndexIntoEntryTable { get; set; }
    }
}