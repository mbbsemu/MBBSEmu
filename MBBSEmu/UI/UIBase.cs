using Terminal.Gui;

namespace MBBSEmu.UI
{
    public abstract class UIBase
    {

        public Window MainWindow { get; set; }

        public readonly ColorScheme MbbsEmuColorScheme = new()
        {
            Normal = new Attribute(Color.White, Color.Blue),
            Focus = new Attribute(Color.BrightYellow, Color.Blue),
            HotNormal = new Attribute(Color.White, Color.Blue),
            HotFocus = new Attribute(Color.White, Color.Blue),
            Disabled = new Attribute(Color.White, Color.Blue)
        };

        protected UIBase()
        {
            Application.Init();
            MainWindow = new Window($"MBBSEmu :: {GetName()}")
            {
                X = 0,
                Y = 1,
                Width = Dim.Fill(),
                Height = Dim.Fill(),
                ColorScheme = MbbsEmuColorScheme
            };
            Application.Top.Add(MainWindow);
        }

        public string GetName() => UIMetadata.GetName(GetType());

        public string GetDescription() => UIMetadata.GetDescription(GetType());

        public virtual void Setup()
        {
        }

        public virtual void Run()
        {
            // Must explicit call Application.Shutdown method to shutdown.
            Application.Run(Application.Top);
        }

        public virtual void RequestStop()
        {
            Application.RequestStop();
        }
    }
}
