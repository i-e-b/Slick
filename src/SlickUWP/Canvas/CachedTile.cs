using System;
using Windows.Foundation;
using Windows.Graphics.DirectX;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;
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
        public CanvasControl UiCanvas;

        public TileState State;

        public byte[] RawImageData;

        public CachedTile()
        {
            State = TileState.Locked;

            UiCanvas = new CanvasControl();
            UiCanvas.Draw += Tile_Draw;
            UiCanvas.Margin = new Thickness(0.0);
            UiCanvas.Height = 256;
            UiCanvas.Width = 256;
            UiCanvas.HorizontalAlignment = HorizontalAlignment.Left;
            UiCanvas.VerticalAlignment = VerticalAlignment.Top;
        }

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
                        g.Clear(Colors.Red); // Should not happen!
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
                            g.Clear(Colors.Red);
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
            if (UiCanvas == null) return;
            UiCanvas.Draw -= Tile_Draw;
        }

        /// <summary>
        /// Change the tile state
        /// </summary>
        public void SetState(TileState state)
        {
            State = state;
            UiCanvas?.Invalidate();
        }
    }
}