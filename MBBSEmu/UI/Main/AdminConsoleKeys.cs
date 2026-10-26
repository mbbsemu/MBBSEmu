using Terminal.Gui.Input;

namespace MBBSEmu.UI.Main
{
    public enum EnumConsoleKeyAction
    {
        /// <summary>
        ///     Let Terminal.Gui handle the key normally
        /// </summary>
        None,

        /// <summary>
        ///     Drop the key before Terminal.Gui's application bindings see it
        /// </summary>
        Swallow,

        /// <summary>
        ///     Ask to shut down MBBSEmu
        /// </summary>
        Shutdown
    }

    /// <summary>
    ///     Keys the Admin Console handles before Terminal.Gui does
    /// </summary>
    public static class AdminConsoleKeys
    {
        /// <summary>
        ///     Ctrl+Q and Ctrl+C ask to shut down. Esc (Terminal.Gui's default Quit key) and Ctrl+Z (its default
        ///     Suspend key on Linux and macOS) are swallowed: either would stop or freeze the BBS for every caller.
        /// </summary>
        public static EnumConsoleKeyAction Classify(Key key)
        {
            if (key.KeyCode == Key.Q.WithCtrl.KeyCode || key.KeyCode == Key.C.WithCtrl.KeyCode)
                return EnumConsoleKeyAction.Shutdown;

            if (key.KeyCode == Key.Esc.KeyCode || key.KeyCode == Key.Z.WithCtrl.KeyCode)
                return EnumConsoleKeyAction.Swallow;

            return EnumConsoleKeyAction.None;
        }
    }
}
