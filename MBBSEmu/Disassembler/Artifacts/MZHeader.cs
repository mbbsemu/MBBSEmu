using System;

namespace MBBSEmu.Disassembler.Artifacts
{
    /// <summary>
    ///     One Block == 512 Bytes
    ///     One Paragraph == 16 Bytes
    /// </summary>
    public class MZHeader
    {
        public ushort Signature { get; set; }
        public ushort BytesInLastBlock { get; set; }
        public ushort BlocksInFile { get; set; }
        public ushort RelocationEntries { get; set; }
        public ushort HeaderParagraphs { get; set; }
        public ushort MinExtraParagraphs { get; set; }
        public ushort MaxExtraParagraphs { get; set; }
        public ushort RelativeSS { get; set; }
        public ushort InitialSP { get; set; }
        public ushort Checksum { get; set; }
        public ushort InitialIP { get; set; }
        public ushort InitialCS { get; set; }
        public ushort RelocationOffset { get; set; }
        public ushort OverlayNumber { get; set; }

        public MZHeader(byte[] fileContent)
        {
            Signature = BitConverter.ToUInt16(fileContent, 0);
            BytesInLastBlock = BitConverter.ToUInt16(fileContent, 2);
            BlocksInFile = BitConverter.ToUInt16(fileContent, 4);
            RelocationEntries = BitConverter.ToUInt16(fileContent, 6);
            HeaderParagraphs = BitConverter.ToUInt16(fileContent, 8);
            MinExtraParagraphs = BitConverter.ToUInt16(fileContent, 10);
            MaxExtraParagraphs = BitConverter.ToUInt16(fileContent, 12);
            RelativeSS = BitConverter.ToUInt16(fileContent, 14);
            InitialSP = BitConverter.ToUInt16(fileContent, 16);
            Checksum = BitConverter.ToUInt16(fileContent, 18);
            InitialIP = BitConverter.ToUInt16(fileContent, 20);
            InitialCS = BitConverter.ToUInt16(fileContent, 22);
            RelocationOffset = BitConverter.ToUInt16(fileContent, 24);
            OverlayNumber = BitConverter.ToUInt16(fileContent, 26);
        }
    }
}