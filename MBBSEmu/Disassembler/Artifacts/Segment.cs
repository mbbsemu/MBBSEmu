using System;
using System.Collections.Generic;

namespace MBBSEmu.Disassembler.Artifacts
{
    /// <summary>
    ///     Represents a single segment of the segmented EXE/DLL file
    /// </summary>
    public class Segment
    {
        private ushort _flag;
        public ushort Ordinal { get; set; }
        public uint Offset { get; set; }
        public ushort Length { get; set; }
        public ushort MinLength { get; set; }

        public ushort Flag
        {
            get => _flag;
            set
            {
                _flag = value;
                Flags = new List<EnumSegmentFlags>();
                Flags.Add((_flag | (short) EnumSegmentFlags.Data) == _flag
                    ? EnumSegmentFlags.Data
                    : EnumSegmentFlags.Code);

                if ((_flag | (short) EnumSegmentFlags.Iterated) == _flag)
                    Flags.Add(EnumSegmentFlags.Iterated);

                Flags.Add((_flag | (short) EnumSegmentFlags.Movable) == _flag
                    ? EnumSegmentFlags.Movable
                    : EnumSegmentFlags.Fixed);

                Flags.Add((_flag | (short) EnumSegmentFlags.Pure) == _flag
                    ? EnumSegmentFlags.Pure
                    : EnumSegmentFlags.Impure);

                Flags.Add((_flag | (short) EnumSegmentFlags.Preload) == _flag
                    ? EnumSegmentFlags.Preload
                    : EnumSegmentFlags.LoadOnCall);

                Flags.Add((_flag | (short) EnumSegmentFlags.ExecuteOnly) == _flag
                    ? EnumSegmentFlags.ExecuteOnly
                    : EnumSegmentFlags.ReadOnly);

                if ((_flag | (short) EnumSegmentFlags.HasRelocationInfo) == _flag)
                    Flags.Add(EnumSegmentFlags.HasRelocationInfo);

                if ((_flag | (short) EnumSegmentFlags.HasDebuggingInfo) == _flag)
                    Flags.Add(EnumSegmentFlags.HasDebuggingInfo);
            }
        }

        public List<EnumSegmentFlags> Flags { get; private set; }

        public byte[] Data { get; set; }
        
        public List<RelocationRecord> RelocationRecords { get; set; }

        public Segment() {}
        
        public Segment(byte[] segmentHeader)
        {
            Offset = BitConverter.ToUInt16(segmentHeader, 0);
            Length = BitConverter.ToUInt16(segmentHeader, 2);
            Flag = BitConverter.ToUInt16(segmentHeader, 4);
            MinLength = BitConverter.ToUInt16(segmentHeader, 6);
        }
    }
}