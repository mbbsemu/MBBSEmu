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
        Shutdown,

        /// <summary>
        ///     Pick a module to load (Modules > Load Module)
        /// </summary>
        LoadModule
    }

    /// <summary>
    ///     Keys the Admin Console handles before Terminal.Gui does
    /// </summary>
    public static class AdminConsoleKeys
    {
        /// <summary>
        ///     Ctrl+Q and Ctrl+C ask to shut down. Esc (Terminal.Gui's default Quit key) and Ctrl+Z (its default
        ///     Suspend key on Linux and macOS) are swallowed: either would stop or freeze the BBS for every caller.
        ///     Ctrl+O loads a module.
        /// </summary>
        public static EnumConsoleKeyAction Classify(Key key)
        {
            if (key.KeyCode == Key.Q.WithCtrl.KeyCode || key.KeyCode == Key.C.WithCtrl.KeyCode)
                return EnumConsoleKeyAction.Shutdown;
            if (key.KeyCode == Key.O.WithCtrl.KeyCode)
                return EnumConsoleKeyAction.LoadModule;

            if (key.KeyCode == Key.Esc.KeyCode || key.KeyCode == Key.Z.WithCtrl.KeyCode)
                return EnumConsoleKeyAction.Swallow;

            return EnumConsoleKeyAction.None;
        }

        /// <summary>
        ///     What to do with a key, given whether the console (rather than a dialog) is on top. Dialogs handle their
        ///     own keys, so Esc can close them, but Ctrl+Z is swallowed everywhere: Terminal.Gui's Suspend binding is
        ///     application-wide and would freeze the BBS even from under a dialog.
        /// </summary>
        public static EnumConsoleKeyAction Decide(Key key, bool consoleOnTop)
        {
            if (key.KeyCode == Key.Z.WithCtrl.KeyCode)
                return EnumConsoleKeyAction.Swallow;

            return consoleOnTop ? Classify(key) : EnumConsoleKeyAction.None;
        }
    }
}
