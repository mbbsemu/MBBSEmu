namespace MBBSEmu.Util
{
    public enum EnumMessageEvent
    {
        EnableModule, //Enables the specified module within MbbsHost
        DisableModule, //Disables the specific module within MbbsHost 
        Cleanup // Initiates a manual system cleanup identical to a scheduled cleanup
    }
}
