using System;
using System.Collections.Generic;
using System.Text;
using Terminal.Gui;
using Attribute = Terminal.Gui.Attribute;

namespace MBBSEmu.UI
{
    public class TextGUI
    {
        private Window _mainWindow;
        private Window _auditTrailWindow;

        private ColorScheme _mbbsColorScheme;

        public TextGUI()
        {
            Application.Init();

            _mbbsColorScheme = new ColorScheme()
            {
                Normal = Attribute.Make(Color.BrighCyan, Color.Blue),
            };


            _mainWindow = new Window(new Rect(0, 0, Application.Top.Frame.Width, Application.Top.Frame.Height),
                "MBBSEmu");
            _mainWindow.ColorScheme = _mbbsColorScheme;

            _auditTrailWindow = new Window(new Rect(0, 0, Application.Top.Frame.Width - 4, 15), "Audit Trail");
            _auditTrailWindow.ColorScheme = _mbbsColorScheme;

            _mainWindow.Add(_auditTrailWindow);
            
            
            Application.Top.Add(_mainWindow);

            
        }

        public void Run() => Application.Run();
    }
}
