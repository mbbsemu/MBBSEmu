using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terminal.Gui;

namespace MBBSEmu.UI.Main
{
    [UIMetadata(name: "Host Window", description: "MBBEmu Main Host Window")]
    public class MainView : UIBase
    {
        public override void Setup()
        {
            //Setup a full screen Text Area for log messages
            MainWindow.Add(new TextView()
            {
                X = 0,
                Y = 0,
                Width = Dim.Fill(),
                Height = Dim.Fill(),
                ReadOnly = true,
                ColorScheme = Colors.TopLevel
            });
        }
    }
}
