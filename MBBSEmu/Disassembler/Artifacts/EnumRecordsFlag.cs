using System;

namespace MBBSEmu.Disassembler.Artifacts
{
    public enum EnumRecordsFlag
    {
        InternalRef = 0,
        ImportOrdinal = 1,
        ImportName = 2,
        OSFIXUP = 3,
        InternalRefAdditive = 4,
        ImportOrdinalAdditive = 5,
        ImportNameAdditive = 6,
        OSFIXUPAdditive = 7

    }
}