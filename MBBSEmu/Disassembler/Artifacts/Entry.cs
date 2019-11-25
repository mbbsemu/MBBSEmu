namespace MBBSEmu.Disassembler.Artifacts
{
    public class Entry
    {
        public ushort Ordinal { get; set; }
        public byte SegmentNumber { get; set; }
        public byte Flag { get; set; }        
        public ushort Offset { get; set; }
    }
}