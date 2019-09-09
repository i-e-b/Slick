using System;
using Windows.Foundation;
using Windows.Graphics.DirectX;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using JetBrains.Annotations;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;

namespace SlickUWP.Canvas
{
    public enum TileState
    {
        Locked, // waiting for data to be loaded
        Empty, // has no backing store
        Ready // loaded from backing store
    }

    /// <summary>
    /// Looks after UI elements of a tile
    /// </summary>
    internal class CachedTile
    {
        [NotNull] private readonly Panel _container;
        [NotNull] private readonly CoreDispatcher _dispatcher;
        private CanvasControl UiCanvas;

        public TileState State;

        public byte[] RawImageData;
        private float _x, _y;

        public CachedTile([NotNull]Panel container)
        {
            _dispatcher = container.Dispatcher ?? throw new Exception("Container panel had no valid dispatcher");

            _container = container;

            State = TileState.Locked;
            _dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                UiCanvas = new CanvasControl();
                UiCanvas.Draw += Tile_Draw;
                UiCanvas.Margin = new Thickness(0.0);
                UiCanvas.Height = 256;
                UiCanvas.Width = 256;
                UiCanvas.HorizontalAlignment = HorizontalAlignment.Left;
                UiCanvas.VerticalAlignment = VerticalAlignment.Top;

                _container.Children?.Add(UiCanvas);

                
                // There might have been image data loaded, and tile movement while waiting for the UI thread to catch up
                UiCanvas.RenderTransform = new TranslateTransform { X = _x, Y = _y };
                UiCanvas?.Invalidate();
            });
        }

        public const int ByteSize = 256 * 256 * 4;
        public int Width => 256;
        public int Height => 256;

        private void Tile_Draw(CanvasControl sender, CanvasDrawEventArgs args)
        {
            var g = args?.DrawingSession;
            if (g == null) return;

            switch (State)
            {
                case TileState.Locked:
                    g.Clear(Colors.DarkGray); // which is lighter than 'Gray'
                    return;

                case TileState.Empty:
                    g.Clear(Colors.White);
                    return;

                case TileState.Ready:

                    if (RawImageData == null)
                    {
                        g.Clear(Colors.Fuchsia); // Should not happen!
                        return;
                    }

                    using (var bmp = CanvasBitmap.CreateFromBytes(sender, RawImageData, 256, 256, // these two should be doubled if we interpolate
                        DirectXPixelFormat.B8G8R8A8UIntNormalized, 96, CanvasAlphaMode.Premultiplied))
                    {
                        try
                        {
                            g.DrawImage(bmp, new Rect(0, 0, 256, 256));
                        }
                        catch
                        {
                            g.Clear(Colors.DarkOrange);
                        }
                    }
                    return;

                default:
                    throw new Exception("Non exhaustive switch in Tile_Draw");
            }
        }

        /// <summary>
        /// Position this tile (in viewport coordinates)
        /// </summary>
        public void MoveTo(float x, float y)
        {
            _x = x;
            _y = y;
            if (UiCanvas == null) return;
            UiCanvas.RenderTransform = new TranslateTransform
            {
                X = x,
                Y = y
            };
        }

        /// <summary>
        /// Remove event bindings
        /// </summary>
        public void Detach()
        {
            _dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                // Container removal has to happen on a specific thread, because this is still 1991.
                if (UiCanvas != null)
                {
                    _container.Children?.Remove(UiCanvas);
                    UiCanvas.Draw -= Tile_Draw;
                }
            });
        }

        /// <summary>
        /// Change the tile state
        /// </summary>
        public void SetState(TileState state)
        {
            State = state;
            UiCanvas?.Invalidate();
        }

        /// <summary>
        /// Set the raw image data to solid white
        /// </summary>
        public void AllocateEmptyImage()
        {
            RawImageData = new byte[ByteSize];
            for (int i = 0; i < ByteSize; i++) { RawImageData[i] = 255; }
        }

        /// <summary>
        /// Returns true if the image data is empty, or all pixels are *very* close to white. Ignores alpha.
        /// </summary>
        public bool ImageIsBlank()
        {
            if (RawImageData == null || State == TileState.Empty) return true;
            if (State == TileState.Locked) return false;

            
            for (int i = 0; i < ByteSize; i+=4) {
                if (RawImageData[i+0] < 252) return false; // B
                if (RawImageData[i+1] < 252) return false; // G
                if (RawImageData[i+2] < 252) return false; // R
            }
            return true;
        }

        public void Invalidate()
        {
            UiCanvas?.Invalidate();
        }

        /// <summary>
        /// Remove the cache, and set state to empty
        /// </summary>
        public void Deallocate()
        {
            State = TileState.Empty;
            RawImageData = null;
        }
    }
}