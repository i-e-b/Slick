using System;
using Windows.Foundation;
using Windows.Graphics.DirectX;
using Windows.UI;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using JetBrains.Annotations;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;

namespace SlickUWP.Canvas
{
    /// <summary>
    /// Looks after UI elements of a tile
    /// </summary>
    internal class CachedTile
    {
        [NotNull] private readonly CanvasControl UiCanvas;

        public TileState State;

        private byte[] RawImageData;
        private float _x, _y;
        private volatile bool _detached = false;

        public const int ByteSize = 256 * 256 * 4;
        public int Width => 256;
        public int Height => 256;

        public CachedTile([NotNull]Panel container)
        {
            State = TileState.Locked;
            UiCanvas = Win2dCanvasPool.Employ(container);
            UiCanvas.Tag = this; // allow the Win2dCanvasPool and it's singleton draw event to find us.
            
            UiCanvas.RenderTransform = new TranslateTransform { X = _x, Y = _y };
            UiCanvas.Invalidate();
        }

        ~CachedTile() {
            if (!_detached)
                throw new Exception("Cached tile was garbage collected without being detached!");
        }
        

        public void SetTileData(byte[] rawData) {
            RawImageData = rawData;
            UiCanvas.Invalidate();
        }

        public byte[] GetTileData()
        {
            return RawImageData;
        }

        public void DrawToSession([NotNull]CanvasControl sender, [NotNull]CanvasDrawingSession g)
        {
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

                    try
                    {
                        using (var bmp = CanvasBitmap.CreateFromBytes(sender, RawImageData, 256, 256, // these two should be doubled if we interpolate
                            DirectXPixelFormat.B8G8R8A8UIntNormalized, 96, CanvasAlphaMode.Premultiplied))
                        {
                            g.DrawImage(bmp, new Rect(0, 0, 256, 256));
                            g.Flush(); // you'll get "Exception thrown at 0x12F9AF43 (Microsoft.Graphics.Canvas.dll) in SlickUWP.exe: 0xC0000005: Access violation reading location 0x1B2EEF78. occurred"
                            // if you fail to flush before disposing of the bmp
                        }
                    }
                    catch
                    {
                        g.Clear(Colors.DarkOrange);
                    }

                    return;

                default:
                    g.Clear(Colors.MediumTurquoise);
                    return;
                //throw new Exception("Non exhaustive switch in Tile_Draw");
            }
        }

        /// <summary>
        /// Position this tile (in viewport coordinates)
        /// </summary>
        public void MoveTo(float x, float y)
        {
            _x = x;
            _y = y;
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
            UiCanvas.Tag = null; 
            Win2dCanvasPool.Retire(UiCanvas);
            _detached = true;
        }

        /// <summary>
        /// Change the tile state
        /// </summary>
        public void SetState(TileState state)
        {
            State = state;
            UiCanvas.Invalidate();
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
            //UiCanvas.Draw += Tile_Draw;
            UiCanvas.Invalidate();
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