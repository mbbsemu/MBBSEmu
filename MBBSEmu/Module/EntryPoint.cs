using System;
using System.Collections.Generic;
using System.Text;

namespace MBBSEmu.Module
{
    public class EntryPoint
    {
        public ushort Segment { get; set; }
        public ushort Offset { get; set; }

        public EntryPoint(ushort segment, ushort offset)
        {
            Segment = segment;
            Offset = offset;
        }
    }
}
