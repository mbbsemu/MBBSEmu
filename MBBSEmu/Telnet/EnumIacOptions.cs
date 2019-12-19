namespace MBBSEmu.Telnet
{
    public enum EnumIacOptions : byte
    {
        BinaryTransmission = 0,
        Echo = 1,
        SuppressGoAhead = 3,
        Status = 5,
        TimingMark = 6,
        TerminalType = 24,
        NegotiateAboutWindowSize = 31,
        TerminalSpeed = 32,
        RemoteFlowControl = 33,
        Linemode = 34,
        EnvironmentOption = 36,
        None = 0xFF
    }
}
