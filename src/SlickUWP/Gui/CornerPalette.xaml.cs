using System;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Shapes;

namespace SlickUWP.Gui
{
    // ReSharper disable once RedundantExtendsListEntry
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
            if (point?.CurrentPoint == null) return false;

            var hits = VisualTreeHelper.FindElementsInHostCoordinates(point.CurrentPoint.Position, this);
            if (hits == null) return false;

            foreach (var hit in hits)
            {
                if (!(hit is Ellipse blob)) continue;

                var fill =  blob.Fill as SolidColorBrush;
                LastColor = fill?.Color ?? DefaultColor;
                LastSize = MapSize(blob.ActualWidth);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Map the palette width to a pen width (they are not the same to make display and interaction easier)
        /// </summary>
        private double MapSize(double buttonWidth)
        {
            // 18 -> 1.5
            // 22 -> 2.5
            // 30 -> 6.5
            // 50 -> 30

            var b = buttonWidth / 16.0;
            return Math.Pow(b, 3);
        }
    }
}
