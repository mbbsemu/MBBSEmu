using Terminal.Gui;

namespace MBBSEmu.UI
{
    public abstract class UIBase
    {
        /// <summary>
        ///     Base Class for all UI Views
        ///
        ///     Has Helpers Methods and Window Definitions for all UI Views
        /// </summary>
        public Window MainWindow { get; set; }

        public bool isRunning { get; set; }

        protected UIBase()
        {
            Application.Init();
            MainWindow = new Window()
            {
                X = 0,
                Y = 0,
                Width = Dim.Fill(),
                Height = Dim.Fill(),
                Border = new Border
                {
                    BorderStyle = BorderStyle.None
                }
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
            isRunning = true;
            Application.Run(Application.Top);
        }

        public virtual void RequestStop()
        {
            Application.RequestStop();
            isRunning = false;
        }
    }
}
