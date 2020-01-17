using System;

namespace MBBSEmu.Disassembler.Artifacts
{
    [Flags]
    public enum EnumRecordsFlag
    {
        INTERNALREF = 0,
        IMPORTORDINAL = 1,
        IMPORTNAME = 1 << 1,
        //TARGET_MASK = 0x03,
        ADDITIVE = 1 << 2
    }
}