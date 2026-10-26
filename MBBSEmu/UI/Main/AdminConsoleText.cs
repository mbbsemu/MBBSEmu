namespace MBBSEmu.UI.Main
{
    /// <summary>
    ///     User-visible text for the Admin Console
    /// </summary>
    public static class AdminConsoleText
    {
        public const string AboutTitle = "About MBBSEmu";
        public const string OkButton = "OK";

        public static string AboutMessage(string build) => "The MajorBBS Emulation Project\n" +
                                                           $"Build #{build}\n\n" +
                                                           "Open Source & Community Driven!\n" +
                                                           "Distributed under MIT License\n\n" +
                                                           "https://github.com/mbbsemu/MBBSEmu\n" +
                                                           "https://www.mbbsemu.com";
    }
}
