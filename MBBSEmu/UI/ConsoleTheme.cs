using Terminal.Gui.Drawing;
using Attribute = Terminal.Gui.Drawing.Attribute;

namespace MBBSEmu.UI
{
    /// <summary>
    ///     The classic MBBSEmu look (blue background, cyan double borders), defined in code so Terminal.Gui
    ///     themes and user configuration files can't change it
    /// </summary>
    public static class ConsoleTheme
    {
        private static readonly Attribute Selection = new(ColorName16.Black, ColorName16.Gray);

        /// <summary>
        ///     Content: white on blue, with the selected row black on gray
        /// </summary>
        public static readonly Scheme Base = new(new Attribute(ColorName16.White, ColorName16.Blue))
        {
            Focus = Selection,
            Active = Selection,
            HotNormal = new Attribute(ColorName16.BrightCyan, ColorName16.Blue),
            HotFocus = new Attribute(ColorName16.BrightBlue, ColorName16.Gray),
            Editable = new Attribute(ColorName16.BrightYellow, ColorName16.Blue)
        };

        /// <summary>
        ///     Panel borders and titles
        /// </summary>
        public static readonly Scheme Frame = new(new Attribute(ColorName16.BrightCyan, ColorName16.Blue))
        {
            Focus = new Attribute(ColorName16.BrightCyan, ColorName16.Blue),
            Active = new Attribute(ColorName16.BrightCyan, ColorName16.Blue)
        };

        /// <summary>
        ///     Menu bar and status bar
        /// </summary>
        public static readonly Scheme Menu = new(new Attribute(ColorName16.White, ColorName16.DarkGray))
        {
            Focus = new Attribute(ColorName16.White, ColorName16.Black),
            HotNormal = new Attribute(ColorName16.BrightYellow, ColorName16.DarkGray),
            HotFocus = new Attribute(ColorName16.BrightYellow, ColorName16.Black)
        };

        /// <summary>
        ///     Shortcut keys on the status bar
        /// </summary>
        public static readonly Scheme MenuHotkey = new(new Attribute(ColorName16.BrightYellow, ColorName16.DarkGray));

        public static readonly Scheme Selected = new(Selection);

        /// <summary>
        ///     ERROR and CRIT log rows
        /// </summary>
        public static readonly Scheme ErrorRow = new(new Attribute(ColorName16.BrightRed, ColorName16.Blue))
        {
            Focus = Selection,
            Active = Selection
        };

        /// <summary>
        ///     WARN log rows
        /// </summary>
        public static readonly Scheme WarningRow = new(new Attribute(ColorName16.BrightYellow, ColorName16.Blue))
        {
            Focus = Selection,
            Active = Selection
        };

        /// <summary>
        ///     DISCONNECTED channels
        /// </summary>
        public static readonly Attribute DimChannel = new(ColorName16.Gray, ColorName16.Blue);
    }
}
