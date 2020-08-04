namespace MBBSEmu.Session.Telnet
{
    /// <summary>
    ///     Enumerator for IAC Verbs and their values
    /// </summary>
    public enum EnumIacVerbs : byte
    {
        WILL = 251,
        WONT = 252,
        DO = 253,
        DONT = 254,
        None = 0xFF
    }
}
