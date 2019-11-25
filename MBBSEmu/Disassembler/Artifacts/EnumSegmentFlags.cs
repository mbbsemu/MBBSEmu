namespace MBBSEmu.Disassembler.Artifacts
{
    public enum EnumSegmentFlags : ushort
    {        
        Code = 0,
        Data = 1,
        Iterated = 8,
        Fixed = 15,
        Movable = 16,
        Impure = 31,
        Pure = 32,
        LoadOnCall = 63,
        Preload = 64,
        ReadOnly = 127,
        ExecuteOnly = 128,
        HasRelocationInfo = 256,
        HasDebuggingInfo = 512
    }
}