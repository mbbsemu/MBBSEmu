using System;

namespace MBBSEmu.Disassembler.Artifacts
{
    [Flags]
    public enum EnumRecordsFlag
    {
        INTERNALREF = 0x00,
        IMPORTORDINAL = 0x01,
        IMPORTNAME = 0x02,
        TARGET_MASK = 0x03,
        ADDITIVE = 0x04
    }
}