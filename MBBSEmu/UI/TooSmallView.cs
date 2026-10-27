using System;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace MBBSEmu.UI
{
    /// <summary>
    ///     Full-screen notice shown in place of the UI while the terminal is below the minimum size
    /// </summary>
    public sealed class TooSmallView : View
    {
        private readonly string _quitHint;
        private readonly Label _message;

        public TooSmallView(string quitHint)
        {
            _quitHint = quitHint;

            X = 0;
            Y = 0;
            Width = Dim.Fill();
            Height = Dim.Fill();
            CanFocus = false;
            Visible = false;

            _message = new Label { TextAlignment = Alignment.Center };
            Add(_message);
        }

        /// <summary>
        ///     Shows the notice centered for the current screen size
        /// </summary>
        public void Show(int width, int height)
        {
            var lines = ScreenSize.TooSmallMessage(width, height, _quitHint);

            _message.Text = string.Join("\n", lines);
            _message.Width = Math.Max(1, width);
            _message.Height = lines.Count;
            _message.X = 0;
            _message.Y = Math.Max(0, (height - lines.Count) / 2);

            Visible = true;
            SetNeedsLayout();
        }

        public void Hide() => Visible = false;
    }
}
