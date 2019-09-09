using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Shapes;

namespace SlickUWP.Gui
{
    public sealed partial class CornerPalette : UserControl
    {
        private readonly Color DefaultColor = Colors.BlueViolet;

        public CornerPalette()
        {
            InitializeComponent();
            LastColor = Colors.Black;
        }

        public Color LastColor { get; set; }
        public double LastSize { get; set; }

        public bool IsHit(PointerEventArgs point)
        {
            var hits = VisualTreeHelper.FindElementsInHostCoordinates(point.CurrentPoint.Position, this);
            if (hits == null) return false;

            foreach (var hit in hits)
            {
                if (hit is Ellipse blob) {
                    var fill =  blob.Fill as SolidColorBrush;
                    LastColor = fill?.Color ?? DefaultColor;
                    LastSize = blob.ActualWidth / 10.0;
                    return true;
                }
            }
            return false;
        }
    }
}
