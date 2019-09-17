using System;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Shapes;
using JetBrains.Annotations;
using SlickCommon.ImageFormats;
using SlickUWP.Canvas;
using SlickUWP.CrossCutting;

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace SlickUWP.Gui
{
    // ReSharper disable once RedundantExtendsListEntry
    public sealed partial class ImportFloater : UserControl
    {
        enum InteractKind { None, Move, Size }
        private InteractKind _interactKind = InteractKind.None;
        private Point? _lastPoint;
        private TileCanvas _canvas;

        public ImportFloater()
        {
            InitializeComponent();

            PointerMoved += ImportFloater_PointerMoved;
            PointerPressed += ImportFloater_PointerPressed;
            PointerReleased += ImportFloater_PointerReleased;
        }

        public void SetCanvasTarget(TileCanvas canvas) {
            _canvas = canvas;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            // Make ourself invisible
            Visibility = Visibility.Collapsed;
        }

        private async void ImportButton_Click(object sender, RoutedEventArgs e)
        {
            if (_canvas == null) return;

            // TODO: scale target based on canvas zoom
            var expectedWidth = (int)ActualWidth;// * 2;
            var expectedHeight = (int)ActualHeight;// * 2;

            // render to image
            var rtb = new RenderTargetBitmap();
            await rtb.RenderAsync(ImageToImport, expectedWidth, expectedHeight).NotNull(); // Render control to RenderTargetBitmap

            // get pixels from RTB
            var pixelBuffer = await rtb.GetPixelsAsync().NotNull(); // BGRA 8888 format
            var rawImage = new RawImageInterleaved_UInt8{
                Data = pixelBuffer.ToArray(),
                Height = rtb.PixelHeight,
                Width = rtb.PixelWidth
            };

            // render to canvas
            int left = (int)Margin.Left;
            int top = (int)Margin.Top;
            _canvas.ImportBytes(rawImage, left, top, rawImage.Width, rawImage.Height, 0, 0);

            // make ourself invisible
            Visibility = Visibility.Collapsed;
        }

        private void ImportFloater_PointerReleased(object sender, [NotNull]PointerRoutedEventArgs e) { 
            _interactKind = InteractKind.None;
            ReleasePointerCapture(e.Pointer);
        }

        private void ImportFloater_PointerPressed(object sender, [NotNull]PointerRoutedEventArgs e) {
            // if pressed in corner, use scale


            CapturePointer(e.Pointer);
            _lastPoint = e.GetCurrentPoint(null)?.Position;

            _interactKind = SizeTagHit(_lastPoint) ? InteractKind.Size : InteractKind.Move;

        }

        private void ImportFloater_PointerMoved(object sender, [NotNull]PointerRoutedEventArgs e)
        {
            switch (_interactKind)
            {
                case InteractKind.Move:
                    MoveFloater(e);
                    break;
                    
                case InteractKind.Size:
                    ResizeFloater(e);
                    break;
            }
        }

        private bool SizeTagHit(Point? point)
        {
            if (point == null) return false;
            
            var hits = VisualTreeHelper.FindElementsInHostCoordinates(point.Value, this);
            if (hits == null) return false;

            return hits.OfType<Rectangle>().Any();
        }

        /// <summary>
        /// Resize the floater
        /// </summary>
        private void ResizeFloater([NotNull]PointerRoutedEventArgs e)
        {
            e.Handled = true;

            var pt = e.GetCurrentPoint(null)?.Position;
            if (pt == null || _lastPoint == null) return;

            var dx = pt.Value.X - _lastPoint.Value.X;
            var dy = pt.Value.Y - _lastPoint.Value.Y;
            _lastPoint = pt;

            var height = ActualHeight + dy;
            var width = ActualWidth + dx;

            if (height > 16) Height = height;
            if (width > 16) Width = width;
        }

        /// <summary>
        /// Change padding relative to parent to move the floater
        /// </summary>
        private void MoveFloater([NotNull]PointerRoutedEventArgs e)
        {
            e.Handled = true;

            var pt = e.GetCurrentPoint(null)?.Position;
            if (pt == null || _lastPoint == null) return;

            var dx = pt.Value.X - _lastPoint.Value.X;
            var dy = pt.Value.Y - _lastPoint.Value.Y;
            _lastPoint = pt;

            Margin = new Thickness(Margin.Left + dx, Margin.Top + dy, 0, 0);
        }

    }
}
