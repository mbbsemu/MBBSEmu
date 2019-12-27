namespace MBBSEmu.HostProcess.Formatters
{
    public interface IANSIFormatter
    {
        string StringFormat(string input, params object[] options);
        string Encode(string input);
        string MoveCursor(ANSIFormatter.enmCursorDirection direction, byte count = 1);
    }
}