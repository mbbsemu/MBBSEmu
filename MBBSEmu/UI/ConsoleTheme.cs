using System.Linq;
using Terminal.Gui.App;
using Terminal.Gui.Configuration;
using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using Attribute = Terminal.Gui.Drawing.Attribute;

namespace MBBSEmu.UI
{
    /// <summary>
    ///     The look of The Major BBS's DOS console: blue background, bright cyan double-line frames, panel titles as
    ///     black-on-gray tabs, magenta system status, green stats, yellow users and dim red empty channels
    ///
    ///     Colors are the DOS (CGA/VGA text mode) palette values the original used, so truecolor terminals match it
    ///     exactly and others get the nearest color. Defined in code so Terminal.Gui themes and user configuration
    ///     files can't change it.
    /// </summary>
    public static class ConsoleTheme
    {
        /// <summary>
        ///     The 16-color DOS text mode palette
        /// </summary>
        public static class Cga
        {
            public static readonly Color Black = new(0x00, 0x00, 0x00);
            public static readonly Color Blue = new(0x00, 0x00, 0xAA);
            public static readonly Color Green = new(0x00, 0xAA, 0x00);
            public static readonly Color Cyan = new(0x00, 0xAA, 0xAA);
            public static readonly Color Red = new(0xAA, 0x00, 0x00);
            public static readonly Color Magenta = new(0xAA, 0x00, 0xAA);
            public static readonly Color LightGray = new(0xAA, 0xAA, 0xAA);
            public static readonly Color DarkGray = new(0x55, 0x55, 0x55);
            public static readonly Color BrightGreen = new(0x55, 0xFF, 0x55);
            public static readonly Color BrightCyan = new(0x55, 0xFF, 0xFF);
            public static readonly Color BrightRed = new(0xFF, 0x55, 0x55);
            public static readonly Color BrightMagenta = new(0xFF, 0x55, 0xFF);
            public static readonly Color Yellow = new(0xFF, 0xFF, 0x55);
            public static readonly Color White = new(0xFF, 0xFF, 0xFF);
        }

        public static Attribute On(Color foreground, Color background) => new(foreground, background);

        /// <summary>
        ///     Panel title tab, like "System Status" on the MajorBBS console
        /// </summary>
        public static readonly Attribute Tab = On(Cga.Black, Cga.LightGray);

        /// <summary>
        ///     Selected row or item in the focused view
        /// </summary>
        public static readonly Attribute Selection = On(Cga.Black, Cga.Cyan);

        /// <summary>
        ///     Content: light gray on blue, with white for headings and emphasis
        /// </summary>
        public static readonly Scheme Base = new(On(Cga.LightGray, Cga.Blue))
        {
            Focus = Selection,
            Active = On(Cga.LightGray, Cga.Blue),
            HotNormal = On(Cga.BrightCyan, Cga.Blue),
            HotFocus = Selection,
            Editable = On(Cga.Black, Cga.Cyan),
            ReadOnly = On(Cga.LightGray, Cga.Blue),
            Disabled = On(Cga.DarkGray, Cga.Blue)
        };

        /// <summary>
        ///     Table column headings
        /// </summary>
        public static readonly Scheme Heading = Uniform(On(Cga.White, Cga.Blue));

        /// <summary>
        ///     Frame lines; a focused frame draws its title as a tab
        /// </summary>
        public static readonly Scheme Frame = new(On(Cga.BrightCyan, Cga.Blue))
        {
            Focus = Tab,
            Active = On(Cga.BrightCyan, Cga.Blue),
            HotNormal = On(Cga.BrightCyan, Cga.Blue),
            HotFocus = Tab
        };

        /// <summary>
        ///     Panel title tabs; the focused panel's tab is highlighted so Tab's position is visible
        /// </summary>
        public static readonly Scheme PanelTab = Uniform(Tab);
        public static readonly Scheme FocusedPanelTab = Uniform(On(Cga.Black, Cga.BrightCyan));

        /// <summary>
        ///     Menu bar and status bar: black on gray with red hotkeys
        /// </summary>
        public static readonly Scheme Menu = new(Tab)
        {
            Focus = On(Cga.White, Cga.Black),
            Active = On(Cga.White, Cga.Black),
            HotNormal = On(Cga.Red, Cga.LightGray),
            HotFocus = On(Cga.BrightRed, Cga.Black)
        };

        /// <summary>
        ///     Shortcut keys on the status bar
        /// </summary>
        public static readonly Scheme MenuHotkey = Uniform(On(Cga.Red, Cga.LightGray));

        /// <summary>
        ///     Audit Log rows, like the console's System Status
        /// </summary>
        public static readonly Scheme AuditRow = Row(Cga.BrightMagenta);

        /// <summary>
        ///     INFO Application Log rows, in the green of the console's stats
        /// </summary>
        public static readonly Scheme InfoRow = Row(Cga.BrightGreen);

        /// <summary>
        ///     TRACE and DEBUG rows
        /// </summary>
        public static readonly Scheme DebugRow = Row(Cga.Green);

        public static readonly Scheme WarningRow = Row(Cga.Yellow);

        /// <summary>
        ///     ERROR and CRIT rows
        /// </summary>
        public static readonly Scheme ErrorRow = Row(Cga.BrightRed);

        /// <summary>
        ///     Channels: logged-in users in yellow (the console's user blocks), sessions still connecting in white,
        ///     and empty channels in dim red (the console's dashes)
        /// </summary>
        public static readonly Attribute OnlineChannel = On(Cga.Yellow, Cga.Blue);
        public static readonly Attribute ConnectingChannel = On(Cga.White, Cga.Blue);
        public static readonly Attribute DimChannel = On(Cga.Red, Cga.Blue);

        /// <summary>
        ///     Dialogs and message boxes: white on blue with a gray tab title and gray focused button
        /// </summary>
        public static readonly Scheme DialogScheme = new(On(Cga.White, Cga.Blue))
        {
            Focus = Tab,
            Active = Tab,
            HotNormal = On(Cga.Yellow, Cga.Blue),
            HotFocus = On(Cga.Red, Cga.LightGray),
            Editable = On(Cga.Black, Cga.Cyan),
            ReadOnly = On(Cga.White, Cga.Blue),
            Disabled = On(Cga.DarkGray, Cga.Blue)
        };

        /// <summary>
        ///     Error message boxes: white on red
        /// </summary>
        public static readonly Scheme ErrorDialogScheme = new(On(Cga.White, Cga.Red))
        {
            Focus = Tab,
            Active = Tab,
            HotNormal = On(Cga.Yellow, Cga.Red),
            HotFocus = On(Cga.Red, Cga.LightGray),
            ReadOnly = On(Cga.White, Cga.Red)
        };

        /// <summary>
        ///     Applies the theme's colors to Terminal.Gui's own dialogs (message boxes, the Save dialog), which are
        ///     created inside Terminal.Gui and can't be styled one by one. Process-wide; called before a UI starts.
        /// </summary>
        public static void ApplyGlobalDefaults()
        {
            SchemeManager.AddScheme(SchemeManager.SchemesToSchemeName(Schemes.Base), Base);
            SchemeManager.AddScheme(SchemeManager.SchemesToSchemeName(Schemes.Dialog), DialogScheme);
            SchemeManager.AddScheme(SchemeManager.SchemesToSchemeName(Schemes.Error), ErrorDialogScheme);
            SchemeManager.AddScheme(SchemeManager.SchemesToSchemeName(Schemes.Menu), Menu);
        }

        /// <summary>
        ///     DOS screens had no drop shadows: removes them from a view and everything in it, and gives dialogs the
        ///     same double-line border as the rest of the UI
        /// </summary>
        public static void ApplyRetroStyle(View view)
        {
            if (view is Dialog)
                view.BorderStyle = LineStyle.Double;

            if (view.ShadowStyle is { } shadow && shadow != ShadowStyles.None)
                view.ShadowStyle = ShadowStyles.None;

            //Dialogs keep their buttons in an adornment (border/padding) rather than among their SubViews
            var children = view.SubViews
                .Concat(view.Margin?.View?.SubViews ?? Enumerable.Empty<View>())
                .Concat(view.Border?.View?.SubViews ?? Enumerable.Empty<View>())
                .Concat(view.Padding?.View?.SubViews ?? Enumerable.Empty<View>())
                .Concat(view is Dialog dialog ? dialog.Buttons : Enumerable.Empty<View>())
                .Distinct()
                .ToList();

            foreach (var child in children)
                ApplyRetroStyle(child);
        }

        /// <summary>
        ///     Applies ApplyRetroStyle to everything the application shows, including the message boxes and Save
        ///     dialog that Terminal.Gui creates itself, as each one opens
        /// </summary>
        public static void StyleDialogsOf(IApplication app) =>
            app.SessionBegun += (_, e) =>
            {
                if (e.State?.Runnable is View view)
                    ApplyRetroStyle(view);
            };

        /// <summary>
        ///     The same colors in every role, so Terminal.Gui doesn't derive dimmer variants
        /// </summary>
        public static Scheme Uniform(Attribute colors) => new(colors)
        {
            Focus = colors,
            Active = colors,
            ReadOnly = colors,
            Editable = colors,
            Disabled = colors
        };

        /// <summary>
        ///     A log row color; highlighted only while it is the selected row of the focused table
        /// </summary>
        private static Scheme Row(Color foreground) => new(On(foreground, Cga.Blue))
        {
            Focus = Selection,
            Active = On(foreground, Cga.Blue)
        };
    }
}
