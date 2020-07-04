namespace MBBSEmu.HostProcess.Structs
{
    /// <summary>
    ///     Special Key Code Values
    ///
    ///     These are for keys that aren't part of the ASCII spec (0-127)
    ///
    ///     GCOMM.H
    /// </summary>
    public enum EnumKeyCodes : ushort
    {
        CRSDN = (80 * 256),
        CRSUP = (72 * 256),
        CTRL = ((94 - 59) * 256)
    }
}
