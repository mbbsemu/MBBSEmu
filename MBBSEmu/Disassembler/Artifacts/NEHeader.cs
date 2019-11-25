using System;

namespace MBBSEmu.Disassembler.Artifacts
{
    public class NEHeader
    {
        public ushort FileOffset { get; set; }
        public byte LinkerVersion { get; set; }
        public byte LinkerRevision { get; set; }
        public ushort EntryTableOffset { get; set; }
        public ushort EntryTableLength { get; set; }
        public uint FileCRC32 { get; set; }
        public ushort FlagWord { get; set; }
        public ushort AutomaticDataSegment { get; set; }
        public ushort DataSegmentHeapInitialSize { get; set; }
        public ushort DataSegmentStackInitialSize { get; set; }
        public uint SegmentNumberOffsetCSIP { get; set; }
        public uint SegmentNumberOffsetSSSP { get; set; }
        public ushort SegmentTableEntries { get; set; }
        public ushort ModuleReferenceTableEntries { get; set; }
        public ushort NonResidentNameTableLength { get; set; }
        public ushort SegmentTableOffset { get; set; }
        public ushort ResourceTableOffset { get; set; }
        public ushort ResidentNameTableOffset { get; set; }
        public ushort ModleReferenceTableOffset { get; set; }
        public ushort ImportedNamesTableOffset { get; set; }
        public uint NonResidentNameTableOffset { get; set; }
        public ushort MovableEntrypoints { get; set; }
        public ushort LogicalSectorAlignmentShift { get; set; }
        public ushort ResourceEntries { get; set; }
        public byte ExecutableType { get; set; }
        public byte[] Reserved { get; set; }
        
        public NEHeader(byte[] headerContents)
        {
            if (headerContents[0] != 'N' || headerContents[1] != 'E')
                throw new Exception("Invalid Windows Header Signature Word");

            LinkerVersion = headerContents[0x02];
            LinkerRevision = headerContents[0x03];
            EntryTableOffset = BitConverter.ToUInt16(headerContents, 0x04);
            EntryTableLength = BitConverter.ToUInt16(headerContents, 0x06);
            FileCRC32 = BitConverter.ToUInt32(headerContents, 0x08);
            FlagWord = BitConverter.ToUInt16(headerContents, 0x0C);
            AutomaticDataSegment = BitConverter.ToUInt16(headerContents, 0x0E);
            DataSegmentHeapInitialSize = BitConverter.ToUInt16(headerContents, 0x10);
            DataSegmentStackInitialSize = BitConverter.ToUInt16(headerContents, 0x12);
            SegmentNumberOffsetCSIP = BitConverter.ToUInt32(headerContents, 0x14);
            SegmentNumberOffsetSSSP = BitConverter.ToUInt32(headerContents, 0x18);
            SegmentTableEntries = BitConverter.ToUInt16(headerContents, 0x1C);
            ModuleReferenceTableEntries = BitConverter.ToUInt16(headerContents, 0x1E);
            NonResidentNameTableLength = BitConverter.ToUInt16(headerContents, 0x20);
            SegmentTableOffset = BitConverter.ToUInt16(headerContents, 0x22);
            ResourceTableOffset = BitConverter.ToUInt16(headerContents, 0x24);
            ResidentNameTableOffset = BitConverter.ToUInt16(headerContents, 0x26);
            ModleReferenceTableOffset = BitConverter.ToUInt16(headerContents, 0x28);
            ImportedNamesTableOffset = BitConverter.ToUInt16(headerContents, 0x2A);
            NonResidentNameTableOffset = BitConverter.ToUInt32(headerContents, 0x2C);
            MovableEntrypoints = BitConverter.ToUInt16(headerContents, 0x30);
            LogicalSectorAlignmentShift = BitConverter.ToUInt16(headerContents, 0x32);
            ResourceEntries = BitConverter.ToUInt16(headerContents, 0x34);
            ExecutableType = headerContents[0x36];
            Reserved = new byte[8];
            Array.Copy(headerContents, 0x37, Reserved, 0, 8);
        }
    }
}