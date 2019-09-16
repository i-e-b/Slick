using System;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using JetBrains.Annotations;

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace SlickUWP.Gui
{
    public sealed partial class ImportFloater : UserControl
    {
        private bool _moving = false;
        private Point? _lastPoint;

        public ImportFloater()
        {
            InitializeComponent();

            PointerMoved += ImportFloater_PointerMoved;
            PointerPressed += ImportFloater_PointerPressed;
            PointerReleased += ImportFloater_PointerReleased;
        }

        private void ImportFloater_PointerReleased(object sender, [NotNull]PointerRoutedEventArgs e) { 
            _moving = false;
            ReleasePointerCapture(e.Pointer);
        }
        private void ImportFloater_PointerPressed(object sender, [NotNull]PointerRoutedEventArgs e) {
            _moving = true;

            CapturePointer(e.Pointer);
            _lastPoint = e.GetCurrentPoint(null)?.Position;
        }

        private void ImportFloater_PointerMoved(object sender, [NotNull]PointerRoutedEventArgs e)
        {
            if (!_moving || _lastPoint == null) return;

            // if pressed, change our padding relative to parent
            e.Handled = true;

            var pt = e.GetCurrentPoint(null)?.Position;
            if (pt == null) return;

            var dx = pt.Value.X - _lastPoint.Value.X;
            var dy = pt.Value.Y - _lastPoint.Value.Y;
            _lastPoint = pt;

            Margin = new Thickness(Margin.Left + dx, Margin.Top + dy, 0, 0); // little sample
        }
    }
}
