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
    public sealed partial class TextFloater : UserControl
    {
        enum InteractKind { None, Move, Size }
        private InteractKind _interactKind = InteractKind.None;
        private Point? _lastPoint;
        private TileCanvas _canvas;

        public TextFloater()
        {
            InitializeComponent();

            PointerMoved += TextFloater_PointerMoved;
            PointerPressed += TextFloater_PointerPressed;
            PointerReleased += TextFloater_PointerReleased;
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
            if (_canvas == null || textBlockToRender == null) return;

            var margin = textBlockToRender.Margin.Top;

            // scale target based on canvas zoom
            var zoom = _canvas.CurrentZoom();
            int expectedWidth = (int)(ActualWidth * zoom);
            int expectedHeight = (int)((ActualHeight-margin) * zoom);

            // render to image
            var rtb = new RenderTargetBitmap();
            await rtb.RenderAsync(textBlockToRender, expectedWidth, expectedHeight).NotNull(); // Render control to RenderTargetBitmap

            // DPI mismatch -- use first attempt to calculate the real scale and render again.
            var scaleW = expectedWidth / (float)rtb.PixelWidth;
            var scaleH = expectedHeight / (float)rtb.PixelHeight;

            // render to image
            rtb = new RenderTargetBitmap();
            await rtb.RenderAsync(textBlockToRender, (int)(expectedWidth * scaleW), (int)(expectedHeight * scaleH)).NotNull(); // Render control to RenderTargetBitmap

            // get pixels from RTB
            var pixelBuffer = await rtb.GetPixelsAsync().NotNull(); // BGRA 8888 format
            var rawImage = new RawImageInterleaved_UInt8{
                Data = pixelBuffer.ToArray(),
                Height = rtb.PixelHeight,
                Width = rtb.PixelWidth
            };

            // render to canvas
            int left = (int)(Margin.Left);
            int top = (int)(Margin.Top + textBlockToRender.Margin.Top);
            int right = left + (int)Width;
            int bottom = top + (int)(Height-margin);
            _canvas.ImportBytesScaled(rawImage, left, top, right, bottom);

            // ReSharper disable RedundantAssignment
            pixelBuffer = null;
            rtb = null;
            GC.Collect();
            // ReSharper restore RedundantAssignment

            // make ourself invisible
            Visibility = Visibility.Collapsed;
        }

        private void TextFloater_PointerReleased(object sender, [NotNull]PointerRoutedEventArgs e) { 
            _interactKind = InteractKind.None;
            ReleasePointerCapture(e.Pointer);
        }

        private void TextFloater_PointerPressed(object sender, [NotNull]PointerRoutedEventArgs e) {
            // if pressed in corner, use scale


            CapturePointer(e.Pointer);
            _lastPoint = e.GetCurrentPoint(null)?.Position;

            _interactKind = SizeTagHit(_lastPoint) ? InteractKind.Size : InteractKind.Move;

        }

        private void TextFloater_PointerMoved(object sender, [NotNull]PointerRoutedEventArgs e)
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

            return hits.OfType<Rectangle>().Any(rect=> rect.Name == "ResizeTab");
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

            if (height > 64) Height = height;
            if (width > 192) Width = width;
        }

        /// <summary>
        /// Change padding relative to parent to move the floater
        /// </summary>
        private void MoveFloater([NotNull]PointerRoutedEventArgs e)
        {
            //e.Handled = true;

            var pt = e.GetCurrentPoint(null)?.Position;
            if (pt == null || _lastPoint == null) return;

            var dx = pt.Value.X - _lastPoint.Value.X;
            var dy = pt.Value.Y - _lastPoint.Value.Y;
            _lastPoint = pt;

            Margin = new Thickness(Margin.Left + dx, Margin.Top + dy, 0, 0);
        }
    }
}
