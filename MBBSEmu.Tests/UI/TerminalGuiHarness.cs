using System;
using System.Linq;
using Terminal.Gui.App;
using Terminal.Gui.Drivers;
using Terminal.Gui.Input;
using Terminal.Gui.Testing;
using Terminal.Gui.Views;

namespace MBBSEmu.Tests.UI
{
    /// <summary>
    ///     Runs a Terminal.Gui view headlessly at a fixed screen size and reads the rendered screen back as text
    /// </summary>
    public sealed class TerminalGuiHarness : IDisposable
    {
        private SessionToken _session;

        public IApplication App { get; }

        public TerminalGuiHarness(int width, int height)
        {
            App = Application.Create();
            App.Init(DriverRegistry.Names.ANSI);
            App.Driver!.SetScreenSize(width, height);
        }

        public void Show(Window window)
        {
            _session = App.Begin(window);
            App.LayoutAndDraw(true);
        }

        /// <summary>
        ///     Rendered screen, one string per row
        /// </summary>
        public string[] Screen()
        {
            App.LayoutAndDraw(true);
            return App.ToString()!.Split('\n').Select(x => x.TrimEnd('\r')).ToArray();
        }

        public void Resize(int width, int height)
        {
            App.Driver!.SetScreenSize(width, height);
            App.LayoutAndDraw(true);
        }

        public void Key(Key key)
        {
            App.InjectKey(key);
            App.LayoutAndDraw(true);
        }

        public void Dispose()
        {
            if (_session != null)
                App.End(_session);

            App.Dispose();
        }
    }
}
